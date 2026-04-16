using Microsoft.Extensions.Logging;
using Quantira.Application.Common.Interfaces;
using Quantira.Application.MarketData.DTOs;
using Quantira.Domain.Interfaces;

namespace Quantira.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that evaluates all active alerts against the
/// latest market prices every 30 seconds. When an alert's condition is met,
/// the alert aggregate's <c>Trigger</c> method is called which raises
/// <c>AlertTriggeredEvent</c> — the notification pipeline handles delivery.
/// Expired alerts are automatically transitioned to the Expired status.
/// Uses Redis for price lookups to avoid hitting the external API
/// on every evaluation cycle.
/// </summary>
public sealed class AlertCheckJob
{
    private readonly IAlertRepository _alertRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly ICacheService _cache;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AlertCheckJob> _logger;

    public AlertCheckJob(
        IAlertRepository alertRepository,
        IAssetRepository assetRepository,
        ICacheService cache,
        IUnitOfWork unitOfWork,
        ILogger<AlertCheckJob> logger)
    {
        _alertRepository = alertRepository;
        _assetRepository = assetRepository;
        _cache = cache;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    /// <summary>
    /// Evaluates all active alerts and triggers or expires them as appropriate.
    /// </summary>
    public async Task CheckAlertsAsync()
    {
        var activeAlerts = await _alertRepository.GetAllActiveAsync();

        if (activeAlerts.Count == 0) return;

        _logger.LogDebug(
            "[AlertCheckJob] Evaluating {Count} active alerts.",
            activeAlerts.Count);

        // Build a symbol map once — avoids N+1 asset lookups per cycle.
        var assetIds = activeAlerts
            .Select(a => a.AssetId)
            .Distinct()
            .ToList();

        var assets = await _assetRepository.GetByIdsAsync(assetIds);

        var symbolMap = assets
            .ToDictionary(asset => asset.Id, asset => asset.Symbol);

        var triggeredCount = 0;
        var expiredCount = 0;

        foreach (var alert in activeAlerts)
        {
            try
            {
                // Check expiry first.
                if (alert.ExpiresAt.HasValue
                    && alert.ExpiresAt.Value <= DateTime.UtcNow)
                {
                    alert.Expire();
                    _alertRepository.Update(alert);
                    expiredCount++;
                    continue;
                }

                // Resolve symbol from map.
                if (!symbolMap.TryGetValue(alert.AssetId, out var symbol))
                    continue;

                // Cache key must match MarketDataService write pattern:
                var cacheKey = $"price:{symbol.ToUpperInvariant()}";
                var priceLatest = await _cache.GetAsync<PriceLatestDto>(cacheKey);

                if (priceLatest is null) continue;

                if (ShouldTrigger(alert, priceLatest.Price))
                {
                    alert.Trigger(priceLatest.Price);
                    _alertRepository.Update(alert);
                    triggeredCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[AlertCheckJob] Error evaluating alert {AlertId}.",
                    alert.Id);
            }
        }

        if (triggeredCount > 0 || expiredCount > 0)
            await _unitOfWork.SaveChangesAsync();

        _logger.LogDebug(
            "[AlertCheckJob] Cycle complete. Triggered={Triggered} Expired={Expired}",
            triggeredCount, expiredCount);
    }

    private static bool ShouldTrigger(
        Domain.Entities.Alert alert,
        decimal currentPrice)
    {
        try
        {
            var condition = System.Text.Json.JsonDocument
                .Parse(alert.ConditionJson)
                .RootElement;

            return alert.AlertType switch
            {
                Domain.Enums.AlertType.PriceAbove =>
                    condition.TryGetProperty("threshold", out var aboveEl)
                    && currentPrice > aboveEl.GetDecimal(),

                Domain.Enums.AlertType.PriceBelow =>
                    condition.TryGetProperty("threshold", out var belowEl)
                    && currentPrice < belowEl.GetDecimal(),

                _ => false
            };
        }
        catch
        {
            return false;
        }
    }
}