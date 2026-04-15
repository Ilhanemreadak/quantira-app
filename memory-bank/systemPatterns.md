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

### Market Data Providers
- `IMarketDataProvider` interface + `MarketDataProviderFactory` picks by asset type
- Priority: Binance → YahooFinance → GoldApi
- Each provider is a typed `HttpClient` with `AddStandardResilienceHandler()`

### AI Integration
- `IAIService` defined in Application, implemented by `ClaudeAIService` in Infrastructure.AI
- `IPortfolioContextBuilder` assembles portfolio snapshot → injected into Claude prompt
- Streaming responses: `ReadLineAsync` loop with `if (line is null) break` (SSE/DONE pattern)

### Domain Model
- Entities extend `Entity<TId>`, aggregate roots extend `AggregateRoot<TId>`
- Value objects extend `ValueObject` (equality by value)
- Domain events raised on aggregate roots, dispatched post-commit
- Domain exceptions: `DomainException` (business rule), `NotFoundException` (missing entity)
