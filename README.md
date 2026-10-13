# TechMove Global Logistics Management System (GLMS)

ASP.NET Core 8 MVC system for TechMove Logistics: contract management, service requests against active
contracts, and automatic USD to ZAR conversion.

## Solution layout

| Project | Purpose |
|---|---|
| `src/GLMS.Shared` | DTOs, enums, service interfaces, `ServiceResult`, shared validation attributes |
| `src/GLMS.Core` | Domain entities, business services, GoF patterns, repository interfaces |
| `src/GLMS.Infrastructure` | EF Core `GlmsDbContext` (Fluent API), repositories, migrations, file storage, exchange-rate client, contract-expiry job |
| `src/GLMS.Web` | MVC front end (controllers + Razor views) |
| `tests/GLMS.UnitTests` | xUnit + Moq unit tests |

The monolith is layered: **controllers -> services -> repositories -> SQL Server**. Controllers never touch
`DbContext`. They depend only on the interfaces in `GLMS.Shared.Abstractions`.

## Design patterns (GoF)

| Pattern | Where | What it does |
|---|---|---|
| Factory Method | `GLMS.Core/Patterns/Factory/ContractFactory.cs` | One `ContractCreator` per contract type (Standard / Express / Premium) sets the SLA response time; every new contract starts as Draft |
| State | `GLMS.Core/Patterns/State/ContractState.cs` | `DraftState`, `ActiveState`, `OnHoldState`, `ExpiredState` decide allowed transitions and whether service requests are accepted |
| Observer | `GLMS.Core/Patterns/Observer/ContractStatusObservers.cs` | `ContractStatusPublisher` (subject) notifies `StatusHistoryObserver` (audit table) and `LoggingNotificationObserver` on every status change |

## Business rules

* **Contract statuses:** Draft, Active, Expired, On Hold (all four from the brief).
  Transitions: Draft -> Active; Active -> On Hold / Expired; On Hold -> Active / Expired; Expired is final.
* **Automated status tracking:** `ContractExpiryBackgroundService` runs at start-up and then hourly. It moves
  Active or On Hold contracts whose end date has passed to Expired, and the Observers record each change.
* **Service requests** can only be raised against an **Active** contract whose end date has not passed.
  Draft, On Hold and Expired contracts are blocked, with a specific message for each.
* **Approve / decline:** service requests go Pending -> Approved or Declined, then Approved -> Completed.
  Approving is blocked if the contract is no longer active.
* **Search:** filter contracts by date range, status, client and text. The date range returns contracts
  whose term *overlaps* the range (`EndDate >= From && StartDate <= To`); see `ContractQueryExtensions`.
* **Currency:** the create page calls ExchangeRate-API (`https://open.er-api.com/v6/latest/USD`, free, no key)
  through a typed `HttpClient` and shows a live ZAR preview. On save, the server fetches the rate again,
  calculates `CostZar` and stores the rate used. Rates are cached for 60 minutes. If the API is down, the
  last known rate is used and flagged as stale. If no rate has ever been fetched, the request is refused
  rather than saved with a wrong amount.
* **Signed agreement upload:** PDF only (extension, content type and `%PDF-` file signature are all
  checked), max 10 MB. Files are saved to `App_Data/agreements` (outside `wwwroot`, not in the DB) under a
  GUID name, and downloaded through `ContractsController.DownloadAgreement`.
* **Validation** is defined once on the DTOs (data annotations + custom `[DateAfter]`), so it runs on the
  server and in the browser (jQuery unobtrusive + `site.js` adapter). Business rules live in the services.

## Running locally

Requirements: .NET 8 SDK and SQL Server (LocalDB, Express, or Docker).

```bash
# Option A: LocalDB (default connection string in src/GLMS.Web/appsettings.json)
dotnet run --project src/GLMS.Web

# Option B: SQL Server in Docker
docker run -d --name glms-sql -e ACCEPT_EULA=Y -e "MSSQL_SA_PASSWORD=Your_Strong_Pass123" -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
ConnectionStrings__DefaultConnection="Server=localhost,1433;Database=GlmsDb;User Id=sa;Password=Your_Strong_Pass123;TrustServerCertificate=True" dotnet run --project src/GLMS.Web
```

Migrations are applied on start-up (`Database:MigrateOnStartup`), and demo data with one contract in each
status is seeded.

### Migrations

```bash
dotnet tool restore
dotnet ef migrations add <Name> --project src/GLMS.Infrastructure --startup-project src/GLMS.Web -o Data/Migrations
dotnet ef migrations script --idempotent --project src/GLMS.Infrastructure --startup-project src/GLMS.Web -o database/migrations.sql
```

The generated SQL script is committed at [`database/migrations.sql`](database/migrations.sql).

## Tests

```bash
dotnet test GLMS.sln
```

The tests cover:

* USD to ZAR maths at given rates, rounding, zero, and negative amounts or rates.
* PDF validation: `.exe` rejected, `.exe` renamed to `.pdf` rejected, null, empty, oversize, wrong content type.
* Service request workflow rules (blocked on Draft, On Hold and Expired, and once the end date has passed),
  rate-API-down handling, and approve/decline transitions.
* State transitions, the Factory SLA tiers, and Observer notification and failure isolation.
* The LINQ contract search, including an EF Core in-memory run.
* The exchange-rate client with a stubbed HTTP handler: success, caching, 500, timeout, malformed JSON and the stale fallback.
* GUID file naming and path-traversal protection in file storage.
* DTO validation, including a culture regression test (`en-ZA` decimal separator).

GitHub Actions (`.github/workflows/ci.yml`) builds and runs every test on each push and pull request.
