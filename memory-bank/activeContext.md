# Active Context

## Current State
WebAPI startup configuration is aligned with .NET 10 and currently builds clean with the rest of the solution.

## Recent Changes (this session)
- WebAPI `Program.cs` was standardized to .NET 10 native OpenAPI + Scalar (`AddOpenApi`, `MapOpenApi`, `MapScalarApiReference`), without Swashbuckle calls.
- `Quantira.WebAPI.csproj` now includes explicit API surface dependencies used in startup:
  - `Microsoft.AspNetCore.Authentication.JwtBearer`
  - `Microsoft.AspNetCore.OpenApi`
  - `Scalar.AspNetCore`
  - `AspNetCore.HealthChecks.SqlServer`
  - `AspNetCore.HealthChecks.Redis`
- Health checks are configured with named SQL/Redis registrations and tags in `Program.cs`.
- Remaining Turkish XML summary/comment text in `Program.cs` was converted to English.
- Asset catalogue flow was refactored to provider-based architecture:
  - Added `IAssetProvider` contract in `Infrastructure/Assets`
  - Added `BinanceAssetProvider` and `BistAssetProvider` under `Infrastructure/Assets/Providers`
  - Replaced split job methods with unified `AssetCatalogueUpdateJob.RunAllUpdatesAsync`
  - Added retry-on-network-failure (3 attempts, exponential backoff)
  - Added detailed symbol-level logs for inserts/updates in Hangfire traces
- Added lightweight update skeleton for existing assets (`Name`, `DataProviderKey`) via `Asset.UpdateCatalogueMetadata(...)`.
- Updated DI/Hangfire registration to use provider collection + single recurring job id `asset-catalogue-update`.
- Removed banned `Polly.Extensions.Http` package reference from `Quantira.Infrastructure.csproj`.
- Folder organization cleanup: `Jobs/` now contains only scheduled jobs; asset providers moved out to `Assets/` for cleaner separation of concerns.
- `AssetCatalogueUpdateJob` was hardened as provider-orchestrator:
  - Iterates all `IAssetProvider` instances in a single `RunAllUpdatesAsync` flow
  - Per-provider `try/catch` isolation ensures one failed provider does not block others
  - Fetches provider assets, computes symbol diff against DB, inserts only missing symbols
  - Uses `IDbContextTransaction` + `AddRangeAsync` for batch insert consistency
  - Adds detailed `Information` + `Error` logs for fetch/diff/insert lifecycle
- Added `GlobalStockProviderOptions` + `GlobalStockAssetProvider` (Finnhub-style REST skeleton):
  - Config-driven (`AssetProviders:GlobalStocks`) with API key from appsettings
  - Multi-exchange fetch loop (`Exchanges`) for global stock catalogue ingestion
  - Graceful no-op behavior when disabled or API key is missing
- Infrastructure DI updated:
  - Added `IAssetProvider` registration for `GlobalStockAssetProvider`
  - Added named HttpClient `"GlobalStocks"` with resilience handler
  - Bound `GlobalStockProviderOptions` from configuration
- WebAPI appsettings updated with `AssetProviders:GlobalStocks` section (base URL, API key placeholder, exchange list).
- Market data batch flow was hardened for performance and DbContext safety:
  - Added `IAssetRepository.GetBySymbolsAsync(...)` contract for single-query symbol batch reads
  - Implemented repository-level symbol normalization (`Trim().ToUpperInvariant()`) + `IN` query in `AssetRepository`
  - Refactored `MarketDataService.GetBatchLatestAsync(...)` to remove N+1 symbol lookups
  - Provider calls now run in parallel only after DB phase completes
  - Added provider-group `try/catch` isolation so one failed provider does not fail the entire batch
  - Cache writes for batch results are normalized (`price:{SYMBOL}`) and awaited in parallel
- Full solution build completed successfully after refactor (`dotnet build Quantira.sln`).

## Active Decisions
- `ICacheService` is the only Redis abstraction visible to Application layer
- `Infrastructure.AI` stays isolated — no shared types with `Infrastructure`
- Controllers call only `IMediator.Send()` — no direct service injection

## Next Logical Steps
- Verify `IUnitOfWork` is correctly registered and scoped in DI
- Add integration tests for the Redis cache and key pipeline behaviors
- Confirm frontend `client/` API base URL is aligned with WebAPI launch settings
- Implement real BIST source adapter (MKK/KAP CSV or licensed feed) behind `BistAssetProvider`
- Add provider integration tests for `AssetCatalogueUpdateJob` diff/transaction behavior and provider-failure isolation
