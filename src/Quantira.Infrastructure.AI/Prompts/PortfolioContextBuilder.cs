using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quantira.Application.Chat.Services;
using Quantira.Application.Common.Interfaces;
using Quantira.Domain.Interfaces;

namespace Quantira.Infrastructure.AI.Prompts;

/// <summary>
/// Builds a compact JSON context string that is injected into every
/// AI request as part of the system prompt. The context gives Claude
/// awareness of the user's current financial situation without requiring
/// the user to describe their portfolio in every message.
/// Designed to stay under 2000 tokens to leave sufficient room for
/// the conversation history and the model's response within the
/// context window budget.
/// </summary>
public sealed class PortfolioContextBuilder : IPortfolioContextBuilder
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IMarketDataService _marketDataService;
    private readonly IIndicatorEngine _indicatorEngine;
    private readonly ILogger<PortfolioContextBuilder> _logger;

    public PortfolioContextBuilder(
        IPortfolioRepository portfolioRepository,
        IMarketDataService marketDataService,
        IIndicatorEngine indicatorEngine,
        ILogger<PortfolioContextBuilder> logger)
    {
        _portfolioRepository = portfolioRepository;
        _marketDataService = marketDataService;
        _indicatorEngine = indicatorEngine;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> BuildAsync(
        Guid userId,
        Guid? portfolioId = null,
        Guid? assetId = null,
        CancellationToken cancellationToken = default)
    {
        var context = new ContextPayload();

        if (portfolioId.HasValue)
        {
            try
            {
                await EnrichWithPortfolioAsync(
                    context, portfolioId.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[ContextBuilder] Failed to enrich portfolio context " +
                    "for portfolio {PortfolioId}", portfolioId);
            }
        }

        if (assetId.HasValue)
        {
            try
            {
                await EnrichWithAssetAsync(
                    context, assetId.Value, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[ContextBuilder] Failed to enrich asset context " +
                    "for asset {AssetId}", assetId);
            }
        }

        return JsonSerializer.Serialize(context, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition =
                System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    // ── Private enrichment methods ───────────────────────────────────

    private async Task EnrichWithPortfolioAsync(
        ContextPayload context,
        Guid portfolioId,
        CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository
            .GetWithPositionsAsync(portfolioId, cancellationToken);

        if (portfolio is null) return;

        var positions = portfolio.Positions
            .Where(p => p.Quantity > 0)
            .ToList();

        var totalCost = positions.Sum(p => p.TotalCost.Amount);
        var totalValue = positions.Sum(p => p.CurrentValue?.Amount ?? 0m);
        var totalPnL = totalValue - totalCost;
        var pnlPct = totalCost == 0 ? 0m : totalPnL / totalCost * 100m;

        context.Portfolio = new PortfolioContext(
            Name: portfolio.Name,
            BaseCurrency: portfolio.BaseCurrency.Code,
            TotalCost: Math.Round(totalCost, 2),
            TotalValue: Math.Round(totalValue, 2),
            PnL: Math.Round(totalPnL, 2),
            PnLPct: Math.Round(pnlPct, 2),
            Positions: positions
                .OrderByDescending(p => p.CurrentValue?.Amount ?? 0)
                .Take(10)
                .Select(p => new PositionContext(
                    AssetId: p.AssetId.ToString()[..8],
                    Qty: p.Quantity,
                    AvgCost: Math.Round(p.AvgCostPrice.Amount, 4),
                    CurrentValue: Math.Round(p.CurrentValue?.Amount ?? 0, 2),
                    PnL: Math.Round(p.UnrealizedPnL?.Amount ?? 0, 2),
                    PnLPct: Math.Round(p.UnrealizedPnLPct ?? 0, 2)))
                .ToList());
    }

    private async Task EnrichWithAssetAsync(
        ContextPayload context,
        Guid assetId,
        CancellationToken cancellationToken)
    {
        // Use asset ID string as symbol placeholder until full asset repo
        // lookup is wired — replace with GetByIdAsync in next iteration.
        var assetSymbol = assetId.ToString()[..8];

        var latestPrice = await _marketDataService
            .GetLatestAsync(assetSymbol, cancellationToken);

        Application.MarketData.DTOs.IndicatorResultDto? rsi = null;
        Application.MarketData.DTOs.IndicatorResultDto? macd = null;

        try
        {
            rsi = await _indicatorEngine.CalculateAsync(
                assetSymbol, "RSI", "1d",
                cancellationToken: cancellationToken);
        }
        catch { /* Indicator unavailable — skip */ }

        try
        {
            macd = await _indicatorEngine.CalculateAsync(
                assetSymbol, "MACD", "1d",
                cancellationToken: cancellationToken);
        }
        catch { /* Indicator unavailable — skip */ }

        context.Asset = new AssetContext(
            Symbol: latestPrice.Symbol,
            Price: latestPrice.Price,
            Change: latestPrice.Change,
            ChangePct: latestPrice.ChangePct,
            Status: latestPrice.MarketStatus,
            RsiValue: rsi?.Values.LastOrDefault()?.Value,
            MacdValue: macd?.Values.LastOrDefault()?.Value);
    }

    // ── Context payload models ───────────────────────────────────────

    private sealed class ContextPayload
    {
        public PortfolioContext? Portfolio { get; set; }
        public AssetContext? Asset { get; set; }
    }

    private sealed record PortfolioContext(
        string Name,
        string BaseCurrency,
        decimal TotalCost,
        decimal TotalValue,
        decimal PnL,
        decimal PnLPct,
        List<PositionContext> Positions);

    private sealed record PositionContext(
        string AssetId,
        decimal Qty,
        decimal AvgCost,
        decimal CurrentValue,
        decimal PnL,
        decimal PnLPct);

    private sealed record AssetContext(
        string Symbol,
        decimal Price,
        decimal Change,
        decimal ChangePct,
        string Status,
        decimal? RsiValue,
        decimal? MacdValue);
}