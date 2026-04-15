using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Quantira.WebAPI.Hubs;

/// <summary>
/// SignalR hub that streams real-time price updates to authenticated
/// dashboard clients. Clients subscribe to asset-specific groups
/// (one group per symbol) so they only receive updates for assets
/// they are currently viewing or holding in their portfolio.
/// <c>MarketDataRefreshJob</c> broadcasts to these groups every 15 seconds
/// after fetching fresh prices from the external providers.
/// Authentication is required — anonymous clients are rejected at the
/// hub level before any group subscription is processed.
/// </summary>
[Authorize]
public sealed class PriceHub : Hub
{
    private readonly ILogger<PriceHub> _logger;

    public PriceHub(ILogger<PriceHub> logger)
        => _logger = logger;

    /// <summary>
    /// Called by the client to subscribe to price updates for a specific symbol.
    /// The client is added to a SignalR group named after the symbol
    /// so it receives targeted broadcasts from <c>MarketDataRefreshJob</c>.
    /// </summary>
    /// <param name="symbol">
    /// The asset symbol to subscribe to (e.g. "THYAO", "BTC", "XAU/USD").
    /// </param>
    public async Task SubscribeToSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return;

        var group = symbol.Trim().ToUpperInvariant();
        await Groups.AddToGroupAsync(Context.ConnectionId, group);

        _logger.LogDebug(
            "[PriceHub] Client {ConnectionId} subscribed to {Symbol}",
            Context.ConnectionId, group);
    }

    /// <summary>
    /// Called by the client to unsubscribe from price updates for a symbol.
    /// Should be called when the user navigates away from an asset's detail page.
    /// </summary>
    public async Task UnsubscribeFromSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return;

        var group = symbol.Trim().ToUpperInvariant();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);

        _logger.LogDebug(
            "[PriceHub] Client {ConnectionId} unsubscribed from {Symbol}",
            Context.ConnectionId, group);
    }

    /// <summary>
    /// Called by the client to subscribe to all symbols in the user's
    /// active portfolio at once. Convenience method for dashboard load.
    /// </summary>
    /// <param name="symbols">List of symbols to subscribe to.</param>
    public async Task SubscribeToPortfolio(IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols)
            await SubscribeToSymbol(symbol);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug(
            "[PriceHub] Client connected: {ConnectionId}",
            Context.ConnectionId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug(
            "[PriceHub] Client disconnected: {ConnectionId}",
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }
}