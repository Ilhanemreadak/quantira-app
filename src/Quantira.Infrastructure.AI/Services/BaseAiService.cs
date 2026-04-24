using System.Text.Json;
using Microsoft.Extensions.Logging;
using Quantira.Application.Common.Interfaces;

namespace Quantira.Infrastructure.AI.Services;

/// <summary>
/// Shared base for all AI provider implementations.
/// Eliminates duplication of: system prompt, sentiment analysis,
/// sentiment JSON parsing, and markdown fence stripping.
/// Subclasses implement only provider-specific HTTP transport.
/// </summary>
public abstract class BaseAiService : IAIService
{
    private static readonly JsonSerializerOptions SentimentParseOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string SystemPromptBase =
        """
        You are Quantira, an AI-powered financial assistant embedded in a
        portfolio tracking application. Your role is to help users understand
        their portfolio performance, analyze market data, and make informed
        investment decisions.

        Guidelines:
        - Always base your analysis on the provided portfolio context data.
        - Be concise, factual, and data-driven in your responses.
        - Always include this disclaimer when giving investment-related advice:
          "This is not investment advice. Always do your own research."
        - Format numbers clearly (e.g. use % for percentages, currency symbols).
        - If the user asks about a specific asset, focus on that asset's data.
        - Do not fabricate market data — only use what is provided in the context.
        """;

    private readonly ILogger _logger;
    private readonly string _providerName;

    protected BaseAiService(ILogger logger, string providerName)
    {
        _logger = logger;
        _providerName = providerName;
    }

    public abstract Task<string> GetAdviceAsync(
        string portfolioContext,
        string question,
        CancellationToken cancellationToken = default);

    public abstract IAsyncEnumerable<string> StreamAdviceAsync(
        string portfolioContext,
        string question,
        CancellationToken cancellationToken = default);

    public async Task<NewsSentimentResult> AnalyzeSentimentAsync(
        string newsText,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var prompt =
            $"Analyze the sentiment of this financial news article about {symbol}. " +
            $"Respond ONLY with a JSON object in this exact format: " +
            $"{{\"score\": <number between -1.0 and 1.0>, " +
            $"\"label\": \"Positive|Neutral|Negative\", " +
            $"\"summary\": \"<one sentence summary>\"}}. " +
            $"Article: {newsText[..Math.Min(newsText.Length, 1500)]}";

        var result = await GetAdviceAsync(string.Empty, prompt, cancellationToken);

        try
        {
            var clean = result.Replace("```json", "").Replace("```", "").Trim();
            var parsed = JsonSerializer.Deserialize<SentimentJson>(clean, SentimentParseOpts);

            return new NewsSentimentResult(
                Score: parsed?.Score ?? 0,
                Label: parsed?.Label ?? "Neutral",
                Summary: parsed?.Summary ?? string.Empty);
        }
        catch
        {
            _logger.LogWarning(
                "[{Provider}] Failed to parse sentiment response for {Symbol}. Raw: {Response}",
                _providerName, symbol, result[..Math.Min(result.Length, 200)]);

            return new NewsSentimentResult(0, "Neutral", string.Empty);
        }
    }

    protected static string BuildSystemPrompt(string context) =>
        string.IsNullOrWhiteSpace(context)
            ? SystemPromptBase
            : $"{SystemPromptBase}\n\n## Current Portfolio Context\n{context}";

    protected sealed record SentimentJson(double Score, string Label, string Summary);
}
