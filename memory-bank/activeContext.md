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

## Active Decisions
- `ICacheService` is the only Redis abstraction visible to Application layer
- `Infrastructure.AI` stays isolated — no shared types with `Infrastructure`
- Controllers call only `IMediator.Send()` — no direct service injection

## Next Logical Steps
- Verify `IUnitOfWork` is correctly registered and scoped in DI
- Add integration tests for the Redis cache and key pipeline behaviors
- Confirm frontend `client/` API base URL is aligned with WebAPI launch settings
