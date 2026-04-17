using MediatR;
using Quantira.Application.Common.Interfaces;
using Quantira.Domain.Exceptions;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Portfolios.Commands.AddTrade;

/// <summary>
/// Handles <see cref="AddTradeCommand"/>.
/// Loads the portfolio aggregate with its positions, delegates trade
/// recording to <see cref="Domain.Entities.Portfolio.AddTrade"/>,
/// and invalidates the related Redis cache entries.
/// The domain aggregate raises <c>TradeAddedEvent</c> internally —
/// this handler does not need to know about downstream side effects.
/// <c>TransactionBehavior</c> commits all changes after this returns.
/// </summary>
public sealed class AddTradeCommandHandler : IRequestHandler<AddTradeCommand, Guid>
{
    private readonly IPortfolioRepository _portfolioRepository;
    private readonly IAssetRepository _assetRepository;
    private readonly ICacheService _cache;

    public AddTradeCommandHandler(
        IPortfolioRepository portfolioRepository,
        IAssetRepository assetRepository,
        ICacheService cache)
    {
        _portfolioRepository = portfolioRepository;
        _assetRepository = assetRepository;
        _cache = cache;
    }

    public async Task<Guid> Handle(
        AddTradeCommand command,
        CancellationToken cancellationToken)
    {
        var portfolio = await _portfolioRepository
            .GetWithPositionsAsync(command.PortfolioId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Portfolio), command.PortfolioId);

        var asset = await _assetRepository
            .GetByIdAsync(command.AssetId, cancellationToken)
            ?? throw new NotFoundException(nameof(Domain.Entities.Asset), command.AssetId);

        var trade = portfolio.AddTrade(
            asset: asset,
            tradeType: command.TradeType,
            quantity: command.Quantity,
            price: command.Price,
            priceCurrency: command.PriceCurrency,
            commission: command.Commission,
            taxAmount: command.TaxAmount,
            tradedAt: command.TradedAt,
            notes: command.Notes);

        await _cache.RemoveByPrefixAsync(
            $"quantira:portfolio:{command.PortfolioId}",
            cancellationToken);

        return trade.Id;
    }
}