using Microsoft.AspNetCore.Identity;

namespace Quantira.Infrastructure.Persistence;

/// <summary>
/// Extends the default ASP.NET Core Identity user with Quantira-specific
/// profile fields. Stored in the <c>Users</c> table alongside the standard
/// Identity columns (email, password hash, lockout, etc.).
/// All financial preferences are stored here so they travel with the user
/// across portfolios and do not need to be loaded separately on each request.
/// </summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>User's full display name.</summary>
    public string FullName { get; set; } = default!;

    /// <summary>
    /// ISO 4217 preferred currency for portfolio valuation display.
    /// Defaults to "TRY". Users can change this in their profile settings.
    /// </summary>
    public string PreferredCurrency { get; set; } = "TRY";

    /// <summary>
    /// Default cost method applied when creating new portfolios.
    /// Defaults to FIFO. Individual portfolios can override this.
    /// </summary>
    public string DefaultCostMethod { get; set; } = "Fifo";

    /// <summary>UTC timestamp of account creation.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC timestamp of last profile update.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// UTC timestamp of soft-deletion. Null means the account is active.
    /// Soft-deleted users cannot log in and are excluded from all queries.
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}