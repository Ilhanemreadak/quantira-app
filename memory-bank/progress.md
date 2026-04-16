# Progress

## Done
### Infrastructure
- [x] Solution structure — 5 backend + 3 test projects wired
- [x] All projects targeting `net10.0`, build clean (0 errors, 0 warnings)
- [x] Package conflicts resolved — no `StrongName` assemblies in dependency graph
- [x] `.clinerules/` configured with architecture rules, file map, coding style

### Domain
- [x] Entities: `Portfolio`, `Asset`, `Trade`, `Alert`
- [x] Value objects: `Money`, `PnL`, `Currency`, `DateRange`
- [x] Enums: `AssetType`, `TradeType`, `CostMethod`, `AlertType`, `MarketStatus`
- [x] Domain events: `PortfolioCreated`, `TradeAdded`, `AlertTriggered`, `AssetPriceUpdated`
- [x] Repository interfaces: `IPortfolioRepository`, `IAssetRepository`, `IPositionRepository`, `ITradeRepository`, `IAlertRepository`
- [x] `IUnitOfWork`, `DomainException`, `NotFoundException`

### Application
- [x] Pipeline behaviors: Logging, Validation, Caching, Transaction
- [x] `ICacheService`, `IAIService`, `IMarketDataService`, `INotificationService`, `IIndicatorEngine`
- [x] Commands + handlers: CreatePortfolio, AddTrade, DeletePortfolio, CreateAsset, CreateAlert, DeleteAlert, SendMessage
- [x] Queries + handlers: GetPortfolioList, GetPortfolioSummary, GetTradeHistory, GetAssetBySymbol, GetUserAlerts, GetPriceHistory, CalculateIndicator, GetChatHistory
- [x] DTOs for all features

### Infrastructure
- [x] `QuantiraDbContext` + EF Fluent configs for all entities
- [x] Repository implementations for all 5 aggregates
- [x] `RedisCacheService` + `RedisCacheOptions` (complete, all interface members implemented)
- [x] Hangfire jobs: `MarketDataRefreshJob`, `AlertCheckJob`, `NewsIngestionJob`
- [x] Market data providers: `BinanceProvider`, `YahooFinanceProvider`, `GoldApiProvider`
- [x] `EmailNotificationService`
- [x] `DependencyInjection.cs` — all services registered
- [x] Asset catalogue providers moved to `Infrastructure/Assets` (`IAssetProvider`, `Providers/*`)
- [x] `Jobs` folder cleaned to scheduled job classes only (separation of concerns)
- [x] `AssetCatalogueUpdateJob` refactored to provider-loop orchestration with provider-failure isolation
- [x] Asset catalogue job now performs insert-only symbol diff + transactional `AddRangeAsync`
- [x] `GlobalStockAssetProvider` added with config-driven API key skeleton (Finnhub-style REST)
- [x] DI extended with `GlobalStockProviderOptions` binding and named HttpClient `"GlobalStocks"`
- [x] `IAssetRepository.GetBySymbolsAsync(...)` added for single-query symbol batch reads
- [x] `MarketDataService.GetBatchLatestAsync(...)` refactored to remove N+1 lookups and run provider calls in parallel safely
- [x] Provider-level fault isolation added in market data batch flow (`try/catch` per provider group)
- [x] `IAssetRepository.GetByIdsAsync(...)` added for batched asset-id lookups
- [x] `AlertCheckJob` asset resolution refactored from per-id loop to single batched repository call
- [x] `MarketDataRefreshJob` SignalR broadcasting refactored to parallel `Task.WhenAll` publish
- [x] `AssetCatalogueUpdateJob` upgraded with symbol normalization + chunked existing-symbol lookup strategy
- [x] `AssetCatalogueUpdateJob` insert pipeline chunked with `ChangeTracker.Clear()` between batches for lower memory pressure
- [x] `NewsIngestionJob` upgraded with bounded concurrency (`SemaphoreSlim`) + per-symbol fault isolation + cancellation propagation

### Infrastructure.AI
- [x] `ClaudeAIService` — streaming + non-streaming, CA2024 fixed
- [x] `PortfolioContextBuilder`
- [x] `DependencyInjection.cs`

### WebAPI
- [x] Controllers: Portfolios, Assets, Trades, MarketData, Auth
- [x] `PriceHub` (SignalR)
- [x] `ExceptionHandlingMiddleware`, `RequestLoggingMiddleware`

## Not Done / Needs Verification
- [x] `Program.cs` startup/OpenAPI/health-check wiring verified for .NET 10 (OpenAPI + Scalar + named health checks)
- [ ] EF Core migrations — none generated yet
- [ ] Test projects exist but test coverage unknown
- [ ] Frontend `client/` — scaffold exists, production-readiness unknown
- [ ] Backend ↔ Frontend API contract alignment not verified
- [x] Full solution build verification while WebAPI process is stopped (`dotnet build Quantira.sln` succeeded)

## Known Issues
- Some docs in `docs/` reference old project name `PortfolioTracker`
- No migration history — first `dotnet ef migrations add` needed before DB can be created
