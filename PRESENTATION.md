# TaskApp — Presentation and Thought Process

This document explains how I approached the exercise: the user story, how I broke the work down, the decisions I made and why, and how I used and checked a GenAI tool.

| If you want to… | Read |
|---|---|
| Run the app and the tests | [README.md](README.md) |
| See the layers and why dependencies point inward | [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) |
| See the GenAI prompt, sample output, and what I validated and corrected | [docs/GENAI.md](docs/GENAI.md) |

## 1. User story

> As a registered user, I want to log in and create, view, update, and delete my own tasks so I can track what is due.
> I must never see or change another user's tasks.

I chose a task manager because it matches the GenAI part of the exercise (tasks with title, description, status, due date, owned by a user), so one codebase answers both parts. The second sentence of the story is deliberate: "my own tasks" turns simple CRUD into a real authorization problem, which is where most of the interesting design and testing went.

## 2. How the requirements map to the solution

| Requirement | Where |
|---|---|
| Data store with a data table and a users table | SQLite via EF Core: `Tasks` and `Users` (`src/Infrastructure/Persistence`) |
| Web API with CRUD | `src/Api.Tasks` — `TasksController` |
| Second API: create user, login, authorized and non-authorized endpoints | `src/Api.Auth` — `register`, `login`, `me` (authorized), `ping` (anonymous) |
| Data access layer | `src/Infrastructure` — EF repositories behind interfaces |
| Business logic independent of data and API | `src/Application` — references only `src/Domain` |
| Unit tests for data, business and API layers | `tests/` — 94 tests |
| Responsive frontend with CRUD | `client/` — Angular 22 |
| README and seeded demo credentials | [README.md](README.md) — `demo@taskapp.com` / `Demo@1234` |
| GenAI prompt, output, validation | [docs/GENAI.md](docs/GENAI.md) |

## 3. How I worked

I split the work into phases, from the inside of the architecture outwards, so each layer was finished and tested before anything depended on it:

1. **Domain and task rules** — entities, then `TaskService`, one rule at a time, test first.
2. **Auth rules** — `AuthService`: registration, password policy, login.
3. **Infrastructure** — EF Core, repositories, password hashing, JWT generation, migration, seed data.
4. **APIs** — thin controllers, error handling, authentication, integration tests.
5. **Angular client** — auth, task list, task form, specs.
6. **Documentation.**

**TDD.** For the business rules I followed red → green → refactor strictly: write one failing test, watch it fail for the right reason, write the minimum code, then tidy. The first seven task rules are visible as separate `test:` / `feat:` commits in the git history. Later phases were also test-first, but in small batches rather than one test at a time, and are committed per phase.

The value showed up early: the minimal code for "you can't get another user's task" crashed with a `NullReferenceException` when the task didn't exist at all. The very next test caught it before any API existed.

## 4. Key design decisions

**Architecture**
- **Clean Architecture with the compiler as the enforcer.** `Application` references only `Domain`. It can't use EF Core, ASP.NET or JWT libraries because the project reference isn't there. It declares interfaces (`ITaskRepository`, `IPasswordHasher`, `ITokenGenerator`) and `Infrastructure` implements them.
- **Two APIs, one database.** Auth issues tokens; Tasks only validates them. Both read the same JWT settings and the same SQLite file, and the integration tests run both together to prove a token from one is accepted by the other.
- **Thin controllers.** They translate HTTP to a service call and nothing else. One global exception handler maps application exceptions to 400/401/404/409 as `ProblemDetails`.

**Security**
- **The user id comes only from the token**, never from the request body or URL.
- **404, not 403, for another user's task**, so task ids can't be probed.
- **One generic "Invalid credentials"** for an unknown email and a wrong password, so accounts can't be enumerated.
- **Authenticated by default.** Every endpoint requires a token unless it explicitly opts out.
- **Passwords** are hashed with salted PBKDF2 and compared in constant time. The hash is never returned.
- **No secrets in the repo.** The JWT signing key comes from user-secrets or an environment variable, and the API refuses to start without it.

**Testing**
- **Fixed clock.** `TimeProvider` is injected, so "due date can't be in the past" is tested without depending on today's date.
- **Real SQLite in data tests**, not EF's in-memory provider, so unique indexes and SQL behave as in production.
- **Three levels for the core rule.** "User B cannot access user A's task" is tested in the service, over HTTP, and in a real browser run.

**Frontend**
- Angular standalone components, lazy-loaded routes, typed models with no `any`.
- Signals for auth state; an interceptor adds the token and handles expired sessions; guards are for user experience only, because the API enforces every rule itself.
- Explicit loading, error and empty states; delete needs a confirming second click; layout works down to phone width.

## 5. How I used GenAI

I used Claude Code as a pair programmer, driven by one structured prompt that fixed the architecture, the business rules and the working method (TDD, small steps, explain every choice).

I treated its output as **a draft until something independent proved it**: a failing test first, the real APIs running, or the app in a browser. That found nine concrete problems. For example, `GET /api/tasks?status=99` silently returned an empty list until I added a failing test and validation.

The prompt, sample output, every problem with its fix, and where I deviated from my own prompt are in [docs/GENAI.md](docs/GENAI.md).

## 6. Results

- **94 .NET tests** (36 business rules, 23 data layer, 35 API) and **20 Angular specs**, all passing; builds with 0 warnings.
- End-to-end browser run of login, filter, create, edit, delete, logout and a second user's isolation: no console warnings or errors, no horizontal scrolling at phone width.

## 7. What I would do with more time

- Move the token from `localStorage` to an httpOnly cookie, and add refresh tokens.
- Pagination and sorting on the task list.
- Rate limiting on login and register.
- A CI pipeline running both test suites on every push.
- Swap SQLite for a server database; only `Infrastructure` would change.

## 8. Demo walkthrough (for the live presentation)

1. Log in as `demo@taskapp.com` — four seeded tasks in mixed statuses.
2. Filter by status; create a task (show the validation messages); edit it; delete it with confirmation.
3. Log out, register a second user — an empty list, proving isolation.
4. Show the same rule in code: `TaskService.GetOwnedTaskAsync` and the test `User_B_cannot_see_change_or_delete_user_As_task`.
5. Show `Application.csproj` — a single reference, to `Domain`.
6. Run `dotnet test`.
