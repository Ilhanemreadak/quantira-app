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
/// Google Gemini API implementation of <see cref="IAIService"/>.
/// Uses Gemini 1.5 Pro model with system instructions and streaming support.
/// </summary>
public sealed class GeminiAIService : IAIService
{
    private const string BaseApiUrl = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro-latest";
    private const int MaxTokens = 1024;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, // Gemini uses camelCase
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiAIService> _logger;

    public GeminiAIService(
        HttpClient httpClient,
        IOptions<GeminiOptions> options,
        ILogger<GeminiAIService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _options.ApiKey);
    }

    /// <inheritdoc/>
    public async Task<string> GetAdviceAsync(
        string portfolioContext,
        string question,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(portfolioContext, question);
        var url = $"{BaseApiUrl}:generateContent";

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                url, request, JsonOpts, cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<GeminiResponse>(JsonOpts, cancellationToken);

            return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Gemini] GetAdviceAsync failed for question: {Question}",
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
        var request = BuildRequest(portfolioContext, question);
        var url = $"{BaseApiUrl}:streamGenerateContent?alt=sse";

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
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
            _logger.LogError(ex, "[Gemini] StreamAdviceAsync failed.");
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

            GeminiResponse? evt;

            try
            {
                evt = JsonSerializer.Deserialize<GeminiResponse>(data, JsonOpts);
            }
            catch
            {
                continue;
            }

            var textDelta = evt?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
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
            var cleanResult = result.Replace("```json", "").Replace("```", "").Trim();

            // System.Text.Json CamelCase'i destekleyecek şekilde ayarlandı (JsonOpts üzerinden)
            var parsed = JsonSerializer.Deserialize<SentimentJson>(cleanResult, JsonOpts);

            return new NewsSentimentResult(
                Score: parsed?.Score ?? 0,
                Label: parsed?.Label ?? "Neutral",
                Summary: parsed?.Summary ?? string.Empty);
        }
        catch
        {
            _logger.LogWarning(
                "[Gemini] Failed to parse sentiment response for {Symbol}. " +
                "Raw response: {Response}",
                symbol, result[..Math.Min(result.Length, 200)]);

            return new NewsSentimentResult(0, "Neutral", string.Empty);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static GeminiRequest BuildRequest(
        string context,
        string question)
    {
        var systemPrompt = string.IsNullOrWhiteSpace(context)
            ? BuildBaseSystemPrompt()
            : $"{BuildBaseSystemPrompt()}\n\n## Current Portfolio Context\n{context}";

        return new GeminiRequest(
            SystemInstruction: new GeminiContent(
                Parts: [new GeminiPart(systemPrompt)]
            ),
            Contents:
            [
                new GeminiContent(
                    Role: "user",
                    Parts: [new GeminiPart(question)]
                )
            ],
            GenerationConfig: new GeminiGenerationConfig(MaxOutputTokens: MaxTokens)
        );
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

    private sealed record GeminiRequest(
        GeminiContent SystemInstruction,
        List<GeminiContent> Contents,
        GeminiGenerationConfig GenerationConfig);

    private sealed record GeminiGenerationConfig(int MaxOutputTokens);

    private sealed record GeminiContent(
        List<GeminiPart> Parts,
        string? Role = null);

    private sealed record GeminiPart(string Text);

    private sealed record GeminiResponse(List<GeminiCandidate>? Candidates);

    private sealed record GeminiCandidate(GeminiContent? Content);

    private sealed record SentimentJson(
        double Score,
        string Label,
        string Summary);
}

/// <summary>
/// Configuration options for the Gemini AI service.
/// </summary>
public sealed class GeminiOptions
{
    public string ApiKey { get; set; } = string.Empty;
}