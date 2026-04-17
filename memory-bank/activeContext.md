# Active Context

## Current State
WebAPI startup configuration is aligned with .NET 10 and currently builds clean with the rest of the solution.

## Recent Changes (this session)
- EF Core tracking/state safety refactor was applied for write flows:
  - Removed generic repository `Update(...)` methods from portfolio/alert/asset repository contracts and EF implementations.
  - Removed tracked-load → mutate → `Update(...)` anti-pattern from command handlers (`AddTrade`, `CreatePortfolio`, `DeletePortfolio`, `DeleteAlert`).
  - Removed redundant `_alertRepository.Update(...)` calls from `AlertCheckJob`; tracked alert mutations now persist via `IUnitOfWork.SaveChangesAsync()` at the end of the cycle.
  - Removed unused `IUnitOfWork` constructor dependencies from handlers that never called it.
  - Verified there are no remaining `_portfolioRepository.Update(...)`, `_alertRepository.Update(...)`, `_assetRepository.Update(...)` usages and solution builds successfully.
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
- Additional job-level performance hardening was applied after cross-checking similar patterns:
  - Added `IAssetRepository.GetByIdsAsync(...)` for batch lookup by asset id
  - Refactored `AlertCheckJob` to replace per-id asset fetch loop with single batched DB call
  - Refactored `MarketDataRefreshJob` SignalR publish loop to parallel `Task.WhenAll` broadcasting
  - Verified solution builds successfully after these refactors (`dotnet build Quantira.sln`)
- Further scalability hardening was applied for the remaining roadmap items:
  - `AssetCatalogueUpdateJob` now normalizes provider symbols and performs chunked symbol matching (`SymbolLookupChunkSize`) instead of loading all existing symbols per type
  - `AssetCatalogueUpdateJob` inserts are chunked (`InsertChunkSize`) with change tracker clearing between batches to reduce memory pressure on large catalog updates
  - `NewsIngestionJob` now uses bounded concurrency (`SemaphoreSlim`, `MaxParallelism`) with per-symbol fault isolation and cycle-level success/failure metrics
  - `NewsIngestionJob` signature now propagates `CancellationToken` through repository/cache operations
  - Full solution build re-verified after these updates (`dotnet build Quantira.sln`)
- Market data resilience hardening was added for provider throttling/auth issues:
  - `MarketDataService.GetBatchLatestAsync(...)` now applies provider-scoped in-memory 429 circuit breaker logic
  - Threshold: 3 consecutive `429 TooManyRequests`; cooldown: 5 minutes per provider
  - During cooldown, provider calls are skipped with warning logs to avoid unnecessary external pressure
  - `YahooFinanceProvider` now rethrows `HttpRequestException` on 429 so circuit breaker can observe failures
  - `GoldApiProvider` now logs explicit diagnostics for `403 Forbidden` (key/quota/entitlement hints)
  - Hangfire `market-data-refresh` cron interval was relaxed from every 15 seconds to every 1 minute
  - Full solution build re-verified after these updates (`dotnet build Quantira.sln`)

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
- Add load/perf tests for Hangfire cycles (`MarketDataRefreshJob`, `AlertCheckJob`) under higher symbol/alert counts
- Add load/perf tests for `AssetCatalogueUpdateJob` chunk thresholds and tune `SymbolLookupChunkSize`/`InsertChunkSize` with production-like data
