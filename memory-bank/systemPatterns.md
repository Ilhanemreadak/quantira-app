# System Patterns

## Request Flow
```
HTTP Request
  → Controller (IMediator.Send only — no business logic)
  → MediatR Pipeline
      → LoggingBehavior        (structured log every request)
      → ValidationBehavior     (FluentValidation — throws on failure)
      → CachingBehavior        (Redis hit → skip handler entirely)
      → TransactionBehavior    (wraps commands in DB transaction)
  → Handler (IRequestHandler)
  → Domain / Repository / Service
  → Infrastructure (EF Core / Redis / HTTP)
```

## Key Design Decisions

### Command vs Query
- **Command**: mutates state, wrapped in transaction, never cached
- **Query**: read-only, may implement `ICacheableQuery` to opt into Redis cache
- Cache key convention: `"quantira:{entity}:{operation}:{id}"`

### Caching
- Application layer uses only `ICacheService` — no StackExchange.Redis types
- `RedisCacheService` silently swallows failures (log warning, return null/false)
- `RedisCacheOptions` (`KeyPrefix`, `DefaultExpiry`) bound from `appsettings "Redis"` section
- `BuildKey(key)` → `$"{KeyPrefix}:{key}"` — all keys are namespaced

### Repository
- Interfaces defined in **Domain** (`IPortfolioRepository`, etc.)
- Implementations in **Infrastructure/Persistence/Repositories/**
- EF Core for writes; Dapper for complex read queries
- Unit of Work (`IUnitOfWork`) wraps `SaveChangesAsync`
- Tracked aggregate/entity mutation flows must **not** call repository `Update(...)`.
  - Load tracked entity in write flow
  - Mutate via domain methods
  - Let EF change tracker persist on `SaveChangesAsync`
  - Never use graph-wide `_context.Update(...)` for tracked aggregates with child collections

### Market Data Providers
- `IMarketDataProvider` interface + `MarketDataProviderFactory` picks by asset type
- Priority: Binance → YahooFinance → GoldApi
- Each provider is a typed `HttpClient` with `AddStandardResilienceHandler()`
- Runtime guardrail: `MarketDataService` applies provider-scoped 429 circuit breaker for batch latest calls
  - Opens after 3 consecutive `429` responses from the same provider
  - Cooldown window: 5 minutes (provider skipped during cooldown)
  - Circuit state is tracked in-memory per application instance

### Asset Catalogue Providers
- Asset catalogue refresh now follows provider pattern via `IAssetProvider`
- `AssetCatalogueUpdateJob` iterates `IEnumerable<IAssetProvider>` (Open/Closed)
- Folder boundaries:
  - `Infrastructure/Jobs` contains only scheduled orchestration jobs
  - `Infrastructure/Assets` contains asset ingestion abstractions/providers
- Per-provider fault isolation: each provider runs in `try/catch`; failure logs `Error` and loop continues
- Insert strategy:
  - Query existing symbols by `AssetType`
  - Compute diff in memory
  - Insert only missing symbols (`AddRangeAsync`) inside `IDbContextTransaction`
  - Skip updates for existing rows in catalogue job (insert-only ingestion)
- Structured logs cover start/fetch/diff/transaction/result per provider
- Global stock ingestion is added via `GlobalStockAssetProvider` (Finnhub-style REST skeleton)
  - Config section: `AssetProviders:GlobalStocks`
  - Supports multi-exchange pulls via configurable `Exchanges`

### AI Integration
- `IAIService` defined in Application, implemented by `ClaudeAIService` in Infrastructure.AI
- `IPortfolioContextBuilder` assembles portfolio snapshot → injected into Claude prompt
- Streaming responses: `ReadLineAsync` loop with `if (line is null) break` (SSE/DONE pattern)

### Domain Model
- Entities extend `Entity<TId>`, aggregate roots extend `AggregateRoot<TId>`
- Value objects extend `ValueObject` (equality by value)
- Domain events raised on aggregate roots, dispatched post-commit
- Domain exceptions: `DomainException` (business rule), `NotFoundException` (missing entity)
