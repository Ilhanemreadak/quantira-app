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
/// OpenAI API implementation of <see cref="IAIService"/>.
/// Uses OpenAI's Chat Completions API with streaming support.
/// </summary>
public sealed class OpenAIService : IAIService
{
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string Model = "gpt-4o"; // Or gpt-4-turbo
    private const int MaxTokens = 1024;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(
        HttpClient httpClient,
        IOptions<OpenAIOptions> options,
        ILogger<OpenAIService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
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
                .ReadFromJsonAsync<OpenAIResponse>(JsonOpts, cancellationToken);

            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[OpenAI] GetAdviceAsync failed for question: {Question}",
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
            _logger.LogError(ex, "[OpenAI] StreamAdviceAsync failed.");
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

            OpenAIStreamResponse? evt;

            try
            {
                evt = JsonSerializer.Deserialize<OpenAIStreamResponse>(data, JsonOpts);
            }
            catch
            {
                continue;
            }

            var textDelta = evt?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (textDelta is not null)
            {
                yield return textDelta;
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
            // Markdown block cleaning in case the model wraps the JSON in ```json ... ``` for formatting
            var cleanResult = result.Replace("```json", "").Replace("```", "").Trim();
            var parsed = JsonSerializer.Deserialize<SentimentJson>(cleanResult, JsonOpts);

            return new NewsSentimentResult(
                Score: parsed?.Score ?? 0,
                Label: parsed?.Label ?? "Neutral",
                Summary: parsed?.Summary ?? string.Empty);
        }
        catch
        {
            _logger.LogWarning(
                "[OpenAI] Failed to parse sentiment response for {Symbol}. " +
                "Raw response: {Response}",
                symbol, result[..Math.Min(result.Length, 200)]);

            return new NewsSentimentResult(0, "Neutral", string.Empty);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static OpenAIRequest BuildRequest(
        string context,
        string question,
        bool stream)
    {
        var systemPrompt = string.IsNullOrWhiteSpace(context)
            ? BuildBaseSystemPrompt()
            : $"{BuildBaseSystemPrompt()}\n\n## Current Portfolio Context\n{context}";

        return new OpenAIRequest(
            Model: Model,
            MaxTokens: MaxTokens,
            Stream: stream,
            Messages:
            [
                new OpenAIMessage(Role: "system", Content: systemPrompt),
                new OpenAIMessage(Role: "user", Content: question)
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

    private sealed record OpenAIRequest(
        string Model,
        int MaxTokens,
        bool Stream,
        List<OpenAIMessage> Messages);

    private sealed record OpenAIMessage(string Role, string Content);

    private sealed record OpenAIResponse(List<OpenAIChoice>? Choices);

    private sealed record OpenAIChoice(OpenAIMessage? Message);

    private sealed record OpenAIStreamResponse(List<OpenAIStreamChoice>? Choices);

    private sealed record OpenAIStreamChoice(OpenAIStreamDelta? Delta);

    private sealed record OpenAIStreamDelta(string? Content);

    private sealed record SentimentJson(
        double Score,
        string Label,
        string Summary);
}

/// <summary>
/// Configuration options for the OpenAI AI service.
/// </summary>
public sealed class OpenAIOptions
{
    public string ApiKey { get; set; } = string.Empty;
}