using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.Common.Interfaces;

namespace Quantira.Infrastructure.AI.Services;

public sealed class GeminiAIService : BaseAiService
{
    // Gemini API uses camelCase — kept separate from the shared base opts.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
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
        : base(logger, "Gemini")
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Add("x-goog-api-key", _options.ApiKey);
    }

    public override async Task<string> GetAdviceAsync(
        string portfolioContext,
        string question,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(portfolioContext, question);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                $"{_options.BaseApiUrl}:generateContent", request, JsonOpts, cancellationToken);

            response.EnsureSuccessStatusCode();

            var result = await response.Content
                .ReadFromJsonAsync<GeminiResponse>(JsonOpts, cancellationToken);

            return result?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text
                ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[Gemini] GetAdviceAsync failed for question: {Question}",
                question[..Math.Min(question.Length, 100)]);
            throw;
        }
    }

    public override async IAsyncEnumerable<string> StreamAdviceAsync(
        string portfolioContext,
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(portfolioContext, question);

        var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"{_options.BaseApiUrl}:streamGenerateContent?alt=sse")
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

            var text = evt?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
            if (text is not null)
                yield return text;
        }
    }

    private GeminiRequest BuildRequest(string context, string question) =>
        new(
            SystemInstruction: new GeminiContent(Parts: [new GeminiPart(BuildSystemPrompt(context))]),
            Contents: [new GeminiContent(Role: "user", Parts: [new GeminiPart(question)])],
            GenerationConfig: new GeminiGenerationConfig(MaxOutputTokens: _options.MaxTokens));

    private sealed record GeminiRequest(
        GeminiContent SystemInstruction,
        List<GeminiContent> Contents,
        GeminiGenerationConfig GenerationConfig);

    private sealed record GeminiGenerationConfig(int MaxOutputTokens);
    private sealed record GeminiContent(List<GeminiPart> Parts, string? Role = null);
    private sealed record GeminiPart(string Text);
    private sealed record GeminiResponse(List<GeminiCandidate>? Candidates);
    private sealed record GeminiCandidate(GeminiContent? Content);
}

public sealed class GeminiOptions
{
    /// <summary>Store via: <c>dotnet user-secrets set "Gemini:ApiKey" "..."</c></summary>
    public string ApiKey { get; set; } = string.Empty;
    public string BaseApiUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-pro-latest";
    public int MaxTokens { get; set; } = 1024;
}
