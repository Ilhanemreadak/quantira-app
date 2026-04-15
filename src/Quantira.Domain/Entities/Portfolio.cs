using Quantira.Domain.Common;
using Quantira.Domain.Enums;
using Quantira.Domain.Events;
using Quantira.Domain.Exceptions;
using Quantira.Domain.ValueObjects;

namespace Quantira.Domain.Entities;

/// <summary>
/// The central aggregate root of the Quantira domain.
/// A portfolio groups a user's financial positions and trade history
/// under a single named container with a defined base currency and
/// cost calculation method. All trade recording and position management
/// must go through this aggregate — never directly through
/// <see cref="Position"/> or <see cref="Trade"/> entities.
/// Supports multiple portfolios per user (e.g. "Stocks", "Crypto", "Commodities").
/// </summary>
public sealed class Portfolio : AggregateRoot<Guid>
{
    /// <summary>The owner of this portfolio.</summary>
    public Guid UserId { get; private set; }

    /// <summary>Display name. Unique per user.</summary>
    public string Name { get; private set; } = default!;

    /// <summary>Optional description for the portfolio.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// The currency all P&amp;L and valuation figures are expressed in.
    /// Individual trades may be in different currencies but are converted
    /// to this base currency for reporting.
    /// </summary>
    public Currency BaseCurrency { get; private set; } = default!;

    /// <summary>
    /// The inventory cost method applied to all sell trades in this portfolio.
    /// Changing this value triggers a full position recalculation.
    /// </summary>
    public CostMethod CostMethod { get; private set; }

    /// <summary>
    /// Marks this as the user's default portfolio shown on first login.
    /// Only one portfolio per user can be the default at any time.
    /// </summary>
    public bool IsDefault { get; private set; }

    /// <summary>Whether this portfolio is active and visible to the user.</summary>
    public bool IsActive { get; private set; }

    private readonly List<Position> _positions = [];
    private readonly List<Trade> _trades = [];

    /// <summary>Current open positions in this portfolio.</summary>
    public IReadOnlyCollection<Position> Positions => _positions.AsReadOnly();

    /// <summary>All trade records associated with this portfolio.</summary>
    public IReadOnlyCollection<Trade> Trades => _trades.AsReadOnly();

    private Portfolio() { }

    /// <summary>
    /// Creates a new portfolio for the given user.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="name"/> is null or empty.
    /// </exception>
    public static Portfolio Create(
        Guid userId,
        string name,
        Currency baseCurrency,
        CostMethod costMethod = CostMethod.Fifo,
        string? description = null,
        bool isDefault = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Portfolio name cannot be empty.");

        var portfolio = new Portfolio
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name.Trim(),
            Description = description?.Trim(),
            BaseCurrency = baseCurrency,
            CostMethod = costMethod,
            IsDefault = isDefault,
            IsActive = true
        };

        portfolio.AddDomainEvent(new PortfolioCreatedEvent(portfolio.Id, userId, name));
        return portfolio;
    }

    /// <summary>
    /// Records a new trade against this portfolio and updates the
    /// corresponding position. Position is created if it does not exist.
    /// Raises <see cref="TradeAddedEvent"/> for downstream handlers.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when the portfolio is inactive or soft-deleted,
    /// or when quantity/price violate business rules.
    /// </exception>
    public Trade AddTrade(
        Asset asset,
        TradeType tradeType,
        decimal quantity,
        decimal price,
        string priceCurrency,
        decimal commission = 0m,
        decimal taxAmount = 0m,
        DateTime? tradedAt = null,
        string? notes = null)
    {
        if (!IsActive || IsDeleted)
            throw new DomainException($"Cannot add a trade to inactive portfolio '{Name}'.");

        if (quantity <= 0)
            throw new DomainException($"Trade quantity must be positive. Received: {quantity}");

        if (price < 0)
            throw new DomainException($"Trade price cannot be negative. Received: {price}");

        var trade = Trade.Create(
            portfolioId: Id,
            assetId: asset.Id,
            tradeType: tradeType,
            quantity: quantity,
            price: price,
            priceCurrency: priceCurrency,
            commission: commission,
            taxAmount: taxAmount,
            tradedAt: tradedAt ?? DateTime.UtcNow,
            notes: notes);

        _trades.Add(trade);
        UpdatePosition(asset, trade);
        MarkUpdated();

        AddDomainEvent(new TradeAddedEvent(
            portfolioId: Id,
            assetId: asset.Id,
            tradeId: trade.Id,
            tradeType: tradeType,
            quantity: quantity,
            price: price,
            priceCurrency: priceCurrency));

        return trade;
    }

    /// <summary>
    /// Updates the display name of this portfolio.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="newName"/> is null or empty.
    /// </exception>
    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new DomainException("Portfolio name cannot be empty.");

        Name = newName.Trim();
        MarkUpdated();
    }

    /// <summary>
    /// Marks this as the user's default portfolio.
    /// The caller (command handler) is responsible for unsetting
    /// the previous default before calling this method.
    /// </summary>
    public void SetAsDefault()
    {
        IsDefault = true;
        MarkUpdated();
    }

    /// <summary>Removes the default flag from this portfolio.</summary>
    public void UnsetDefault()
    {
        IsDefault = false;
        MarkUpdated();
    }

    /// <summary>
    /// Soft-deletes this portfolio. All positions and trade records
    /// are retained for audit and compliance purposes.
    /// </summary>
    /// <exception cref="DomainException">
    /// Thrown when attempting to delete the user's last active portfolio.
    /// </exception>
    public void Delete()
    {
        if (!IsActive)
            throw new DomainException($"Portfolio '{Name}' is already deleted.");

        IsActive = false;
        MarkDeleted();
    }

    // ── Private helpers ─────────────────────────────────────────────

    private void UpdatePosition(Asset asset, Trade trade)
    {
        var position = _positions.FirstOrDefault(p => p.AssetId == asset.Id);

        if (position is null)
        {
            position = Position.Create(Id, asset.Id, asset.Currency);
            _positions.Add(position);
        }

        position.ApplyTrade(trade, CostMethod);
    }
}