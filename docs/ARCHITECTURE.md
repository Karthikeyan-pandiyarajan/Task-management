# Architecture

TaskApp follows **Clean Architecture**: business rules sit at the centre, and every dependency points *inward*, toward them.

```
          ┌──────────────────────────────────────────────┐
          │  Api.Auth      Api.Tasks      (controllers)  │  presentation
          │            Api.Common (HTTP plumbing)        │
          └───────────────┬──────────────────┬───────────┘
                          │                  │
                          ▼                  ▼
          ┌──────────────────────┐   ┌───────────────────────┐
          │    Infrastructure    │──▶│      Application      │  use cases + ports
          │ EF Core, SQLite,     │   │ TaskService,          │
          │ PBKDF2, JWT, seeding │   │ AuthService, DTOs,    │
          └──────────────────────┘   │ ITaskRepository, ...  │
                                     └───────────┬───────────┘
                                                 ▼
                                     ┌───────────────────────┐
                                     │        Domain         │  entities
                                     │ TaskItem, User, enum  │
                                     └───────────────────────┘
```

## Layers

| Layer | Contains | May depend on | Must not depend on |
|---|---|---|---|
| **Domain** | `TaskItem`, `User`, `TaskItemStatus` | nothing | everything else |
| **Application** | `TaskService`, `AuthService`; request/response records; ports (`ITaskRepository`, `IUserRepository`, `IPasswordHasher`, `ITokenGenerator`); exceptions (`AppValidationException`, `NotFoundException`, `ConflictException`, `UnauthorizedException`) | Domain | EF Core, ASP.NET, JWT libraries, Infrastructure |
| **Infrastructure** | `AppDbContext`, EF repositories, migrations, `DemoDataSeeder`, `Pbkdf2PasswordHasher`, `JwtTokenGenerator`, options classes, `AddInfrastructure()` | Application, Domain | Api.* |
| **Api.Common** | `GlobalExceptionHandler`, JWT bearer + CORS + authorization fallback setup, `ClaimsPrincipal.GetUserId()` | Application, Infrastructure | Api.Auth, Api.Tasks |
| **Api.Auth / Api.Tasks** | Thin controllers and `Program.cs` composition roots | Api.Common, Application, Infrastructure | — |

## Why dependencies point inward

1. **The rules are the part that matters, so they depend on nothing volatile.** "A title can't exceed 100 characters" or "you only see your own tasks" don't change when we swap SQLite for PostgreSQL or JWT for cookies. Keeping Application free of those libraries means such swaps never touch business code.
2. **Dependency inversion through ports.** Application *declares* what it needs (`ITaskRepository`, `IPasswordHasher`, `ITokenGenerator`) and Infrastructure *implements* it. The direction of the source-code dependency (Infrastructure → Application) is the opposite of the runtime call direction (TaskService → EfTaskRepository). That's what lets the core stay ignorant of the database.
3. **Fast, deterministic tests.** Because Application only sees interfaces and `TimeProvider`, its 36 tests run in about 200 ms with Moq and a fixed clock: no database, no HTTP, no flakiness.
4. **Enforced by the compiler.** The `.csproj` references make the rule physical: Application has no reference to Infrastructure or ASP.NET, so a violation doesn't compile.
5. **Composition happens at the edge.** Only each API's `Program.cs` knows about every layer. It wires interfaces to implementations (`AddInfrastructure()`), and that's the single place to change when an implementation changes.

## Two APIs, one database

- `Api.Auth` (port 5001) issues tokens. `Api.Tasks` (port 5002) only validates them. Both bind the same `Jwt` settings (issuer, audience, secret), so a token from one is accepted by the other.
- Both point at the same SQLite file through `Database:Path = ../../data/taskapp.db`, resolved against each API's content root.
- Both call `InitializeDatabaseAsync()` on startup. EF Core migrations take a lock, and the seeder is idempotent (a unique-email race is caught), so either API can start first.
- Integration tests reproduce this exactly: both hosts run in-memory against one temp database file.

## Request flow (`PUT /api/tasks/{id}`)

1. `JwtBearer` validates signature, issuer, audience and lifetime. An invalid token gets **401**.
2. The authorization fallback policy requires an authenticated user.
3. `TasksController.Update` reads the user id from the `sub` claim (never from the body) and calls `TaskService.UpdateAsync(userId, id, request)`.
4. `TaskService` loads the task. A task that is missing *or* owned by someone else raises `NotFoundException`. It then validates the fields, applies the changes and calls `ITaskRepository.UpdateAsync`.
5. Any exception is turned into `ProblemDetails` by `GlobalExceptionHandler`: 400, 401, 404 or 409, and 500 without internal details.

## Security summary

| Concern | Mitigation |
|---|---|
| Seeing or changing others' data | Ownership check in the Application layer; the user id comes only from the token; 404 rather than 403 |
| Account enumeration | Identical "Invalid credentials" for unknown email and wrong password |
| Password storage | PBKDF2-HMAC-SHA512, 210k iterations, 16-byte random salt, constant-time comparison |
| Secrets in source | `Jwt:Secret` comes only from user-secrets or env var; startup fails if it's missing or under 32 chars |
| Duplicate accounts | Emails normalized (trim + lowercase); unique index enforced by the DB |
| Cross-origin calls | CORS allows only `http://localhost:4200` |
| Forgotten `[Authorize]` | Fallback policy: authenticated by default |
| Error leakage | 500 responses carry no exception text |
| Client open redirect | `returnUrl` only accepts in-app paths |

## Client architecture (Angular 22)

```
client/src/app/
  core/        auth.service (signals, token storage), auth.interceptor, auth.guard,
               models (typed DTOs), http/problem-details (error → message)
  features/
    auth/      login, register              (lazy routes, typed reactive forms)
    tasks/     task-list, task-form, task-api.service, tasks.routes (lazy)
```

- **Standalone and zoneless**, with `OnPush` everywhere. State lives in signals.
- **Data loading**: `rxResource` re-fetches when the status filter changes, cancels stale requests, and unsubscribes on destroy. One-off calls use `takeUntilDestroyed`.
- **Interceptor**: adds the token only to relative `/api/` URLs. On a 401 from any endpoint except login/register it logs out and redirects to `/login?returnUrl=…`.
- **Guards are UX only**: the API enforces every rule independently.

## Testing strategy

| Level | Tooling | Purpose |
|---|---|---|
| Unit (Application) | xUnit, Moq, FluentAssertions, fixed `TimeProvider` | Each business rule written test-first |
| Integration (Infrastructure) | Real SQLite in-memory, fresh `DbContext` per assertion | Mappings, indexes, queries and seeding behave as in production |
| Unit (controllers) | Controllers constructed directly with a fake `ClaimsPrincipal` and mocked ports | Controllers only translate HTTP ↔ service calls; the user id comes from the token |
| HTTP (Api.Tests) | `WebApplicationFactory` × 2, shared temp DB | Status codes, auth, cross-service token, user isolation |
| Client | Jasmine + Karma, `HttpTestingController` | Auth state, interceptor behaviour, list UI states |
