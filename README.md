# TechMove Global Logistics Management System (GLMS)

ASP.NET Core 8 system for TechMove Logistics: contract management, service requests against active
contracts, and automatic USD to ZAR conversion. It runs as a service-oriented, containerised application.

```
browser ──:8080──> glms-frontend-web ──HTTP/JSON + JWT──> glms-backend-api ──EF Core──> sql-server-db
                   (MVC, no DB access)                    (Web API, Swagger)            (SQL Server 2022)
```

> Git history: tag `part2` is the layered MVC monolith (Part 2). `main` / tag `part3` is the
> API + MVC client split with Docker (Part 3).

## Quick start (Docker)

```bash
docker compose up --build
```

| URL | What |
|---|---|
| http://localhost:8080 | Web UI. Sign in as `admin` / `Admin@12345` (dev default) |
| http://localhost:8081/swagger | API docs. Use `POST /api/auth/login`, then **Authorize** with the token |
| http://localhost:8081/health | API + database health |

To override the dev passwords and JWT key, copy `.env.example` to `.env`.

## Solution layout

| Project | Purpose |
|---|---|
| `src/GLMS.Shared` | DTOs, enums, service interfaces, `ServiceResult`, shared validation attributes |
| `src/GLMS.Core` | Domain entities, business services, GoF patterns, repository interfaces, auth service |
| `src/GLMS.Infrastructure` | EF Core `GlmsDbContext` (Fluent API), repositories, migrations, file storage, exchange-rate client, contract-expiry job |
| `src/GLMS.Api` | ASP.NET Core Web API (JSON, Swagger, JWT). **The only component that talks to the database** |
| `src/GLMS.Web` | MVC front end. References only `GLMS.Shared` and calls the API through typed `HttpClient`s |
| `tests/GLMS.UnitTests` | xUnit + Moq unit tests (business rules, patterns, API clients, architecture rule) |
| `tests/GLMS.IntegrationTests` | Calls the real API endpoints and asserts on the responses |

### Architecture

* **API:** controllers -> services (`GLMS.Core`) -> repositories (`GLMS.Infrastructure`) -> SQL Server,
  all wired with dependency injection. Controllers contain no business logic. They map `ServiceResult`
  to `200/201/400/404/503` responses (`ServiceResultExtensions`).
* **Front end:** the MVC controllers are unchanged from Part 2. They still depend on `IContractService`,
  `IServiceRequestService` and the other interfaces, but these are now implemented by `ContractApiClient`
  and friends (`src/GLMS.Web/ApiClients`). Failed API calls are handled centrally by `ApiExceptionFilter`:
  an unreachable API or a 5xx shows a friendly 503 page, and an expired token signs the user out and
  redirects to login. API validation errors (400) come back as inline form errors.
* `ArchitectureTests.WebFrontEnd_HasNoDatabaseOrBackEndDependencies` fails the build if `GLMS.Web` ever
  references EF Core, SqlClient, `GLMS.Core` or `GLMS.Infrastructure`.

### Authentication

`POST /api/auth/login` checks the credentials against the `Users` table (PBKDF2 hashes via ASP.NET Core
Identity's `PasswordHasher`) and returns a signed JWT (HMAC-SHA256, 60 minutes). Every other endpoint
requires the token. The web app stores the JWT inside its encrypted, HttpOnly auth cookie, and
`BearerTokenHandler` attaches it to each API call. The first admin user is seeded from configuration
(`Auth:SeedAdmin`).

## Design patterns (GoF)

| Pattern | Where | What it does |
|---|---|---|
| Factory Method | `GLMS.Core/Patterns/Factory/ContractFactory.cs` | One `ContractCreator` per contract type (Standard / Express / Premium) sets the SLA response time; every new contract starts as Draft |
| State | `GLMS.Core/Patterns/State/ContractState.cs` | `DraftState`, `ActiveState`, `OnHoldState`, `ExpiredState` decide allowed transitions and whether service requests are accepted |
| Observer | `GLMS.Core/Patterns/Observer/ContractStatusObservers.cs` | `ContractStatusPublisher` (subject) notifies `StatusHistoryObserver` (audit table) and `LoggingNotificationObserver` on every status change |

Repository and Service Layer are used as well, but they are architectural patterns rather than GoF patterns.

## API endpoints

| Method | Route | Success | Errors |
|---|---|---|---|
| POST | `/api/auth/login` | 200 token | 400, 401 |
| GET | `/api/contracts?from=&to=&status=&clientId=&search=` | 200 | 400 (to < from), 401 |
| GET | `/api/contracts/{id}` | 200 | 404 |
| POST | `/api/contracts` | 201 + `Location` | 400 |
| PUT | `/api/contracts/{id}` | 200 | 400, 404 |
| PATCH | `/api/contracts/{id}/status` | 200 | 400 (illegal transition), 404 |
| POST | `/api/contracts/{id}/agreement` (multipart, PDF) | 200 | 400 (not a PDF), 404 |
| GET | `/api/contracts/{id}/agreement` | 200 `application/pdf` | 404 |
| GET | `/api/service-requests?contractId=` | 200 | |
| GET | `/api/service-requests/{id}` | 200 | 404 |
| POST | `/api/service-requests` | 201 + `Location` | 400 (contract not active), 503 (no exchange rate) |
| PATCH | `/api/service-requests/{id}/status` | 200 | 400 (illegal transition), 404 |
| GET/POST/PUT | `/api/clients`, `/api/clients/{id}` | 200 / 201 | 400, 404 |
| GET | `/api/exchange-rates/usd-zar` | 200 | 503 |
| GET | `/health` | 200 `Healthy` | 503 |

## Business rules and interpretations of the brief

* **Contract statuses:** Draft, Active, Expired, On Hold. The Part 2 entity spec lists three, but the
  workflow rule needs On Hold, so all four are implemented. Transitions: Draft -> Active;
  Active -> On Hold / Expired; On Hold -> Active / Expired; Expired is final.
* **Automated status tracking:** `ContractExpiryBackgroundService` runs at start-up and then hourly. It moves
  Active or On Hold contracts whose end date has passed to Expired, and the Observers record each change.
* **Service requests** can only be raised against an **Active** contract whose end date has not passed.
  Draft, On Hold and Expired contracts are blocked, with a specific message for each.
* **"Approve / decline" PATCH:** interpreted as the service-request workflow
  (`PATCH /api/service-requests/{id}/status`): Pending -> Approved or Declined, then Approved -> Completed.
  Approving is blocked if the contract is no longer active. Contract status changes use
  `PATCH /api/contracts/{id}/status`.
* **Search:** the date range returns contracts whose term *overlaps* the range
  (`EndDate >= From && StartDate <= To`); see `ContractQueryExtensions`.
* **Currency:** ExchangeRate-API (`https://open.er-api.com/v6/latest/USD`, free, no key) is called through a
  typed `HttpClient` with async/await throughout. Rates are cached for 60 minutes. If the API is down, the
  last known rate is used and flagged as stale. If no rate has ever been fetched, the request gets a 503
  and is not saved with a wrong amount. The ZAR amount is always recalculated on the server, and the rate
  used is stored with the request.
* **Signed agreement:** PDF only (extension, content type and `%PDF-` file signature are all checked),
  max 10 MB. Files are stored on disk (`App_Data/agreements`, a Docker volume in Compose) under a GUID
  name, never in the database.
* **Validation** is defined once on the shared DTOs (data annotations + custom `[DateAfter]`). It runs in
  the browser (jQuery unobtrusive + adapter in `site.js`), in MVC model binding, and again in the API.
  Business rules live in the services, not the controllers.
* **Integration tests "call the running API":** by default the suite hosts the real API in-process with
  `WebApplicationFactory` (SQLite in-memory, exchange rate stubbed). Set `GLMS_API_URL` and the same
  tests run over the network against the containerised API. CI does both (see below).
* **Naming:** containers are named as in the brief: `sql-server-db`, `glms-backend-api`, `glms-frontend-web`.

## Docker

* Both Dockerfiles are **multi-stage** (restore -> publish -> runtime). The restore layer copies only
  `.csproj` files, so package restore is cached until dependencies change. The runtime image is
  `aspnet:8.0` (no SDK) running as the non-root `app` user.
* `docker-compose.yml` passes all configuration through **environment variables** (connection string,
  JWT key, admin seed, API base URL). Containers reach each other by service name.
* Two networks: `glms-backend` (`internal: true`, API + SQL only) and `glms-frontend` (API + web). The web
  container is not on the backend network, so it cannot reach SQL Server at all.
* SQL Server has a health check, and the API waits for it (`depends_on: service_healthy`), then
  migrates and seeds with retries.

## Running without Docker

Requires the .NET 8 SDK and a SQL Server (LocalDB by default).

```bash
dotnet run --project src/GLMS.Api   # http://localhost:5147/swagger (Development: admin / Admin@12345)
dotnet run --project src/GLMS.Web   # calls the API at GlmsApi:BaseUrl
```

### Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/GLMS.Infrastructure --startup-project src/GLMS.Api -o Data/Migrations
dotnet ef migrations script --idempotent --project src/GLMS.Infrastructure --startup-project src/GLMS.Api -o database/migrations.sql
```

The generated SQL script is committed at [`database/migrations.sql`](database/migrations.sql).

## Tests

```bash
dotnet test GLMS.sln                                                         # unit + in-process integration
GLMS_API_URL=http://localhost:8081/ dotnet test tests/GLMS.IntegrationTests  # against docker compose
```

* **Unit tests:**
  * USD to ZAR maths, rounding, zero and negative values.
  * PDF validation: `.exe`, a renamed `.exe`, null, empty and oversize files.
  * Service request rules (blocked on Draft, On Hold, Expired and after the end date) and rate-API-down handling.
  * State transitions, Factory SLA tiers, and Observer notification and failure isolation.
  * The LINQ search.
  * The exchange-rate client with a stubbed HTTP handler (success, cache, 500, timeout, bad JSON, stale fallback).
  * GUID file naming and path-traversal protection.
  * DTO validation, including an `en-ZA` culture regression test.
  * The front-end API clients (400/404/401/503/network failure) and the architecture rule.
* **Integration tests:**
  * `GET /api/contracts` returns 200 with JSON.
  * 401 without a token.
  * Filters by status and date range.
  * Create-then-read for contracts, clients and service requests (201 + `Location`, then 200).
  * 400 for invalid input and illegal transitions.
  * 404 for unknown ids.
  * PDF upload, then download returns the same bytes; `.exe` upload gets 400.
  * 503 when no exchange rate is available.
  * Login success and failure, `/health`, and the Swagger document.

GitHub Actions (`.github/workflows/ci.yml`) runs all tests on every push and pull request. A second job
builds both images, starts the Compose stack, and runs the integration tests against the containers.
