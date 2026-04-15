namespace Quantira.Application.Common.Interfaces;

/// <summary>
/// Abstracts distributed cache operations over the underlying Redis store.
/// Keeps the application layer decoupled from the StackExchange.Redis
/// implementation details. All keys are namespaced by the infrastructure
/// implementation to avoid collisions across different Quantira services.
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached value by key and deserializes it to
    /// <typeparamref name="T"/>. Returns <c>null</c> if the key does
    /// not exist or has expired.
    /// </summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes <paramref name="value"/> and stores it under
    /// <paramref name="key"/> with the given TTL.
    /// Overwrites any existing value at that key.
    /// </summary>
    Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiry = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the entry at <paramref name="key"/> if it exists.
    /// No-op if the key is not found.
    /// </summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes all cache entries whose keys match the given prefix pattern.
    /// Used to invalidate a group of related entries (e.g. all keys for
    /// a specific portfolio: <c>"portfolio:{id}:*"</c>).
    /// </summary>
    Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <c>true</c> if a non-expired entry exists for the given key.
    /// </summary>
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
}