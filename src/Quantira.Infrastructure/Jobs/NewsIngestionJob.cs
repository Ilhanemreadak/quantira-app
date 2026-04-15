using Microsoft.Extensions.Logging;
using Quantira.Application.Common.Interfaces;
using Quantira.Domain.Interfaces;

namespace Quantira.Infrastructure.Jobs;

/// <summary>
/// Hangfire recurring job that fetches financial news for all actively
/// tracked asset symbols every 30 minutes. Each article is run through
/// the AI sentiment analysis pipeline and the enriched result is cached
/// in Redis for the news feed widget on the dashboard.
/// Only the top 50 most-held symbols are processed per cycle to stay
/// within NewsAPI rate limits on the free tier.
/// Full news persistence to MongoDB is handled by a separate
/// archival job (to be added in a future phase).
/// </summary>
public sealed class NewsIngestionJob
{
    private readonly IAssetRepository _assetRepository;
    private readonly IAIService _aiService;
    private readonly ICacheService _cache;
    private readonly ILogger<NewsIngestionJob> _logger;

    private static readonly TimeSpan NewsTtl = TimeSpan.FromMinutes(35);
    private const int MaxSymbols = 50;

    public NewsIngestionJob(
        IAssetRepository assetRepository,
        IAIService aiService,
        ICacheService cache,
        ILogger<NewsIngestionJob> logger)
    {
        _assetRepository = assetRepository;
        _aiService = aiService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Fetches and enriches news for the most actively tracked symbols.
    /// </summary>
    public async Task IngestNewsAsync()
    {
        _logger.LogInformation("[NewsIngestionJob] Starting news ingestion cycle.");

        var assets = await _assetRepository.GetAllActiveAsync();

        var symbols = assets
            .Take(MaxSymbols)
            .Select(a => a.Symbol)
            .ToList();

        var processed = 0;

        foreach (var symbol in symbols)
        {
            try
            {
                // Placeholder: replace with real NewsAPI or RSS feed call.
                // For now we log the intent and cache an empty result.
                var cacheKey = $"news:{symbol}";

                _logger.LogDebug(
                    "[NewsIngestionJob] Processing news for {Symbol}", symbol);

                // TODO: Call NewsAPI provider, run sentiment analysis,
                // cache enriched articles.
                // var articles = await _newsProvider.GetArticlesAsync(symbol);
                // foreach (var article in articles)
                // {
                //     var sentiment = await _aiService.AnalyzeSentimentAsync(
                //         article.Content, symbol);
                //     enriched.Add(article with { Sentiment = sentiment });
                // }
                // await _cache.SetAsync(cacheKey, enriched, NewsTtl);

                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[NewsIngestionJob] Failed to ingest news for {Symbol}", symbol);
            }
        }

        _logger.LogInformation(
            "[NewsIngestionJob] Cycle complete. Processed {Count}/{Total} symbols.",
            processed, symbols.Count);
    }
}