using MediatR;
using Microsoft.Extensions.Logging;
using Quantira.Application.Common.Interfaces;

namespace Quantira.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that provides transparent Redis caching
/// for queries that implement <see cref="ICacheableQuery"/>.
/// On each request it first checks Redis for a cached result.
/// On a cache hit the handler is bypassed entirely, giving sub-millisecond
/// response times for expensive queries such as portfolio summaries and
/// indicator calculations.
/// On a cache miss the handler runs normally and its result is stored
/// in Redis with the TTL defined by the query itself.
/// Commands never implement <see cref="ICacheableQuery"/> and pass through
/// this behavior without any overhead.
/// </summary>
/// <typeparam name="TRequest">The query type.</typeparam>
/// <typeparam name="TResponse">The handler response type.</typeparam>
public sealed class CachingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICacheService _cache;
    private readonly ILogger<CachingBehavior<TRequest, TResponse>> _logger;

    public CachingBehavior(
        ICacheService cache,
        ILogger<CachingBehavior<TRequest, TResponse>> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is not ICacheableQuery cacheableQuery)
            return await next();

        var key = cacheableQuery.CacheKey;

        var cached = await _cache.GetAsync<TResponse>(key, cancellationToken);

        if (cached is not null)
        {
            _logger.LogDebug(
                "[CACHE HIT] {RequestName} key={CacheKey}",
                typeof(TRequest).Name, key);

            return cached;
        }

        _logger.LogDebug(
            "[CACHE MISS] {RequestName} key={CacheKey}",
            typeof(TRequest).Name, key);

        var response = await next();

        await _cache.SetAsync(key, response, cacheableQuery.CacheDuration, cancellationToken);

        return response;
    }
}

/// <summary>
/// Marker interface for queries whose results should be cached in Redis.
/// Implement this on any query record to opt in to the caching pipeline.
/// </summary>
public interface ICacheableQuery
{
    /// <summary>
    /// The Redis cache key for this query instance.
    /// Should be unique per set of query parameters.
    /// Convention: <c>"quantira:{entity}:{operation}:{id}"</c>
    /// Example:    <c>"quantira:portfolio:summary:3f2a1b"</c>
    /// </summary>
    string CacheKey { get; }

    /// <summary>
    /// How long the cached result remains valid.
    /// <c>null</c> uses the global default TTL configured in
    /// <c>RedisCacheService</c> (currently 60 seconds).
    /// </summary>
    TimeSpan? CacheDuration { get; }
}