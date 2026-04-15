using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.Common.Interfaces;
using StackExchange.Redis;

namespace Quantira.Infrastructure.Cache;

/// <summary>
/// Redis implementation of <see cref="ICacheService"/>.
/// Uses StackExchange.Redis for all cache operations.
/// All keys are automatically namespaced with the application prefix
/// to prevent collisions when multiple applications share a Redis instance.
/// Serialization uses System.Text.Json for performance.
/// All operations are wrapped in try-catch blocks so a Redis failure
/// never propagates to the application layer — cache misses are returned
/// instead, allowing the system to degrade gracefully.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IDatabase _database;
    private readonly IServer _server;
    private readonly RedisCacheOptions _options;
    private readonly ILogger<RedisCacheService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public RedisCacheService(
        IConnectionMultiplexer redis,
        IOptions<RedisCacheOptions> options,
        ILogger<RedisCacheService> logger)
    {
        _database = redis.GetDatabase();
        _server = redis.GetServer(redis.GetEndPoints().First());
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await _database.StringGetAsync(BuildKey(key));

            if (value.IsNullOrEmpty)
                return default;

            return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Redis] GET failed for key {Key} — returning cache miss.",
                key);
            return default;
        }
    }

    /// <inheritdoc/>
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serialized = JsonSerializer.Serialize(value, JsonOptions);
            var ttl = expiry ?? _options.DefaultExpiry;

            await _database.StringSetAsync(
                BuildKey(key),
                serialized,
                ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Redis] SET failed for key {Key} — continuing without cache.",
                key);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _database.KeyDeleteAsync(BuildKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Redis] DELETE failed for key {Key}.",
                key);
        }
    }

    /// <inheritdoc/>
    public async Task RemoveByPrefixAsync(
        string prefix,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = $"{_options.KeyPrefix}:{prefix}*";
            var keys = _server
                .Keys(pattern: pattern)
                .ToArray();

            if (keys.Length > 0)
                await _database.KeyDeleteAsync(keys);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Redis] REMOVE BY PREFIX failed for prefix {Prefix}.",
                prefix);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> ExistsAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _database.KeyExistsAsync(BuildKey(key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "[Redis] EXISTS failed for key {Key} — returning false.",
                key);
            return false;
        }
    }

    private string BuildKey(string key) => $"{_options.KeyPrefix}:{key}";
}
