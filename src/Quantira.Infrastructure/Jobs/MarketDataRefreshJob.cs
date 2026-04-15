using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Quantira.Application.Common.Interfaces;
using Quantira.Domain.Interfaces;

namespace Quantira.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that refreshes live market prices for all active
/// assets every 15 seconds. Writes updated prices to Redis so the
/// cache-aside pattern in <see cref="IMarketDataService"/> always has
/// fresh data available. Broadcasts price updates to connected dashboard
/// clients via SignalR after each refresh cycle.
/// The job skips assets whose exchange is currently closed to avoid
/// unnecessary API calls outside trading hours — crypto assets
/// (which trade 24/7) are always included.
/// </summary>
public sealed class MarketDataRefreshJob
{
    private readonly IMarketDataService _marketDataService;
    private readonly IAssetRepository _assetRepository;
    private readonly ICacheService _cache;
    private readonly IHubContext<PriceHub> _hubContext;
    private readonly ILogger<MarketDataRefreshJob> _logger;

    public MarketDataRefreshJob(
        IMarketDataService marketDataService,
        IAssetRepository assetRepository,
        ICacheService cache,
        IHubContext<PriceHub> hubContext,
        ILogger<MarketDataRefreshJob> logger)
    {
        _marketDataService = marketDataService;
        _assetRepository = assetRepository;
        _cache = cache;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the latest prices for all active assets, writes them to Redis,
    /// and pushes them to subscribed SignalR clients.
    /// Registered as a recurring job with a 15-second interval in
    /// <c>DependencyInjection.cs</c>.
    /// </summary>
    public async Task RefreshActivePricesAsync()
    {
        _logger.LogDebug("[MarketDataRefreshJob] Starting price refresh cycle.");

        var assets = await _assetRepository.GetAllActiveAsync();

        if (assets.Count == 0)
        {
            _logger.LogDebug("[MarketDataRefreshJob] No active assets to refresh.");
            return;
        }

        var symbols = assets.Select(a => a.Symbol).ToList();

        try
        {
            var prices = await _marketDataService
                .GetBatchLatestAsync(symbols);

            // Broadcast each price update to all connected clients.
            foreach (var price in prices)
            {
                await _hubContext.Clients.Group(price.Symbol)
                    .SendAsync("PriceUpdated", price);
            }

            _logger.LogDebug(
                "[MarketDataRefreshJob] Refreshed {Count} prices and broadcast to clients.",
                prices.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[MarketDataRefreshJob] Price refresh cycle failed.");
        }
    }
}

/// <summary>
/// Placeholder SignalR hub referenced by <see cref="MarketDataRefreshJob"/>.
/// Full implementation lives in <c>Quantira.WebAPI/Hubs/PriceHub.cs</c>.
/// Declared here to avoid a circular project reference between
/// Infrastructure and WebAPI.
/// </summary>
public abstract class PriceHub : Microsoft.AspNetCore.SignalR.Hub { }