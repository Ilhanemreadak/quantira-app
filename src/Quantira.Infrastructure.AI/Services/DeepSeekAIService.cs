using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.Common.Interfaces;

namespace Quantira.Infrastructure.AI.Services;

public sealed class DeepSeekAIService : IAIService
{
    private const string ApiUrl = "https://integrate.api.nvidia.com/v1/chat/completions";
    private const string Model = "deepseek-ai/deepseek-v3.2";
    private const int MaxTokens = 8192;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly DeepSeekOptions _options;
    private readonly ILogger<DeepSeekAIService> _logger;

    public DeepSeekAIService(
        HttpClient httpClient,
        IOptions<DeepSeekOptions> options,
        ILogger<DeepSeekAIService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Add(
            "Authorization", $"Bearer {_options.ApiKey}");
    }

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
                .ReadFromJsonAsync<DeepSeekResponse>(JsonOpts, cancellationToken);

            return result?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[DeepSeek] GetAdviceAsync failed for question: {Question}",
                question[..Math.Min(question.Length, 100)]);
            throw;
        }
    }

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
            _logger.LogError(ex, "[DeepSeek] StreamAdviceAsync failed.");
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

            DeepSeekStreamChunk? chunk;

            try
            {
                chunk = JsonSerializer.Deserialize<DeepSeekStreamChunk>(data, JsonOpts);
            }
            catch
            {
                continue;
            }

            var content = chunk?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (content is not null)
                yield return content;
        }
    }

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
                "[DeepSeek] Failed to parse sentiment response for {Symbol}. " +
                "Raw response: {Response}",
                symbol, result[..Math.Min(result.Length, 200)]);

            return new NewsSentimentResult(0, "Neutral", string.Empty);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private static DeepSeekRequest BuildRequest(
        string context,
        string question,
        bool stream)
    {
        var systemPrompt = string.IsNullOrWhiteSpace(context)
            ? BuildBaseSystemPrompt()
            : $"{BuildBaseSystemPrompt()}\n\n## Current Portfolio Context\n{context}";

        return new DeepSeekRequest(
            Model: Model,
            Messages:
            [
                new DeepSeekMessage(Role: "system", Content: systemPrompt),
                new DeepSeekMessage(Role: "user", Content: question)
            ],
            MaxTokens: MaxTokens,
            Temperature: 1.0,
            TopP: 0.95,
            Stream: stream,
            ChatTemplateKwargs: new DeepSeekChatTemplateKwargs(Thinking: false));
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

    private sealed record DeepSeekRequest(
        string Model,
        List<DeepSeekMessage> Messages,
        int MaxTokens,
        double Temperature,
        double TopP,
        bool Stream,
        DeepSeekChatTemplateKwargs? ChatTemplateKwargs);

    private sealed record DeepSeekMessage(string Role, string Content);

    private sealed record DeepSeekChatTemplateKwargs(bool Thinking);

    private sealed record DeepSeekResponse(List<DeepSeekChoice>? Choices);

    private sealed record DeepSeekChoice(DeepSeekMessage? Message);

    private sealed record DeepSeekStreamChunk(List<DeepSeekStreamChoice>? Choices);

    private sealed record DeepSeekStreamChoice(DeepSeekStreamDelta? Delta);

    private sealed record DeepSeekStreamDelta(string? Content);

    private sealed record SentimentJson(
        double Score,
        string Label,
        string Summary);
}

public sealed class DeepSeekOptions
{
    /// <summary>
    /// NVIDIA API key. Store via:
    /// <c>dotnet user-secrets set "DeepSeek:ApiKey" "nvapi-..."</c>
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
}
