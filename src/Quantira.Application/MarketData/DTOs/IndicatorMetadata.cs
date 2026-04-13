using Quantira.Domain.Enums;

namespace Quantira.Application.MarketData.DTOs;

/// <summary>
/// Describes a registered technical indicator.
/// Returned by <c>IIndicatorEngine.GetAvailableIndicators()</c>
/// and used to populate the indicator selector dropdown in the frontend.
/// </summary>
/// <param name="Name">Unique identifier used in API requests (e.g. "RSI").</param>
/// <param name="DisplayName">Human-readable label (e.g. "Relative Strength Index").</param>
/// <param name="Description">Short explanation of what the indicator measures.</param>
/// <param name="Category">Grouping for the UI selector (e.g. "Momentum", "Trend").</param>
/// <param name="MinimumPeriod">Minimum number of candles required for calculation.</param>
/// <param name="DefaultParameters">
/// Default key-value parameter map shown in the indicator settings panel.
/// </param>
/// <param name="SupportedAssetTypes">
/// Asset types this indicator can be applied to.
/// Some indicators (e.g. On-Balance Volume) are only meaningful for assets with volume data.
/// </param>
public sealed record IndicatorMetadata(
    string Name,
    string DisplayName,
    string Description,
    string Category,
    int MinimumPeriod,
    Dictionary<string, string> DefaultParameters,
    IReadOnlyList<AssetType> SupportedAssetTypes);