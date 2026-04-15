using Quantira.Domain.Common;
using Quantira.Domain.Enums;
using Quantira.Domain.Events;
using Quantira.Domain.Exceptions;

namespace Quantira.Domain.Entities;

/// <summary>
/// Represents a tradeable financial instrument tracked in Quantira.
/// Assets are shared reference data — they are not owned by any user
/// or portfolio. A single asset record is reused across all portfolios
/// that hold it. Supports stocks (BIST, NYSE), cryptocurrencies,
/// commodities (gold, silver, oil), currencies and funds.
/// The <see cref="DataProviderKey"/> maps the Quantira symbol to the
/// external API symbol (e.g. "THYAO" → "THYAO.IS" for Yahoo Finance).
/// </summary>
public sealed class Asset : AggregateRoot<Guid>
{
    /// <summary>
    /// Normalized ticker symbol in uppercase (e.g. "THYAO", "BTC", "XAU/USD").
    /// Unique across all asset types. Used as the primary lookup key
    /// in market data provider calls and Redis cache keys.
    /// </summary>
    public string Symbol { get; private set; } = default!;

    /// <summary>Full display name of the asset (e.g. "Turk Hava Yollari", "Bitcoin").</summary>
    public string Name { get; private set; } = default!;

    /// <summary>
    /// The category of this asset. Determines which market data provider
    /// is used and which business rules apply (e.g. 8-decimal precision for crypto).
    /// </summary>
    public AssetType AssetType { get; private set; }

    /// <summary>
    /// The exchange this asset is listed on (e.g. "BIST", "NYSE", "BINANCE").
    /// <c>null</c> for commodities and currency pairs which are not
    /// exchange-specific.
    /// </summary>
    public string? Exchange { get; private set; }

    /// <summary>
    /// ISO 4217 currency code the asset is priced in (e.g. "TRY", "USD").
    /// Used for cross-currency portfolio valuation.
    /// </summary>
    public string Currency { get; private set; } = default!;

    /// <summary>
    /// GICS sector classification (e.g. "Technology", "Financials").
    /// Populated for stocks; <c>null</c> for crypto and commodities.
    /// Used for sector allocation reports.
    /// </summary>
    public string? Sector { get; private set; }

    /// <summary>
    /// The symbol or key used when querying the external market data provider.
    /// Examples: "THYAO.IS" (Yahoo Finance), "BTCUSDT" (Binance), "XAU" (GoldApi).
    /// <c>null</c> until the asset is mapped to a provider.
    /// </summary>
    public string? DataProviderKey { get; private set; }

    /// <summary>
    /// Indicates whether this asset is actively tracked for price updates.
    /// Inactive assets are excluded from <c>MarketDataRefreshJob</c>
    /// but retained for historical trade records.
    /// </summary>
    public bool IsActive { get; private set; }

    private Asset() { }

    /// <summary>
    /// Creates a new <see cref="Asset"/> with the given properties.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="symbol"/> or <paramref name="name"/> is null or empty,
    /// or when <paramref name="currency"/> is not a valid 3-letter ISO code.
    /// </exception>
    public static Asset Create(
        string symbol,
        string name,
        AssetType assetType,
        string currency,
        string? exchange = null,
        string? sector = null,
        string? dataProviderKey = null)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new DomainException("Asset symbol cannot be empty.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Asset name cannot be empty.");

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new DomainException($"'{currency}' is not a valid ISO 4217 currency code.");

        var asset = new Asset
        {
            Id = Guid.NewGuid(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            AssetType = assetType,
            Currency = currency.Trim().ToUpperInvariant(),
            Exchange = exchange?.Trim().ToUpperInvariant(),
            Sector = sector?.Trim(),
            DataProviderKey = dataProviderKey?.Trim(),
            IsActive = true
        };

        return asset;
    }

    /// <summary>
    /// Updates the external data provider key for this asset.
    /// Called during asset onboarding when the provider mapping is confirmed.
    /// </summary>
    public void SetDataProviderKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new DomainException("Data provider key cannot be empty.");

        DataProviderKey = key.Trim();
        MarkUpdated();
    }

    /// <summary>
    /// Updates catalogue-managed metadata when external provider mapping
    /// changes (e.g., display name normalization, provider symbol remap).
    /// Returns <c>true</c> if any field changed.
    /// </summary>
    public bool UpdateCatalogueMetadata(string name, string? dataProviderKey)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Asset name cannot be empty.");

        var normalizedName = name.Trim();
        var normalizedProviderKey = dataProviderKey?.Trim();

        var hasChanged = false;

        if (!string.Equals(Name, normalizedName, StringComparison.Ordinal))
        {
            Name = normalizedName;
            hasChanged = true;
        }

        if (!string.Equals(DataProviderKey, normalizedProviderKey, StringComparison.Ordinal))
        {
            DataProviderKey = normalizedProviderKey;
            hasChanged = true;
        }

        if (hasChanged)
            MarkUpdated();

        return hasChanged;
    }

    /// <summary>
    /// Deactivates this asset. It will no longer receive price updates
    /// but its historical data and trade records are preserved.
    /// </summary>
    public void Deactivate()
    {
        if (!IsActive)
            throw new DomainException($"Asset '{Symbol}' is already inactive.");

        IsActive = false;
        AddDomainEvent(new AssetPriceUpdatedEvent(Id, Symbol, 0, 0, Currency));
        MarkDeleted();
    }

    /// <summary>Reactivates a previously deactivated asset.</summary>
    public void Reactivate()
    {
        IsActive = true;
        DeletedAt = null;
        MarkUpdated();
    }
}