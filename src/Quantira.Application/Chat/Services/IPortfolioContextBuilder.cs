namespace Quantira.Application.Chat.Services;

/// <summary>
/// Builds a JSON-serialised context snapshot of the user's financial data
/// to be included in every AI request. The context gives the AI model
/// the information it needs to provide grounded, personalised responses
/// without the user having to repeat their portfolio details in every message.
/// Implemented in <c>Quantira.Infrastructure.AI</c> to keep the data
/// access logic (repository calls, Redis reads) out of the application layer.
/// </summary>
public interface IPortfolioContextBuilder
{
    /// <summary>
    /// Builds the context string for the given combination of user,
    /// portfolio and asset. Any combination of the optional parameters
    /// is valid — the builder includes only the data that is available.
    /// </summary>
    /// <param name="userId">Always required — used for user preferences.</param>
    /// <param name="portfolioId">
    /// When provided, includes portfolio summary, positions, total value,
    /// unrealized P&amp;L and allocation breakdown.
    /// </param>
    /// <param name="assetId">
    /// When provided, includes latest price, 30-day price change,
    /// RSI and MACD values, and the 5 most recent news headlines
    /// with their sentiment scores for this asset.
    /// </param>
    /// <returns>
    /// A compact JSON string ready to be injected into the AI system prompt.
    /// Intentionally concise to stay within the model's context window budget.
    /// </returns>
    Task<string> BuildAsync(
        Guid userId,
        Guid? portfolioId = null,
        Guid? assetId = null,
        CancellationToken cancellationToken = default);
}