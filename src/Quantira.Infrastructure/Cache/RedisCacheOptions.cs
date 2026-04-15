namespace Quantira.Infrastructure.Cache;

/// <summary>
/// Configuration options for <see cref="RedisCacheService"/>.
/// Bind this from the "Redis" section in appsettings.json.
/// </summary>
public sealed class RedisCacheOptions
{
    /// <summary>
    /// Default TTL applied when no expiry is passed to <c>SetAsync</c>.
    /// </summary>
    public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Prefix prepended to every key to namespace this application's
    /// entries in a shared Redis instance.
    /// </summary>
    public string KeyPrefix { get; set; } = "quantira";
}
