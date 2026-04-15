using Quantira.Domain.Enums;

namespace Quantira.Application.Assets.DTOs;

/// <summary>
/// Read model for a financial asset. Returned by asset queries
/// and included as metadata in position and trade responses.
/// </summary>
/// <param name="Id">Unique identifier.</param>
/// <param name="Symbol">Normalized uppercase ticker symbol.</param>
/// <param name="Name">Full display name.</param>
/// <param name="AssetType">Category of the asset.</param>
/// <param name="Exchange">Exchange code. Null for commodities and FX.</param>
/// <param name="Currency">ISO 4217 pricing currency.</param>
/// <param name="Sector">GICS sector. Null for non-equity assets.</param>
/// <param name="IsActive">Whether the asset is actively tracked.</param>
public sealed record AssetDto(
    Guid Id,
    string Symbol,
    string Name,
    AssetType AssetType,
    string? Exchange,
    string Currency,
    string? Sector,
    bool IsActive);