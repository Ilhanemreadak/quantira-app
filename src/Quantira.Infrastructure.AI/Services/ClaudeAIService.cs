using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.Common.Interfaces;

namespace Quantira.Infrastructure.AI.Services;

/// <summary>
/// Claude API implementation of <see cref="IAIService"/>.
/// Uses Anthropic's Messages API with streaming support.
/// Supports both full-response and token-by-token streaming modes.
/// The system prompt instructs Claude to act as a financial assistant
/// with awareness of the user's portfolio context.
/// Switching to a different AI provider requires only a new class
/// implementing <see cref="IAIService"/> and a DI registration change —
/// no application layer code changes needed.
/// </summary>
public sealed class ClaudeAIService : IAIService
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";
    private const string Model = "claude-opus-4-6";
    private const int MaxTokens = 1024;
    private const string ApiVersion = "2023-06-01";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ClaudeOptions _options;
    private readonly ILogger<ClaudeAIService> _logger;

    public ClaudeAIService(
        HttpClient httpClient,
        IOptions<ClaudeOptions> options,
        ILogger<ClaudeAIService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
    }

    /// <inheritdoc/>
    public async Task<string> GetAdviceAsync(
        string portfolioContext,
        string question,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(portfolioContext, question, stream: false);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                ApiUrl, request, JsonOpts, cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<ClaudeResponse>(JsonOpts, cancellationToken);

            return result?.Content?.FirstOrDefault()?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Claude] GetAdviceAsync failed for question: {Question}",
                question[..Math.Min(question.Length, 100)]);
            throw;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamAdviceAsync(
        string portfolioContext,
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(portfolioContext, question, stream: true);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(request, JsonOpts),
                Encoding.UTF8,
                "application/json")
        };

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.SendAsync(
                httpRequest,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Claude] StreamAdviceAsync failed.");
            yield break;
        }

        await using var stream = await response.Content
            .ReadAsStreamAsync(cancellationToken);

        using var reader = new System.IO.StreamReader(stream);

        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;

            if (string.IsNullOrWhiteSpace(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line["data: ".Length..];

            if (data == "[DONE]") break;

            ClaudeStreamEvent? evt;

            try
            {
                evt = JsonSerializer.Deserialize<ClaudeStreamEvent>(data, JsonOpts);
            }
            catch
            {
                continue;
            }

            if (evt?.Type == "content_block_delta"
                && evt.Delta?.Type == "text_delta"
                && evt.Delta.Text is not null)
            {
                yield return evt.Delta.Text;
            }
        }
    }

    /// <inheritdoc/>
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
            var parsed = JsonSerializer.Deserialize<SentimentJson>(result, JsonOpts);

            return new NewsSentimentResult(
                Score: parsed?.Score ?? 0,
                Label: parsed?.Label ?? "Neutral",
                Summary: parsed?.Summary ?? string.Empty);
        }
        catch
        {
            _logger.LogWarning(
                "[Claude] Failed to parse sentiment response for {Symbol}. " +
                "Raw response: {Response}",
                symbol, result[..Math.Min(result.Length, 200)]);

            return new NewsSentimentResult(0, "Neutral", string.Empty);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static ClaudeRequest BuildRequest(
        string context,
        string question,
        bool stream)
    {
        var systemPrompt = string.IsNullOrWhiteSpace(context)
            ? BuildBaseSystemPrompt()
            : $"{BuildBaseSystemPrompt()}\n\n## Current Portfolio Context\n{context}";

        return new ClaudeRequest(
            Model: Model,
            MaxTokens: MaxTokens,
            Stream: stream,
            System: systemPrompt,
            Messages:
            [
                new ClaudeMessage(Role: "user", Content: question)
            ]);
    }

    private static string BuildBaseSystemPrompt() =>
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

    // ── Internal request / response models ──────────────────────────

    private sealed record ClaudeRequest(
        string Model,
        int MaxTokens,
        bool Stream,
        string System,
        List<ClaudeMessage> Messages);

    private sealed record ClaudeMessage(string Role, string Content);

    private sealed record ClaudeResponse(List<ClaudeContent>? Content);

    private sealed record ClaudeContent(string? Type, string? Text);

    private sealed record ClaudeStreamEvent(
        string? Type,
        ClaudeDelta? Delta);

    private sealed record ClaudeDelta(string? Type, string? Text);

    private sealed record SentimentJson(
        double Score,
        string Label,
        string Summary);
}

/// <summary>
/// Configuration options for the Claude AI service.
/// Bound from the "Claude" section of appsettings.json.
/// The API key must be stored in User Secrets — never in appsettings.json.
/// </summary>
public sealed class ClaudeOptions
{
    /// <summary>
    /// Anthropic API key. Store via:
    /// <c>dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."</c>
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}