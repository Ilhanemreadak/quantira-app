namespace Quantira.Application.Portfolios.DTOs;

/// <summary>
/// Lightweight portfolio representation returned by
/// <c>GetPortfolioListQueryHandler</c>. Contains only the metadata
/// needed to populate the portfolio switcher and navigation sidebar.
/// Does not include positions, trades or valuation data.
/// </summary>
/// <param name="Id">Unique identifier of the portfolio.</param>
/// <param name="Name">Display name.</param>
/// <param name="BaseCurrency">ISO 4217 base currency code.</param>
/// <param name="CostMethod">Inventory cost method in use.</param>
/// <param name="IsDefault">Whether this is the user's default portfolio.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
public sealed record PortfolioDto(
    Guid Id,
    string Name,
    string BaseCurrency,
    string CostMethod,
    bool IsDefault,
    DateTime CreatedAt);