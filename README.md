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

To override the dev passwords and JWT key, copy `.env.example` to `.env` (see [Configuration](#configuration)).

## Tech stack

| Area | Technology | Version | Used for |
|---|---|---|---|
| Runtime / language | .NET, C# | 8.0 (LTS), C# 12 | All projects |
| Front end | ASP.NET Core MVC + Razor views | 8.0 | `GLMS.Web` |
| UI | Bootstrap, jQuery, jQuery Validation (+ Unobtrusive) | 5.1, 3.6, 1.19 | Layout, client-side validation |
| Back end | ASP.NET Core Web API (controllers) | 8.0 | `GLMS.Api` |
| API docs | Swashbuckle (Swagger / OpenAPI) | 6.9.0 | `/swagger` |
| Authentication | JWT bearer (`Microsoft.AspNetCore.Authentication.JwtBearer`), cookie auth in the MVC app | 8.0.31 | Login, securing every API call |
| Password hashing | ASP.NET Core Identity `PasswordHasher` (PBKDF2) | 8.0.31 | `Users` table |
| ORM | Entity Framework Core (SQL Server provider, Fluent API, migrations) | 8.0.31 | `GLMS.Infrastructure` |
| Database | Microsoft SQL Server | 2022 (Docker) / LocalDB | Data store |
| External API | ExchangeRate-API open access endpoint (`open.er-api.com`) | v6 | USD to ZAR rates |
| HTTP clients | `IHttpClientFactory` typed clients, `System.Text.Json` | 8.0 | Rate API calls; MVC to API calls |
| Caching | `IMemoryCache` | 8.0 | Exchange-rate caching |
| Background work | `BackgroundService` + `PeriodicTimer` | 8.0 | Hourly contract expiry |
| Health checks | ASP.NET Core health checks + EF Core DbContext check | 8.0.31 | `/health` |
| Unit tests | xUnit, Moq, EF Core InMemory | 2.9.3, 4.20.72, 8.0.31 | `GLMS.UnitTests` |
| Integration tests | `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory), SQLite in-memory | 8.0.31 | `GLMS.IntegrationTests` |
| Coverage | coverlet | 6.0.4 | `dotnet test --collect:"XPlat Code Coverage"` |
| Containers | Docker (multi-stage builds), Docker Compose | Compose v2 | 3-container deployment |
| Base images | `mcr.microsoft.com/dotnet/sdk:8.0`, `aspnet:8.0`, `mssql/server:2022-latest` | | Build / runtime / database |
| CI | GitHub Actions (`ubuntu-latest`) | | Build, test, compose test |
| Tooling | `dotnet-ef` (local tool in `.config/dotnet-tools.json`) | 8.0.31 | Migrations and SQL script |

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

## Configuration

### Prerequisites

| Scenario | You need |
|---|---|
| Run everything in Docker | Docker Desktop (with Compose v2). Nothing else. |
| Run / debug locally | .NET 8 SDK, plus a SQL Server: LocalDB (installed with Visual Studio's "Data storage" workload), SQL Server Express/Developer, or SQL Server in Docker |
| Edit migrations | `dotnet tool restore` (installs the pinned `dotnet-ef` 8.0.31) |

NuGet packages come from nuget.org through the repo's `nuget.config`, so restores work even on machines
with no package sources configured globally.

### How configuration is loaded

Both apps use standard ASP.NET Core configuration. Later sources override earlier ones:

1. `appsettings.json`: safe defaults, committed, **no secrets**
2. `appsettings.{Environment}.json`: `Development` adds local-only dev values (JWT key, admin seed)
3. Environment variables: nested keys use a double underscore, e.g. `Jwt:SigningKey` -> `Jwt__SigningKey`
4. Command-line arguments, e.g. `--ConnectionStrings:DefaultConnection="..."`

Docker Compose sets everything through environment variables (layer 3). The values come from a `.env`
file next to `docker-compose.yml` if you create one, otherwise from the dev defaults in the compose file.

### glms-backend-api settings (`src/GLMS.Api`)

| Key | Default | Description |
|---|---|---|
| `ConnectionStrings:DefaultConnection` | LocalDB `GlmsDb` | SQL Server connection string. **The API is the only app with one.** |
| `Database:MigrateOnStartup` | `true` | Apply EF Core migrations and seed demo data on start-up (retries up to 10 times while SQL Server starts) |
| `Jwt:SigningKey` | *(empty)*; dev value in `appsettings.Development.json` | HMAC key for signing tokens, **at least 32 characters**. Required: if it is missing or too short, login and every secured endpoint fail with a 500 error explaining the key is not configured. |
| `Jwt:Issuer` / `Jwt:Audience` | `glms-backend-api` / `glms-frontend-web` | Values written to and validated in each token |
| `Jwt:LifetimeMinutes` | `60` | Token lifetime (the web sign-in lasts the same time) |
| `Auth:SeedAdmin:Username` / `Password` | *(empty)*; `admin` / `Admin@12345` in Development | Creates this admin user on start-up if it does not exist. Leave empty to skip. |
| `Swagger:Enabled` | `true` | Serve `/swagger` |
| `ExchangeRateApi:BaseUrl` | `https://open.er-api.com/v6/` | Exchange-rate provider (free, no API key) |
| `ExchangeRateApi:CacheMinutes` | `60` | How long a fetched rate is reused |
| `ExchangeRateApi:TimeoutSeconds` | `10` | HTTP timeout for the rate API |
| `FileStorage:RootPath` | `App_Data/agreements` | Folder for signed-agreement PDFs (relative paths resolve from the app folder; Docker uses a volume) |
| `ContractExpiry:Enabled` | `true` | Run the background job that marks overdue contracts Expired |
| `ContractExpiry:IntervalMinutes` | `60` | How often the expiry job runs |

### glms-frontend-web settings (`src/GLMS.Web`)

| Key | Default | Description |
|---|---|---|
| `GlmsApi:BaseUrl` | `http://localhost:5147/` | Where the API is. In Compose: `http://glms-backend-api:8080/` |
| `GlmsApi:TimeoutSeconds` | `15` | After this, the user sees the "service unavailable" page |
| `UseHttpsRedirection` | `true` | Set `false` when served over plain HTTP (as in Docker) |
| `DataProtection:KeysPath` | *(empty)*; `/app/keys` in Docker | Where cookie-encryption keys are stored, so sign-ins survive restarts |

The web app has **no connection string**. It only knows the API's URL.

### Docker Compose variables (`.env`)

Copy `.env.example` to `.env` (git-ignored) and change the values:

| Variable | Dev default | Used for |
|---|---|---|
| `SA_PASSWORD` | `Glms_Dev_Pass123` | SQL Server `sa` password (must meet SQL Server complexity rules) and the API's connection string |
| `JWT_SIGNING_KEY` | `compose-dev-signing-key-change-me-0123456789` | `Jwt__SigningKey` for the API |
| `GLMS_ADMIN_USER` | `admin` | Seeded admin username |
| `GLMS_ADMIN_PASSWORD` | `Admin@12345` | Seeded admin password |

Ports: web `8080`, API `8081` (Swagger). SQL Server is **not** published to the host; it is reachable
only on the internal `glms-backend` network. Data lives in named volumes: `sql-data` for the database,
`agreements` for uploaded PDFs and `web-keys` for cookie keys.

```bash
docker compose up --build -d     # build and start
docker compose ps                # status (sql-server-db should show "healthy")
docker compose logs -f glms-backend-api
docker compose down              # stop, keeping data
docker compose down -v           # stop and delete the database, uploads and keys
```

If you change `SA_PASSWORD` after the first run, also run `docker compose down -v`. SQL Server only sets
the password when it creates its data volume.

### Running locally without Docker

1. **Start a database.** LocalDB works with the default connection string. Or run SQL Server in Docker
   with a published port:

   ```bash
   docker run -d --name glms-sql -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=Your_Strong_Pass123" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
   ```

2. **Run the API.** It migrates the database and seeds the demo data and the `admin` user on first start:

   ```bash
   # LocalDB:
   dotnet run --project src/GLMS.Api --launch-profile http
   # SQL Server in Docker (PowerShell syntax for the environment variable):
   $env:ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=GlmsDb;User Id=sa;Password=Your_Strong_Pass123;TrustServerCertificate=True"
   dotnet run --project src/GLMS.Api --launch-profile http
   ```

   Swagger: http://localhost:5147/swagger

3. **Run the web app** in a second terminal: `dotnet run --project src/GLMS.Web --launch-profile http`,
   then open http://localhost:5217 and sign in as `admin` / `Admin@12345`.

In **Visual Studio**, open `GLMS.sln`, right-click the solution, choose **Configure Startup Projects ->
Multiple startup projects**, and set `GLMS.Api` and `GLMS.Web` to *Start*.

Both apps run with `ASPNETCORE_ENVIRONMENT=Development` from their launch profiles, which is what
supplies the dev JWT key and admin seed. Anywhere else, set `Jwt__SigningKey` and the `Auth__SeedAdmin__*`
values yourself.

### Production checklist

* Set a long random `Jwt__SigningKey` and strong `SA_PASSWORD` / admin password. Never use the dev defaults.
* Use a dedicated SQL login instead of `sa`.
* Put the web app behind HTTPS (a reverse proxy or load balancer) and leave `UseHttpsRedirection` on.
* Consider `Swagger__Enabled=false` if the API should not be browsable.

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

## Testing with Postman

The [`postman/`](postman/) folder holds a Postman collection in the v3 YAML format (one `.request.yaml` file
per request) and two environments:

| Environment | Base URL | Use when |
|---|---|---|
| `GLMS - Docker` | http://localhost:8081 | the API is running under `docker compose up` |
| `GLMS - Local` | http://localhost:5147 | the API is running with `dotnet run --project src/GLMS.Api` |

**In the Postman app:** open the `postman/` folder in Local Mode (or import `postman/GLMS API` and the two
`*.environment.yaml` files), select an environment, then send **Auth > Login** or use **Run collection**.
Login stores the JWT in `{{token}}` and every other request sends it as a Bearer token. Create requests
store the new `clientId`, `contractId` and `serviceRequestId`, so the folders run in order as one workflow:

* **Auth:** login, wrong password (401), no token (401).
* **Clients:** create, list, get, update; invalid input (400), unknown id (404).
* **Contracts:** create as Draft, get, update, activate, search with filters; end before start (400),
  Draft to Expired (400), service request on a Draft contract (400), `to` before `from` (400).
* **Exchange rates:** current USD to ZAR rate.
* **Service requests:** create (checks ZAR = USD x rate), list by contract, get, approve, complete;
  completing a Pending request (400).
* **Signed agreement:** upload a PDF, then download it. Choose a PDF for the `file` field first.

**From the command line** (Newman cannot run v3 collections, so use the Postman CLI):

```bash
npx postman-cli collection run "postman/GLMS API" -e "postman/GLMS - Docker.environment.yaml" \
  -i Auth -i Clients -i Contracts -i "Exchange rates" -i "Service requests"
```

The `-i` flags skip the signed-agreement folder, which needs a file chosen in the app. Each run adds a
client, contract and service request to the database.
