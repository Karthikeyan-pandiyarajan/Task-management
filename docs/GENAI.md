# Use of Generative AI

This project was built with an AI coding assistant (Claude Code) working under my direction in VS Code. I wrote the prompt below, reviewed every step, and own all design decisions. This document records the prompt, a sample of the generated output, and what I validated and corrected.

## The prompt

```text
CONTEXT
I'm building a full-stack Task Management app for a technical interview. The solution scaffold is already created and `dotnet build` succeeds. Do NOT re-create or re-scaffold projects. Continue from the current state.

ENVIRONMENT
- Windows, PowerShell, VS Code. Repo root: C:\AI\Task-Management (single monorepo, git already initialized).
- .NET SDK 10, Angular CLI 22, Node 24. Solution file: TaskApp.slnx
- Existing projects (already wired with references):
  src/Domain, src/Application, src/Infrastructure, src/Api.Auth, src/Api.Tasks
  tests/Application.Tests, tests/Infrastructure.Tests, tests/Api.Tests
- Packages already installed: EF Core SQLite + Design, JwtBearer, Moq, FluentAssertions 7.x, Mvc.Testing.
- Reference direction (do not change): Application -> Domain; Infrastructure -> Application; Api.* -> Application + Infrastructure. Domain and Application must never reference Infrastructure or ASP.NET.
- The Angular app will be created later in client/.

USER STORY
As a registered user, I want to log in and create, view, update, and delete my own tasks so I can track what is due. I must never see or change another user's tasks.

WORKING RULES
- Strict TDD: for each behavior, (1) write ONE failing xUnit test, (2) run `dotnet test` and show it failing, (3) write the minimum code to pass, (4) refactor, (5) propose a commit message ("test: ..." then "feat: ...").
- Work in small steps. Stop after each phase and wait for my "continue".
- Before writing code, restate the phase plan in 5 lines or fewer.
- Explain briefly why you made each design choice, because I must defend it in a code review.

PHASE 0: CLEAN UP AND FIRST COMMIT
- Delete template files: Class1.cs in Domain, Application, and Infrastructure; UnitTest1.cs in all three test projects.
- Run `dotnet build`, then commit: "chore: scaffold clean architecture solution".
- Leave the webapi template's sample /weatherforecast code alone for now; it is replaced in Phase 4.

PHASE 1: DOMAIN + APPLICATION (TaskService)
Domain (no tests needed):
- Enum TaskItemStatus { Todo, InProgress, Done }
- TaskItem: Id (Guid), Title, Description (nullable), Status, DueDate (DateTime), UserId (Guid)
- User: Id (Guid), Email, PasswordHash
Application:
- ITaskRepository: GetByIdAsync, GetByUserAsync(userId, status?), AddAsync, UpdateAsync, DeleteAsync
- DTOs as records: CreateTaskRequest, UpdateTaskRequest, TaskResponse (never expose entities)
- Exceptions: AppValidationException, NotFoundException
- TaskService takes ITaskRepository and TimeProvider (so the past-date rule is testable with a fixed clock).
Business rules, each driven by its own failing test in this order:
 1. Empty/whitespace title throws AppValidationException and never calls the repository
 2. Title over 100 characters throws
 3. Invalid enum status throws
 4. Due date in the past throws on create
 5. Valid create calls AddAsync once, sets UserId from the argument, returns the data
 6. Get another user's task throws NotFoundException (same 404 as missing, so IDs can't be probed)
 7. Get a missing task throws NotFoundException
 8. Update and Delete on another user's task throw NotFoundException and never modify the repository
 9. List passes the status filter to the repository
Use Moq + FluentAssertions. Commit after each green cycle.

PHASE 2: AUTH (AuthService)
- Interfaces: IUserRepository, IPasswordHasher, ITokenGenerator
- DTOs: RegisterRequest, LoginRequest, AuthResponse (token + email), no PasswordHash ever returned
- Rules, each test-first:
  - Invalid email format throws
  - Password under 8 chars, or missing a letter or a digit, throws
  - Duplicate email throws a ConflictException (add it, maps to 409)
  - Register stores a hashed password, never plain text
  - Login with wrong email and login with wrong password both throw the SAME generic "Invalid credentials" exception (UnauthorizedException, maps to 401)
  - Valid login returns a token from ITokenGenerator

PHASE 3: INFRASTRUCTURE
- AppDbContext (SQLite) with Users (unique index on Email) and Tasks tables
- EfTaskRepository, EfUserRepository (test against SQLite in-memory in Infrastructure.Tests, test-first)
- Pbkdf2PasswordHasher implementing IPasswordHasher (salted, constant-time compare)
- JwtTokenGenerator implementing ITokenGenerator; secret, issuer, audience, and expiry come from configuration, never hardcoded (user-secrets or env var for the dev secret)
- EF migration "Init" (`dotnet ef migrations add Init -p src/Infrastructure -s src/Api.Tasks`)
- Seed on startup: user demo@taskapp.com / Demo@1234 with 4 sample tasks in mixed statuses
- Both APIs must point at the SAME SQLite file, path from appsettings

PHASE 4: APIS + INTEGRATION TESTS
Replace the template minimal-API sample with controllers. Controllers stay thin: no business logic.
Api.Tasks (all [Authorize]; userId comes from the JWT, never the request body):
- POST /api/tasks -> 201 + Location, GET /api/tasks (?status=), GET /api/tasks/{id}, PUT /api/tasks/{id}, DELETE /api/tasks/{id} -> 204
Api.Auth:
- POST /api/auth/register (201/400/409), POST /api/auth/login (200/401), GET /api/auth/me ([Authorize]), GET /api/auth/ping ([AllowAnonymous])
Cross-cutting:
- Global exception handler mapping AppValidationException->400, NotFoundException->404, ConflictException->409, UnauthorizedException->401, returning ProblemDetails
- JWT bearer auth configured from settings in both APIs, CORS only for http://localhost:4200
- Integration tests with WebApplicationFactory covering 200/201/204/400/401/404/409, including "user B cannot access user A's task"

PHASE 5: ANGULAR CLIENT (client/)
- `ng new client --style=scss --routing` (standalone components), typed models (no `any`), lazy-loaded routes
- core/: AuthService (signals, token storage), HTTP interceptor (adds Bearer token, redirects to login on 401), auth guard
- features/auth: login and register with reactive forms and validation messages
- features/tasks: list with status filter, create/edit form, delete with confirmation, loading and error states
- Responsive SCSS, no browser console warnings, proper unsubscribe (async pipe or takeUntilDestroyed)
- proxy.conf.json forwarding /api/auth and /api/tasks to the two API ports
- A few Jasmine specs: AuthService, interceptor, task list

PHASE 6: DOCS
- README.md: prerequisites (.NET 10 SDK, Node 24, Angular CLI), commands to run both APIs and the client, how to run tests (dotnet test, ng test), demo credentials, architecture overview, user story
- docs/ARCHITECTURE.md: layers and why dependencies point inward
- docs/GENAI.md: this prompt, a sample of the generated output, and a "what I validated and corrected" section left for me to fill in

Begin with Phase 0, then Phase 1. Restate the plan first, then write the first failing test.
```

Mid-session I changed two working rules: no more per-step commits (I commit at the end), and continue through all phases without stopping for "continue".

## Sample of the generated output

**One TDD cycle (rule 7, "Get a missing task throws NotFoundException").** First the failing test:

```csharp
[Fact]
public async Task Get_missing_task_throws_not_found()
{
    _repository
        .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync((TaskItem?)null);

    var act = () => _sut.GetByIdAsync(UserId, Guid.NewGuid());

    await act.Should().ThrowAsync<NotFoundException>();
}
```

The red run, which caught a real bug in the minimal code written for rule 6:

```text
Failed Application.Tests.Tasks.TaskServiceTests.Get_missing_task_throws_not_found
  Expected a <Application.Common.NotFoundException> to be thrown, but found <System.NullReferenceException>
```

The green code, refactored into a helper that Update and Delete reuse:

```csharp
// Missing and not-owned produce the same exception so callers can't probe which task IDs exist.
private async Task<TaskItem> GetOwnedTaskAsync(Guid userId, Guid taskId, CancellationToken ct)
{
    var task = await repository.GetByIdAsync(taskId, ct);
    if (task is null || task.UserId != userId)
        throw new NotFoundException($"Task {taskId} was not found.");

    return task;
}
```

**Integration test for the core security requirement:**

```csharp
[Fact]
public async Task User_B_cannot_see_change_or_delete_user_As_task()
{
    using var userA = await fixture.CreateAuthenticatedTasksClientAsync();
    using var userB = await fixture.CreateAuthenticatedTasksClientAsync();
    var aTask = await CreateAsync(userA, NewTask("A's private task"));
    var url = $"/api/tasks/{aTask.Id}";

    (await userB.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await userB.PutAsJsonAsync(url, new UpdateTaskRequest("Hijacked", null, TaskItemStatus.Done, Tomorrow), Json))
        .StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await userB.DeleteAsync(url)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    (await userB.GetFromJsonAsync<List<TaskResponse>>("/api/tasks", Json)).Should().BeEmpty();

    var stillThere = await userA.GetFromJsonAsync<TaskResponse>(url, Json);
    stillThere!.Title.Should().Be("A's private task");
}
```

## Where the implementation deviates from the prompt (and why)

| Prompt said | What was done | Reason |
|---|---|---|
| `Api.* -> Application + Infrastructure` only | Added a small `src/Api.Common` class library referenced by both APIs | Avoids duplicating the exception handler, JWT bearer setup and CORS in two `Program.cs` files. It sits in the presentation layer, so the inward dependency rule still holds. |
| One failing test per rule | Rules 1–7 followed one-test cycles. Rules 8–9, the Auth rules and the Infrastructure tests were written as small red batches (each test shown failing, then implemented) | Picked up pace after I asked to finish without per-step stops |
| Rules 1–9 only | Added tests for the Update/Delete happy paths, Update validation, "past due date allowed on update", and "unknown status filter → 400" | Phase 4 needed these behaviours, and each one is a business rule |
| Integration tests with `WebApplicationFactory` | Those, plus isolated controller unit tests | The assignment asks for unit tests of the API endpoints as well |
| `ng new client --style=scss --routing` | Added `--test-runner=karma --skip-git --ssr=false` | Angular 22 defaults to Vitest, but the prompt asks for Jasmine specs. The repo already has git. SSR isn't needed |
| `dotnet ef migrations add Init ...` | Same, plus `-o Persistence/Migrations` | Keeps migrations next to `AppDbContext` |

## What I validated and corrected

My rule throughout: **AI output is a draft until something independent proves it** — a failing-then-passing test, the real application running, or my own reading of the diff. Nothing was accepted because it "looked right".

### How I validated the AI's suggestions

| Method | What it proved |
|---|---|
| **Red before green.** Every behaviour had to be shown failing with `dotnet test` before the implementation was written | The test really exercises the rule (a test that can't fail proves nothing), and the failure is for the right reason: an assertion, not a compile error |
| **94 .NET tests + 20 Jasmine specs**, re-run after every change | No regression as later phases touched earlier code |
| **Real SQLite in the data-layer tests**, asserting through a *fresh* `DbContext` | Data actually reached the database, and the unique email index and FK really exist. EF's change tracker or the InMemory provider would have hidden both |
| **Both real APIs started as separate processes** and called over HTTP | Things unit tests can't show: a token issued by Api.Auth is accepted by Api.Tasks, both share one SQLite file, CORS allows only `localhost:4200`, and startup fails with a clear message when `Jwt:Secret` is missing |
| **Automated real-browser run** (headless Chrome driving the running app): login → filter → create → edit → delete with confirmation → logout → register a second user | The UI works end to end, a second user sees none of the demo user's tasks, **0 console warnings/errors in normal use**, and no horizontal scrolling at 375 px width |
| **Build output**: 0 warnings in `dotnet build`, `ng build` and `ng serve` | No ignored compiler or template diagnostics |
| **Reading the diff** against the dependency rule | `Application.csproj` references only `Domain`; no EF Core, ASP.NET or JWT types leak inward |

### What was wrong or incomplete in the AI output, and how it was corrected

| # | Problem found | How it was found | Correction |
|---|---|---|---|
| 1 | `GetByIdAsync` threw `NullReferenceException` for a missing task (the minimal code for rule 6 only checked the owner) | Rule 7's test went red with the NRE instead of `NotFoundException` | Null and wrong-owner are now one check in `GetOwnedTaskAsync`, reused by Get, Update and Delete |
| 2 | `GET /api/tasks?status=99` bound to an undefined enum value and silently returned an empty list | Reviewing the controller's query binding before writing the integration tests | New failing test, then `ValidateStatus` in `TaskService.ListAsync` → 400 |
| 3 | My prompt only specified the past-due-date rule "on create"; applying it to update too would make overdue tasks impossible to complete | Thinking through the update flow | Rule deliberately *not* applied on update, documented by the test `Update_allows_past_due_date_so_overdue_tasks_can_be_completed` |
| 4 | My prompt listed only 9 task rules: no happy-path or validation tests for Update/Delete | Phase 4 needed those behaviours | Added tests for update success, update validation, and delete success before using them from the API |
| 5 | Both APIs have a `Program` class in the global namespace, so `WebApplicationFactory<Program>` is ambiguous in one test project | Flagged right after scaffolding | Marker types `AuthApiMarker` / `TasksApiMarker` |
| 6 | Exception handler, JWT setup and CORS would have been copy-pasted into both APIs | Review of the Phase 4 plan | Extracted into `Api.Common` (a deliberate deviation from my prompt, see table above) |
| 7 | Angular 22's `ng new` defaults to Vitest, but I asked for Jasmine | Checked `ng new --help` before generating | `--test-runner=karma` |
| 8 | The list template used `$any($event.target).value`, which breaks my "no `any`" rule | Reading the generated template | Replaced with a typed template reference (`#filter`) |
| 9 | The assignment asks for unit tests of the API endpoints; the first version only had HTTP-level integration tests | Comparing the result against the assignment text | Added isolated controller unit tests (`TasksControllerTests`, `AuthControllerTests`) |

### How edge cases, authentication and validation are handled

**Authentication**
- Passwords: PBKDF2-HMAC-SHA512, 210,000 iterations, random 16-byte salt, constant-time comparison. The hash never leaves the server: `AuthResponse` has only `token` and `email`, and an integration test asserts the response body contains no password data.
- Login returns the identical `"Invalid credentials"` 401 for an unknown email and a wrong password, so accounts can't be enumerated.
- JWT: issuer, audience, expiry and secret come from configuration; the secret only from user-secrets or an environment variable; a key under 32 characters is rejected. A tampered token returns 401 (integration test).
- Authorization is secure by default: a fallback policy requires a valid token on every endpoint unless it is marked `[AllowAnonymous]`.

**Authorization (the core of the user story)**
- The user id comes only from the token's `sub` claim. A `userId` sent in the request body is ignored (integration test `Create_ignores_any_user_id_in_the_body`).
- Another user's task returns **404, not 403**, for get, update and delete, so task ids can't be probed. Verified at three levels: service unit test, HTTP integration test, and the browser run with a second user.

**Validation**
- Title: required, whitespace-only rejected, trimmed, max 100 characters. Status: must be a defined enum value (also for the list filter). Due date: not in the past on create, compared by date so "today" is valid, tested with a fixed `TimeProvider`.
- Email: format check, then trimmed and lower-cased so `Demo@TaskApp.com` and `demo@taskapp.com` are the same account. The database's unique index backs this up against concurrent registrations.
- Password: at least 8 characters with a letter and a digit.
- Validation runs **before** any repository call (tests assert `VerifyNoOtherCalls`), and every failure is returned as RFC 7807 `ProblemDetails`. Unexpected errors return a 500 with no internal details.

**Edge cases in the client**
- An expired token in `localStorage` is treated as logged out; a 401 from the API logs out and redirects to login with a `returnUrl`, which only accepts in-app paths (no open redirect).
- The token is attached only to relative `/api/` URLs, never to other hosts.
- Due dates are handled as calendar dates (`yyyy-MM-dd`), so a time zone can't shift a task to the previous day.
- The list has explicit loading, error (with retry) and empty states; delete needs a second, confirming click.

### What I would not claim

- The one console entry that can appear is Chrome's own `Failed to load resource: 401` line when a wrong password is entered. That is the browser reporting the HTTP status, not an application warning.
- Strict one-test-at-a-time TDD is visible in the git history for task rules 1–7. After that I asked the assistant to move faster, and tests were written in small batches (still red first, then green).
- Known trade-offs I chose not to solve here are listed in the README: token in `localStorage`, no refresh tokens, no pagination.
