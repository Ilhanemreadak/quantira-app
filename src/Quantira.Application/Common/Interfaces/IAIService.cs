namespace Quantira.Application.Common.Interfaces;

/// <summary>
/// Abstracts AI model interactions from the application layer.
/// The infrastructure implementation in <c>Quantira.Infrastructure.AI</c>
/// currently targets the Claude API but can be swapped for any other
/// provider (OpenAI, Gemini, local model) without touching application code.
/// All methods accept a <c>portfolioContext</c> string — a JSON-serialised
/// snapshot of the user's relevant portfolio and asset data built by
/// <c>PortfolioContextBuilder</c> — so the AI has the financial context
/// it needs to give grounded, personalized responses.
/// </summary>
public interface IAIService
{
    /// <summary>
    /// Sends a question to the AI model with the given portfolio context
    /// and returns the complete response as a single string.
    /// Use for short interactions where streaming is not required.
    /// </summary>
    /// <param name="portfolioContext">
    /// JSON snapshot of the user's portfolio, selected asset, latest
    /// price, and recent news. Built by <c>PortfolioContextBuilder</c>.
    /// </param>
    /// <param name="question">The user's question.</param>
    Task<string> GetAdviceAsync(
        string portfolioContext,
        string question,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams the AI response token by token via an async enumerable.
    /// Each yielded string is a partial token chunk that should be
    /// appended to the UI in real time via SignalR.
    /// </summary>
    /// <param name="portfolioContext">
    /// JSON snapshot of the user's relevant financial context.
    /// </param>
    /// <param name="question">The user's question.</param>
    IAsyncEnumerable<string> StreamAdviceAsync(
        string portfolioContext,
        string question,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Analyzes the sentiment of a news article in the context of
    /// the given asset symbol. Returns a score between -1.0 (very negative)
    /// and +1.0 (very positive) along with a one-sentence summary.
    /// Used by <c>NewsIngestionJob</c> to enrich news records.
    /// </summary>
    Task<NewsSentimentResult> AnalyzeSentimentAsync(
        string newsText,
        string symbol,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Carries the result of a news sentiment analysis request.
/// </summary>
/// <param name="Score">
/// Sentiment score from -1.0 (strongly negative) to +1.0 (strongly positive).
/// </param>
/// <param name="Label">Human-readable label: "Positive", "Neutral", or "Negative".</param>
/// <param name="Summary">One-sentence AI-generated summary of the article.</param>
public sealed record NewsSentimentResult(
    double Score,
    string Label,
    string Summary);