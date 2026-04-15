# Quantira Platform — Cline Rules

## Memory Bank — MANDATORY

Before touching any file or writing any code, read **all** of the following in order:

1. `memory-bank/projectbrief.md`
2. `memory-bank/productContext.md`
3. `memory-bank/systemPatterns.md`
4. `memory-bank/techContext.md`
5. `memory-bank/activeContext.md`
6. `memory-bank/progress.md`

If a file is missing, create it with sensible defaults before proceeding.
Skip none — each file builds on the previous one.

After completing any significant change (new feature, refactor, bug fix, package change),
update the relevant memory bank files so the next session starts with accurate context.
When the user says **update memory bank**, review and rewrite ALL six files.

---

## Project Map — Read Before Searching

```
src/
├── Quantira.Domain/
│   ├── Entities/          # Portfolio, Asset, Trade, Alert
│   ├── ValueObjects/      # Money, PnL, Currency, DateRange
│   ├── Enums/             # AssetType, TradeType, CostMethod, AlertType, MarketStatus
│   ├── Events/            # Domain events (PortfolioCreated, TradeAdded, …)
│   ├── Exceptions/        # DomainException, NotFoundException
│   ├── Interfaces/        # IUnitOfWork, IPortfolioRepository, IAssetRepository, …
│   └── Common/            # Entity<T>, AggregateRoot, ValueObject, IDomainEvent
│
├── Quantira.Application/
│   ├── Common/
│   │   ├── Behaviors/     # LoggingBehavior, ValidationBehavior, CachingBehavior, TransactionBehavior
│   │   ├── Interfaces/    # ICacheService, IAIService, IMarketDataService, INotificationService, IIndicatorEngine
│   │   └── Models/        # PagedResult<T>
│   ├── Portfolios/
│   │   ├── Commands/      # CreatePortfolio, AddTrade, DeletePortfolio
│   │   ├── Queries/       # GetPortfolioList, GetPortfolioSummary, GetTradeHistory
│   │   └── DTOs/          # PortfolioDto, PortfolioSummaryDto, PositionDto, TradeDto
│   ├── Assets/
│   │   ├── Commands/      # CreateAsset
│   │   ├── Queries/       # GetAssetBySymbol
│   │   └── DTOs/          # AssetDto
│   ├── Alerts/
│   │   ├── Commands/      # CreateAlert, DeleteAlert
│   │   ├── Queries/       # GetUserAlerts
│   │   └── DTOs/          # AlertDto
│   ├── MarketData/
│   │   ├── Queries/       # GetPriceHistory, CalculateIndicator
│   │   └── DTOs/          # OhlcvDto, PriceLatestDto, IndicatorResultDto
│   └── Chat/
│       ├── Commands/      # SendMessage
│       ├── Queries/       # GetChatHistory
│       └── Services/      # IPortfolioContextBuilder, IChatSessionService
│
├── Quantira.Infrastructure/
│   ├── Cache/             # RedisCacheService, RedisCacheOptions
│   ├── Jobs/              # MarketDataRefreshJob, AlertCheckJob, NewsIngestionJob
│   ├── MarketData/
│   │   ├── Providers/     # BinanceProvider, YahooFinanceProvider, GoldApiProvider
│   │   └── MarketDataProviderFactory, IMarketDataProvider
│   ├── Notifications/     # EmailNotificationService
│   ├── Persistence/
│   │   ├── Configurations/  # EF Fluent API configs per entity
│   │   ├── Repositories/    # PortfolioRepository, AssetRepository, …
│   │   ├── QuantiraDbContext.cs
│   │   └── ApplicationUser.cs
│   └── DependencyInjection.cs
│
├── Quantira.Infrastructure.AI/
│   ├── Services/          # ClaudeAIService
│   ├── Prompts/           # PortfolioContextBuilder, prompt templates
│   └── DependencyInjection.cs
│
└── Quantira.WebAPI/
    ├── Controllers/       # PortfoliosController, AssetsController, TradesController,
    │                      # MarketDataController, AuthController
    ├── Hubs/              # PriceHub (SignalR)
    ├── Middleware/        # ExceptionHandlingMiddleware, RequestLoggingMiddleware
    └── Program.cs
```

**Adding a new feature checklist:**
1. Domain entity/value object → `Domain/Entities/` or `Domain/ValueObjects/`
2. Repository interface → `Domain/Interfaces/`
3. Command or Query + Handler + Validator → `Application/{Feature}/Commands/` or `Queries/`
4. DTO → `Application/{Feature}/DTOs/`
5. EF config → `Infrastructure/Persistence/Configurations/`
6. Repository impl → `Infrastructure/Persistence/Repositories/`
7. Controller endpoint → `WebAPI/Controllers/`
8. Register in → `Infrastructure/DependencyInjection.cs`

---

## Architecture — Non-Negotiable Rules

### Dependency Direction
```
Domain  ←  Application  ←  Infrastructure      ←  WebAPI
                        ←  Infrastructure.AI   ←  WebAPI
```
- Application **never** references Infrastructure or WebAPI.
- Domain **never** references anything.
- Controllers only call `IMediator.Send()` — no service injection in controllers.

### Layer Responsibilities
| Layer | Owns | Never Contains |
|---|---|---|
| Domain | Entities, ValueObjects, domain logic, interfaces | NuGet packages, EF, HTTP |
| Application | Use cases, DTOs, pipeline behaviors, `IXxx` interfaces | EF types, Redis, HTTP clients |
| Infrastructure | EF, Redis, HTTP, Hangfire implementations | Business logic |
| Infrastructure.AI | Claude integration only | Unrelated services |
| WebAPI | Endpoints, middleware, DI wiring | Business logic |

---

## Key Packages (authoritative list)

### Quantira.Application
- `MediatR` 14
- `FluentValidation.DependencyInjectionExtensions` 12
- `Mapster` 10 + `Mapster.DependencyInjection` 10

### Quantira.Infrastructure
- `Microsoft.EntityFrameworkCore.SqlServer` 10
- `StackExchange.Redis` 2.12.x
- `Hangfire.AspNetCore` + `Hangfire.SqlServer` 1.8.x
- `Microsoft.Extensions.Http.Resilience`
- `Serilog.AspNetCore` 10
- `MongoDB.Driver` 3.x
- `Dapper` 2.x

### Quantira.Infrastructure.AI
- `Microsoft.Extensions.Http`
- `Microsoft.Extensions.Http.Resilience`
- `Microsoft.Extensions.Options.ConfigurationExtensions`

### Quantira.WebAPI
- `Microsoft.AspNetCore.OpenApi` 10
- `Scalar.AspNetCore`

### Banned Packages — Never Add
| Package | Why banned | Use instead |
|---|---|---|
| `Microsoft.Extensions.Caching.Redis` | Pulls StackExchange.Redis.StrongName v1.2.6 → type conflicts | `StackExchange.Redis` directly |
| `Polly.Extensions.Http` | Incompatible with .NET 10 | `Microsoft.Extensions.Http.Resilience` |

---

## .NET 10 Code Style

### File Structure
```csharp
// 1. using directives — alphabetical, System first
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quantira.Application.Common.Interfaces;

// 2. File-scoped namespace — always
namespace Quantira.Infrastructure.Cache;

// 3. Class declaration
public sealed class RedisCacheService : ICacheService
{
    // 4. Private fields — readonly first, then others
    private readonly IDatabase _database;
    private readonly ILogger<RedisCacheService> _logger;

    // 5. Static fields
    private static readonly JsonSerializerOptions JsonOptions = new() { ... };

    // 6. Constructor
    public RedisCacheService(...) { }

    // 7. Public interface methods — in interface declaration order
    // 8. Private methods at the bottom
}
```

### Naming
| Element | Convention | Example |
|---|---|---|
| Class / Interface | PascalCase | `PortfolioRepository`, `ICacheService` |
| Method | PascalCase | `GetByIdAsync` |
| Property | PascalCase | `CacheKey` |
| Private field | `_camelCase` | `_database` |
| Parameter / local var | camelCase | `portfolioId` |
| Async method | suffix `Async` | `GetPortfolioAsync` |
| Command/Query | noun + verb | `CreatePortfolioCommand`, `GetPortfolioSummaryQuery` |
| Handler | command/query name + `Handler` | `CreatePortfolioCommandHandler` |

### Language Rules
```csharp
// ✅ File-scoped namespace
namespace Quantira.Domain.Entities;

// ✅ sealed by default — remove only when inheritance is required
public sealed class Portfolio : AggregateRoot<Guid> { }

// ✅ Records for commands, queries, DTOs — immutable by design
public sealed record CreatePortfolioCommand(string Name, string Currency) : IRequest<Guid>;

// ✅ Expression body for simple single-expression members
private string BuildKey(string key) => $"{_options.KeyPrefix}:{key}";

// ✅ var only when type is obvious from the right-hand side
var portfolios = new List<PortfolioDto>();  // ✅ obvious
List<PortfolioDto> result = await handler.Handle(query);  // ✅ not obvious → explicit

// ✅ Pattern matching over type checks
if (request is not ICacheableQuery cacheableQuery)
    return await next();

// ✅ Null-coalescing for guard clauses
var connection = configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string is missing.");

// ✅ Collection expressions (.NET 8+)
RedisKey[] keys = [.. _server.Keys(pattern: pattern)];

// ❌ Never use nullable suppression (!) without a comment explaining why
var value = GetValue()!;  // only if null is provably impossible — add a comment

// ❌ No nested ternaries
// ❌ No mutable public fields — always properties
// ❌ No public setters on domain entities — use domain methods
```

### Async Rules
```csharp
// ✅ Always propagate CancellationToken
public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)

// ✅ Streaming: ReadLineAsync + null break — never EndOfStream in async (CA2024)
while (!cancellationToken.IsCancellationRequested)
{
    var line = await reader.ReadLineAsync(cancellationToken);
    if (line is null) break;
}

// ✅ ConfigureAwait not needed in ASP.NET Core — skip it
await _database.StringGetAsync(key);  // no .ConfigureAwait(false)

// ❌ Never async void — use async Task
// ❌ Never .Result or .Wait() — always await
```

### MediatR Patterns
```csharp
// Command — mutates state, returns simple result
public sealed record CreatePortfolioCommand(
    string Name,
    string Currency,
    string OwnerId) : IRequest<Guid>;

public sealed class CreatePortfolioCommandHandler
    : IRequestHandler<CreatePortfolioCommand, Guid>
{
    // dependencies injected via constructor
    public async Task<Guid> Handle(
        CreatePortfolioCommand request,
        CancellationToken cancellationToken) { ... }
}

// Query — reads state, never mutates
public sealed record GetPortfolioSummaryQuery(Guid PortfolioId)
    : IRequest<PortfolioSummaryDto>, ICacheableQuery
{
    public string CacheKey => $"quantira:portfolio:summary:{PortfolioId}";
    public TimeSpan? CacheDuration => TimeSpan.FromSeconds(60);
}

// Validator — always in same folder as command
public sealed class CreatePortfolioCommandValidator
    : AbstractValidator<CreatePortfolioCommand>
{
    public CreatePortfolioCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
    }
}
```

### Repository Pattern
```csharp
// Interface lives in Domain
public interface IPortfolioRepository
{
    Task<Portfolio?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Portfolio>> GetByOwnerAsync(string ownerId, CancellationToken cancellationToken = default);
    void Add(Portfolio portfolio);
    void Remove(Portfolio portfolio);
}

// Implementation lives in Infrastructure/Persistence/Repositories/
// Use EF Core for writes, Dapper for complex read queries
```

### Error Handling
```csharp
// ✅ Throw domain exceptions from domain layer
throw new DomainException("Portfolio cannot have negative value.");

// ✅ Throw NotFoundException from handlers when entity is missing
var portfolio = await _repo.GetByIdAsync(id, ct)
    ?? throw new NotFoundException(nameof(Portfolio), id);

// ✅ Infrastructure services swallow their own exceptions — never propagate cache/http failures
catch (Exception ex)
{
    _logger.LogWarning(ex, "[Redis] GET failed for key {Key}.", key);
    return default;
}

// ❌ Never add try/catch in Application handlers — ExceptionHandlingMiddleware catches everything
```

### DI Registration Pattern
```csharp
// Every project has exactly one DependencyInjection.cs at its root
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Group by concern with comments:
        // ── SQL Server / EF Core ─────────
        // ── Redis ────────────────────────
        // ── Hangfire ─────────────────────
        return services;
    }
}
```

### XML Documentation
- Write `/// <summary>` only on `public` API members that are **not self-explanatory**.
- Never add doc comments to methods you did not create or change.
- Never write `/// <summary> Gets the portfolio. </summary>` — if the method name says it all, skip it.

---

## What NOT To Do

- Do not add `using Microsoft.Extensions.Caching.Redis;` anywhere.
- Do not reference StackExchange.Redis types in Application or Domain layers.
- Do not inject services directly into controllers — use `IMediator` only.
- Do not add business logic to WebAPI endpoints or middleware.
- Do not generate migration files — developer runs `dotnet ef migrations add` manually.
- Do not add `[Obsolete]` stubs, backwards-compat shims, or `_unused` renames.
- Do not speculate on future requirements — implement exactly what is asked.
- Do not add XML doc comments to unchanged code.
- Do not create helper/utility classes for one-time use.
- Do not add validation inside domain entities for things that belong in FluentValidation.
- Do not use `async void` anywhere.
- Do not call `.Result` or `.Wait()` on Tasks.
- Do not use `reader.EndOfStream` in async methods (CA2024).
