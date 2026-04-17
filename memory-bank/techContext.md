# Tech Context

## Backend
| Package | Version | Project |
|---|---|---|
| .NET / ASP.NET Core | 10.0 | all |
| MediatR | 14.1.0 | Application |
| FluentValidation | 12.1.1 | Application |
| Mapster | 10.0.7 | Application |
| EF Core SqlServer | 10.0.5 | Infrastructure |
| StackExchange.Redis | 2.12.14 | Infrastructure |
| Hangfire (AspNetCore + SqlServer) | 1.8.23 | Infrastructure |
| Microsoft.Extensions.Http.Resilience | 10.5.0 | Infrastructure, Infrastructure.AI |
| Serilog.AspNetCore | 10.0.0 | Infrastructure |
| MongoDB.Driver | 3.7.1 | Infrastructure |
| Dapper | 2.1.72 | Infrastructure |
| Microsoft.Extensions.Options.ConfigurationExtensions | 10.0.6 | Infrastructure.AI |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.5 | WebAPI |
| Microsoft.AspNetCore.OpenApi | 10.0.6 | WebAPI |
| Scalar.AspNetCore | 2.13.22 | WebAPI |
| AspNetCore.HealthChecks.SqlServer | 9.0.0 | WebAPI |
| AspNetCore.HealthChecks.Redis | 9.0.0 | WebAPI |

## Frontend
React 19, TypeScript, Vite 8 — `client/` directory.
TanStack Query/Router/Table, Zustand, React Hook Form + Zod, Recharts, lightweight-charts.

## Banned Packages
| Package | Reason |
|---|---|
| `Microsoft.Extensions.Caching.Redis` | Pulls `StackExchange.Redis.StrongName` v1.2.6 → type identity conflict with v2.x |
| `Polly.Extensions.Http` | Not compatible with .NET 10 — use `Microsoft.Extensions.Http.Resilience` |

## Recent Infra Notes
- `Quantira.Infrastructure.csproj` no longer references `Polly.Extensions.Http`.
- Asset catalogue providers are registered through DI with `IAssetProvider` implementations.
- Named HttpClient `"Binance"` (`https://api.binance.com/`) is used for catalogue symbol ingestion.
- Named HttpClient `"GlobalStocks"` is registered for global stock catalogue ingestion (Finnhub-style endpoint).
- Provider placement follows feature-based infra organization:
  - `Infrastructure/Assets` for asset catalogue providers
  - `Infrastructure/Jobs` for Hangfire job orchestration only

## Configuration Sections (appsettings)
- `ConnectionStrings:SqlServer` — EF Core + Hangfire
- `ConnectionStrings:Redis` — StackExchange.Redis multiplexer
- `Redis` — bound to `RedisCacheOptions` (`KeyPrefix`, `DefaultExpiry`)
- `Hangfire` — bound to `HangfireSettings` (`WorkerCount`, `Dashboard:Enabled|Path|RequireSsl|IsReadOnly|Username|Password|AllowedIpNetworks`)
- `Claude` — bound to `ClaudeOptions` (API key, model, endpoint)
- `Email` — bound to `EmailOptions`
- `MarketData:GoldApiKey` — GoldApi provider header
- `AssetProviders:GlobalStocks` — bound to `GlobalStockProviderOptions` (`Enabled`, `BaseUrl`, `ApiKey`, `DefaultCurrency`, `DefaultExchangeLabel`, `Exchanges`)

## Known Async Rules
- Never `reader.EndOfStream` in async methods (CA2024) — use `ReadLineAsync` + null check
- Never `.Result` / `.Wait()` — always `await`
- Never `async void` — always `async Task`
- `CancellationToken` required on every async public method
