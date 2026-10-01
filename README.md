# TaskApp — Full-stack Task Management

A small but production-shaped task manager: two ASP.NET Core 10 APIs (Auth and Tasks) built with Clean Architecture and strict TDD, a shared SQLite database, and an Angular 22 client.

| Document | Contents |
|---|---|
| [PRESENTATION.md](PRESENTATION.md) | Thought process: user story, approach, design decisions, results |
| [docs/GENAI.md](docs/GENAI.md) | **GenAI portion**: the prompt, sample output, what was validated and corrected |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Layers and why dependencies point inward |

## User story

> As a registered user, I want to log in and create, view, update, and delete my own tasks so I can track what is due.
> I must never see or change another user's tasks.

## Prerequisites

| Tool | Version | Notes |
|---|---|---|
| .NET SDK | **10** | `dotnet --version` |
| Node.js | **24** | `node --version` |
| Angular CLI | 22 | `npm i -g @angular/cli` (optional, `npx ng` works too) |
| Google Chrome | any recent | only for `ng test` (Karma runs ChromeHeadless) |
| dotnet-ef | 10 | only to create new migrations: `dotnet tool install -g dotnet-ef` |

## Quick start

### 1. One-time: set the JWT signing secret

The secret is never committed. Both APIs share one user-secrets store, so this single command configures both:

```powershell
dotnet user-secrets set "Jwt:Secret" "$(New-Guid)$(New-Guid)" --project src/Api.Auth
```

(Alternative: set the environment variable `Jwt__Secret` to any 32+ character value in each terminal.)
If the secret is missing, the APIs refuse to start and print this exact command.

### 2. Run the two APIs and the client (three terminals, from the repo root)

```powershell
dotnet run --project src/Api.Auth    # http://localhost:5001
dotnet run --project src/Api.Tasks   # http://localhost:5002
```

```powershell
cd client
npm install
npm start                            # http://localhost:4200
```

Open **http://localhost:4200**. The Angular dev server proxies `/api/auth` → 5001 and `/api/tasks` → 5002 (`client/proxy.conf.json`), so the browser only ever talks to one origin.

On first start each API applies EF Core migrations and seeds demo data into **`data/taskapp.db`** (git-ignored). Delete the `data/` folder to reset.

### Demo credentials

| Email | Password |
|---|---|
| `demo@taskapp.com` | `Demo@1234` |

The demo user has 4 sample tasks in mixed statuses (one is past due but Done).

## Running the tests

```powershell
dotnet test                                                     # 94 tests: unit + SQLite + HTTP integration
cd client; npm test -- --watch=false --browsers=ChromeHeadless  # 20 Jasmine specs
```

| Suite | What it covers |
|---|---|
| `tests/Application.Tests` (36) | Every business rule of `TaskService` and `AuthService`, with Moq + a fixed `TimeProvider` |
| `tests/Infrastructure.Tests` (23) | EF repositories, unique email index, seeding against **real SQLite in-memory**; PBKDF2 hasher; JWT generator |
| `tests/Api.Tests` (35) | **Controller unit tests** (14, no HTTP pipeline) + **integration tests** (21): both APIs hosted with `WebApplicationFactory`, sharing one temp DB: 200/201/204/400/401/404/409 and *"user B cannot access user A's task"* |
| `client` (20) | `AuthService`, `authInterceptor`, `TaskList` (loading/error/empty, filter, delete confirmation), app shell |

The running app was also checked end to end in a real (headless) Chrome: login, filter, create, edit, delete, logout, and a second user's isolation, with **no console warnings or errors** and no horizontal scrolling at phone width.

## API

Both APIs are ASP.NET Core MVC controller-based Web APIs (`[ApiController]` + `ControllerBase`); the "view" is the Angular client, so there are no Razor views.

All task endpoints require `Authorization: Bearer <token>`. The user id is taken from the token's `sub` claim — never from the body or URL. Errors are RFC 7807 `ProblemDetails`.

| Method | Route | Success | Errors |
|---|---|---|---|
| POST | `/api/auth/register` | 201 `{ token, email }` | 400 invalid email/weak password, 409 email taken |
| POST | `/api/auth/login` | 200 `{ token, email }` | 401 `"Invalid credentials"` (same for unknown email and wrong password) |
| GET | `/api/auth/me` | 200 `{ id, email }` | 401 |
| GET | `/api/auth/ping` | 200 (anonymous) | — |
| GET | `/api/tasks?status=Todo\|InProgress\|Done` | 200 `Task[]` (ordered by due date) | 400 unknown status, 401 |
| GET | `/api/tasks/{id}` | 200 `Task` | 401, 404 (missing **or** someone else's) |
| POST | `/api/tasks` | 201 + `Location` | 400, 401 |
| PUT | `/api/tasks/{id}` | 200 `Task` | 400, 401, 404 |
| DELETE | `/api/tasks/{id}` | 204 | 401, 404 |

`Task = { id, title, description?, status: "Todo"|"InProgress"|"Done", dueDate }`

Business rules: title required and ≤ 100 chars; status must be a defined value; due date cannot be in the past **on create** (allowed on update so overdue tasks can still be completed). Passwords: ≥ 8 chars with a letter and a digit.

In Development, OpenAPI documents are served at `/openapi/v1.json` on each API.

## Architecture overview

```
src/
  Domain/          Entities: TaskItem, User, TaskItemStatus. No dependencies.
  Application/     Use cases (TaskService, AuthService), DTOs, ports (ITaskRepository, IPasswordHasher, ...), exceptions.
  Infrastructure/  EF Core + SQLite, repositories, PBKDF2 hasher, JWT generator, migrations, seeding.
  Api.Common/      Shared HTTP plumbing: exception → ProblemDetails, JWT bearer setup, CORS, auth fallback policy.
  Api.Auth/        AuthController (register, login, me, ping).
  Api.Tasks/       TasksController (CRUD + status filter).
client/            Angular 22 standalone app (core/ + lazy features/).
tests/             Application.Tests, Infrastructure.Tests, Api.Tests.
docs/              ARCHITECTURE.md, GENAI.md
```

Dependencies point inward: `Api.* → Application + Infrastructure`, `Infrastructure → Application → Domain`. Domain and Application know nothing about EF Core, ASP.NET or JWT, which is why every business rule is unit-tested in milliseconds without a database or web server. See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the full rationale.

## Key design decisions

- **404 instead of 403 for other users' tasks** — the API doesn't reveal whether a task id exists.
- **Generic "Invalid credentials"** for unknown email *and* wrong password — prevents account enumeration.
- **Secure-by-default authorization** — a fallback policy requires authentication on every endpoint; anonymous ones opt out with `[AllowAnonymous]`.
- **`TimeProvider` injected** — date rules are tested with a fixed clock, no flaky "today" tests.
- **PBKDF2-SHA512, 210k iterations, random salt, constant-time compare**; the iteration count is stored in each hash so it can be raised later.
- **JWT config from settings only**, validated at startup (`ValidateOnStart`), secret from user-secrets/env var, ≥ 256-bit key enforced.
- **Real SQLite in tests** rather than EF InMemory, so unique indexes and SQL translation behave as in production.
- **Status stored as text**, enums serialized as strings in JSON — readable DB and API, safe to reorder the enum.
- **Client**: signals for auth state, `rxResource` for the list (auto-cancel/unsubscribe), `takeUntilDestroyed` for one-off calls, functional interceptor/guards, lazy routes, typed reactive forms, `returnUrl` restricted to in-app paths.

## Known trade-offs / next steps

- The JWT lives in `localStorage` (simple, survives refresh) — an httpOnly cookie would resist XSS better but needs cookie + CSRF handling across both APIs.
- No refresh tokens; the session ends when the 60-minute token expires (the interceptor redirects to login).
- SQLite is fine for a demo; for production swap the provider in `Infrastructure` only.
- No pagination on `GET /api/tasks` yet.
