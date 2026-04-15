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
/// Ollama implementation of <see cref="IAIService"/>.
/// Runs AI models locally via the Ollama REST API (http://localhost:11434).
/// No API key required — completely free and private.
/// Supports any model available in the Ollama library
/// (llama3, mistral, phi3, gemma2, etc.).
/// Ideal for development and testing without internet dependency,
/// or for production deployments where data privacy is critical.
/// Switch between Claude and Ollama by changing the DI registration
/// in <c>DependencyInjection.cs</c> — no other code changes needed.
/// </summary>
public sealed class OllamaAIService : IAIService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly OllamaOptions _options;
    private readonly ILogger<OllamaAIService> _logger;

    public OllamaAIService(
        HttpClient httpClient,
        IOptions<OllamaOptions> options,
        ILogger<OllamaAIService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<string> GetAdviceAsync(
        string portfolioContext,
        string question,
        CancellationToken cancellationToken = default)
    {
        var request = new OllamaChatRequest(
            Model: _options.Model,
            Stream: false,
            Messages: BuildMessages(portfolioContext, question));

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/chat", request, JsonOpts, cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<OllamaChatResponse>(JsonOpts, cancellationToken);

            return result?.Message?.Content ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Ollama] GetAdviceAsync failed. Model={Model} BaseUrl={BaseUrl}",
                _options.Model, _options.BaseUrl);
            throw;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<string> StreamAdviceAsync(
        string portfolioContext,
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = new OllamaChatRequest(
            Model: _options.Model,
            Stream: true,
            Messages: BuildMessages(portfolioContext, question));

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
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
            _logger.LogError(ex, "[Ollama] StreamAdviceAsync failed.");
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

            OllamaChatResponse? chunk;

            try
            {
                chunk = JsonSerializer.Deserialize<OllamaChatResponse>(
                    line, JsonOpts);
            }
            catch
            {
                continue;
            }

            if (chunk?.Message?.Content is not null)
            {
                yield return chunk.Message.Content;
            }

            if (chunk?.Done == true) break;
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
            $"Respond ONLY with a JSON object: " +
            $"{{\"score\": <number -1.0 to 1.0>, " +
            $"\"label\": \"Positive|Neutral|Negative\", " +
            $"\"summary\": \"<one sentence>\"}}. " +
            $"Article: {newsText[..Math.Min(newsText.Length, 1500)]}";

        var result = await GetAdviceAsync(string.Empty, prompt, cancellationToken);

        try
        {
            // Strip markdown code fences if the model wraps the JSON.
            var cleaned = result
                .Replace("```json", string.Empty)
                .Replace("```", string.Empty)
                .Trim();

            var parsed = JsonSerializer.Deserialize<SentimentJson>(
                cleaned, JsonOpts);

            return new NewsSentimentResult(
                Score: parsed?.Score ?? 0,
                Label: parsed?.Label ?? "Neutral",
                Summary: parsed?.Summary ?? string.Empty);
        }
        catch
        {
            _logger.LogWarning(
                "[Ollama] Failed to parse sentiment JSON for {Symbol}.", symbol);

            return new NewsSentimentResult(0, "Neutral", string.Empty);
        }
    }

    // ── Private helpers ──────────────────────────────────────────────

    private List<OllamaMessage> BuildMessages(
        string context,
        string question)
    {
        var messages = new List<OllamaMessage>();

        var systemPrompt = string.IsNullOrWhiteSpace(context)
            ? BuildBaseSystemPrompt()
            : $"{BuildBaseSystemPrompt()}\n\n## Portfolio Context\n{context}";

        messages.Add(new OllamaMessage("system", systemPrompt));
        messages.Add(new OllamaMessage("user", question));

        return messages;
    }

    private static string BuildBaseSystemPrompt() =>
        """
        You are Quantira, an AI-powered financial assistant embedded in a
        portfolio tracking application. Help users understand their portfolio,
        analyze market data and make informed decisions.
        Be concise, factual and data-driven.
        Always add: "This is not investment advice. Always do your own research."
        Never fabricate market data — only use what is provided in the context.
        """;

    // ── Internal models ──────────────────────────────────────────────

    private sealed record OllamaChatRequest(
        string Model,
        bool Stream,
        List<OllamaMessage> Messages);

    private sealed record OllamaMessage(string Role, string Content);

    private sealed record OllamaChatResponse(
        OllamaMessage? Message,
        bool? Done);

    private sealed record SentimentJson(
        double Score,
        string Label,
        string Summary);
}

/// <summary>
/// Configuration options for the Ollama AI service.
/// Bound from the "Ollama" section of appsettings.json.
/// </summary>
public sealed class OllamaOptions
{
    /// <summary>
    /// Ollama API base URL. Default: http://localhost:11434.
    /// Change this for remote Ollama deployments.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// The model to use for completions.
    /// Must be pulled first: <c>ollama pull llama3.2</c>
    /// Recommended models by use case:
    /// Fast dev: phi3, gemma2:2b
    /// Balanced: llama3.2, mistral
    /// Best quality: llama3.1:70b (requires strong GPU)
    /// </summary>
    public string Model { get; set; } = "llama3.2";
}