using Hangfire;
using Hangfire.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quantira.Application.Chat.Services;
using Quantira.Application.Common.Interfaces;
using Quantira.Domain.Interfaces;
using Quantira.Infrastructure.Assets;
using Quantira.Infrastructure.Assets.Providers;
using Quantira.Infrastructure.Cache;
using Quantira.Infrastructure.Chat;
using Quantira.Infrastructure.Indicators;
using Quantira.Infrastructure.Jobs;
using Quantira.Infrastructure.MarketData;
using Quantira.Infrastructure.MarketData.Providers;
using Quantira.Infrastructure.Notifications;
using Quantira.Infrastructure.Persistence;
using Quantira.Infrastructure.Persistence.Repositories;
using StackExchange.Redis;

namespace Quantira.Infrastructure;

/// <summary>
/// Extension method that registers all <c>Quantira.Infrastructure</c>
/// dependencies into the ASP.NET Core DI container.
/// Called from <c>Program.cs</c> after <c>AddApplication()</c>.
/// Wires up EF Core, Redis, Hangfire, market data providers, repositories,
/// cache service, and notification service in a single place.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── SQL Server / EF Core ─────────────────────────────────────
        services.AddDbContext<QuantiraDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("SqlServer"),
                sql => sql.MigrationsAssembly(
                    typeof(QuantiraDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork>(sp =>
            sp.GetRequiredService<QuantiraDbContext>());

        // ── Repositories ─────────────────────────────────────────────
        services.AddScoped<IPortfolioRepository, PortfolioRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IPositionRepository, PositionRepository>();
        services.AddScoped<ITradeRepository, TradeRepository>();
        services.AddScoped<IAlertRepository, AlertRepository>();

        // ── Redis ────────────────────────────────────────────────────
        var redisConnection = configuration.GetConnectionString("Redis")
            ?? throw new InvalidOperationException(
                "Redis connection string is missing. Add it via User Secrets.");

        services.AddSingleton<IConnectionMultiplexer>(
            ConnectionMultiplexer.Connect(redisConnection));

        services.Configure<RedisCacheOptions>(
            configuration.GetSection("Redis"));

        services.AddSingleton<ICacheService, RedisCacheService>();

        // ── Market Data Providers ────────────────────────────────────
        // Register in priority order — factory picks the first match.
        services.AddHttpClient<BinanceProvider>()
            .AddStandardResilienceHandler();

        services.AddHttpClient<YahooFinanceProvider>()
            .AddStandardResilienceHandler();

        services.AddHttpClient<GoldApiProvider>(client =>
        {
            client.DefaultRequestHeaders.Add(
                "x-access-token",
                configuration["MarketData:GoldApiKey"]);
        }).AddStandardResilienceHandler();

        services.AddHttpClient("Binance", client =>
        {
            client.BaseAddress = new Uri("https://api.binance.com/");
        }).AddStandardResilienceHandler();

        services.Configure<GlobalStockProviderOptions>(
            configuration.GetSection(GlobalStockProviderOptions.SectionName));

        services.AddHttpClient("GlobalStocks", client =>
        {
            var configuredBaseUrl = configuration[
                $"{GlobalStockProviderOptions.SectionName}:{nameof(GlobalStockProviderOptions.BaseUrl)}"];

            var baseUrl = string.IsNullOrWhiteSpace(configuredBaseUrl)
                ? "https://finnhub.io/"
                : configuredBaseUrl;

            client.BaseAddress = new Uri(baseUrl);
        }).AddStandardResilienceHandler();

        services.AddSingleton<IMarketDataProvider, BinanceProvider>();
        services.AddSingleton<IMarketDataProvider, YahooFinanceProvider>();
        services.AddSingleton<IMarketDataProvider, GoldApiProvider>();

        services.AddSingleton<MarketDataProviderFactory>();
        services.AddScoped<IMarketDataService, MarketDataService>();

        // ── Indicator Engine (stub — replace when indicators are implemented) ─
        services.AddScoped<IIndicatorEngine, StubIndicatorEngine>();

        // ── Chat Session Service (stub — replace when MongoDB service is ready) ─
        services.AddScoped<IChatSessionService, StubChatSessionService>();

        services.AddScoped<IAssetProvider, BinanceAssetProvider>();
        services.AddScoped<IAssetProvider, BistAssetProvider>();
        services.AddScoped<IAssetProvider, GlobalStockAssetProvider>();
        services.AddScoped<AssetCatalogueUpdateJob>();

        // ── Hangfire ─────────────────────────────────────────────────
        services.AddHangfire(config => config
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                configuration.GetConnectionString("SqlServer"),
                new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                }));

        services.AddHangfireServer(options =>
            options.WorkerCount = 5);

        services.AddScoped<MarketDataRefreshJob>();
        services.AddScoped<AlertCheckJob>();
        services.AddScoped<NewsIngestionJob>();

        // ── Notifications ────────────────────────────────────────────
        services.Configure<EmailOptions>(
            configuration.GetSection("Email"));

        services.AddScoped<INotificationService, EmailNotificationService>();

        return services;
    }

    /// <summary>
    /// Registers Hangfire recurring jobs. Called from <c>Program.cs</c>
    /// after the application is built, using the
    /// <see cref="IRecurringJobManager"/> from the DI container.
    /// </summary>
    public static void RegisterRecurringJobs(IRecurringJobManager jobManager)
    {
        jobManager.AddOrUpdate<MarketDataRefreshJob>(
            recurringJobId: "market-data-refresh",
            methodCall: job => job.RefreshActivePricesAsync(),
            cronExpression: "*/15 * * * * *",  // Every 15 seconds
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        jobManager.AddOrUpdate<AlertCheckJob>(
            recurringJobId: "alert-check",
            methodCall: job => job.CheckAlertsAsync(),
            cronExpression: "*/30 * * * * *",  // Every 30 seconds
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        jobManager.AddOrUpdate<NewsIngestionJob>(
            recurringJobId: "news-ingestion",
            methodCall: job => job.IngestNewsAsync(),
            cronExpression: "0 */30 * * * *",  // Every 30 minutes
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });

        jobManager.AddOrUpdate<AssetCatalogueUpdateJob>(
            recurringJobId: "asset-catalogue-update",
            methodCall: job => job.RunAllUpdatesAsync(CancellationToken.None),
            cronExpression: "0 2 * * *",   // Her gün saat 02:00 UTC
            options: new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }
}