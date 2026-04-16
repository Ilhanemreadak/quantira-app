using Microsoft.AspNetCore.Authorization;
using Quantira.Infrastructure.Jobs;

namespace Quantira.WebAPI.Hubs;

/// <summary>
/// SignalR hub that streams real-time price updates to authenticated
/// dashboard clients. Inherits from <see cref="PriceHubMarker"/> so
/// <see cref="MarketDataRefreshJob"/> can reference it without a circular
/// project dependency between Infrastructure and WebAPI.
/// </summary>
[Authorize]
public sealed class PriceHub : PriceHubMarker
{
    private readonly ILogger<PriceHub> _logger;

    public PriceHub(ILogger<PriceHub> logger)
        => _logger = logger;

    public async Task SubscribeToSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return;
        var group = symbol.Trim().ToUpperInvariant();
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        _logger.LogDebug("[PriceHub] {ConnectionId} subscribed to {Symbol}",
            Context.ConnectionId, group);
    }

    public async Task UnsubscribeFromSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol)) return;
        var group = symbol.Trim().ToUpperInvariant();
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        _logger.LogDebug("[PriceHub] {ConnectionId} unsubscribed from {Symbol}",
            Context.ConnectionId, group);
    }

    public async Task SubscribeToPortfolio(IEnumerable<string> symbols)
    {
        foreach (var symbol in symbols)
            await SubscribeToSymbol(symbol);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("[PriceHub] Connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogDebug("[PriceHub] Disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}