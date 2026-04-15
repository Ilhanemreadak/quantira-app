# Active Context

## Current State
Build is clean: **0 errors, 0 warnings** across all 5 backend projects on .NET 10.

## Recent Changes (this session)
- Removed `Microsoft.Extensions.Caching.Redis` from Infrastructure.csproj — was pulling `StackExchange.Redis.StrongName` v1.2.6 causing type identity conflicts
- Created `Infrastructure/Cache/RedisCacheOptions.cs` — custom options class with `KeyPrefix` and `DefaultExpiry`
- Rewrote `RedisCacheService.cs` — completed truncated methods, added `ExistsAsync`, added `BuildKey`, fixed ambiguous `JsonSerializer.Deserialize` cast
- Added missing packages to `Infrastructure.AI.csproj`: `Microsoft.Extensions.Http`, `Microsoft.Extensions.Http.Resilience`, `Microsoft.Extensions.Options.ConfigurationExtensions`
- Fixed `ClaudeAIService.cs` line 120: replaced `reader.EndOfStream` with `ReadLineAsync` + null check (CA2024)
- Created `.clinerules/default-rules.md` — comprehensive coding rules and project map
- Initialized `memory-bank/` with all 6 required files

## Active Decisions
- `ICacheService` is the only Redis abstraction visible to Application layer
- `Infrastructure.AI` stays isolated — no shared types with `Infrastructure`
- Controllers call only `IMediator.Send()` — no direct service injection

## Next Logical Steps
- Wire up `Program.cs` endpoints (currently minimal — controllers exist but mapping may be incomplete)
- Verify `IUnitOfWork` is correctly registered and scoped in DI
- Add integration tests for the Redis cache and key pipeline behaviors
- Confirm frontend `client/` API base URL is aligned with WebAPI launch settings
