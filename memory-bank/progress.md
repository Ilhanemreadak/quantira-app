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

## Known Issues
- Some docs in `docs/` reference old project name `PortfolioTracker`
- No migration history — first `dotnet ef migrations add` needed before DB can be created
