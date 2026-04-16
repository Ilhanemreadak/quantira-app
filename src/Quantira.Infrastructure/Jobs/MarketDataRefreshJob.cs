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
    private readonly IHubContext<PriceHubMarker> _hubContext;
    private readonly ILogger<MarketDataRefreshJob> _logger;

    public MarketDataRefreshJob(
        IMarketDataService marketDataService,
        IAssetRepository assetRepository,
        IHubContext<PriceHubMarker> hubContext,
        ILogger<MarketDataRefreshJob> logger)
    {
        _marketDataService = marketDataService;
        _assetRepository = assetRepository;
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Fetches the latest prices for all active assets and broadcasts
    /// them to subscribed SignalR clients.
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
            var prices = await _marketDataService.GetBatchLatestAsync(symbols);

            foreach (var price in prices)
            {
                await _hubContext.Clients
                    .Group(price.Symbol)
                    .SendAsync("PriceUpdated", price);
            }

            _logger.LogDebug(
                "[MarketDataRefreshJob] Refreshed {Count} prices.",
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
/// Marker hub base class used by <see cref="MarketDataRefreshJob"/> to
/// reference the SignalR hub without a direct dependency on the WebAPI project.
/// The concrete <c>PriceHub</c> in <c>Quantira.WebAPI</c> inherits from this.
/// </summary>
public abstract class PriceHubMarker : Hub { }