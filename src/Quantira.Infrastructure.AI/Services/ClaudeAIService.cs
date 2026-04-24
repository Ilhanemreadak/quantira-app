using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.Common.Interfaces;

namespace Quantira.Infrastructure.AI.Services;

public sealed class ClaudeAIService : BaseAiService
{
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
        : base(logger, "Claude")
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
    }

    public override async Task<string> GetAdviceAsync(
        string portfolioContext,
        string question,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(portfolioContext, question, stream: false);

        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                _options.ApiUrl, request, JsonOpts, cancellationToken);

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

    public override async IAsyncEnumerable<string> StreamAdviceAsync(
        string portfolioContext,
        string question,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var request = BuildRequest(portfolioContext, question, stream: true);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl)
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

    private ClaudeRequest BuildRequest(
        string context,
        string question,
        bool stream) =>
        new(
            Model: _options.Model,
            MaxTokens: _options.MaxTokens,
            Stream: stream,
            System: BuildSystemPrompt(context),
            Messages: [new ClaudeMessage(Role: "user", Content: question)]);

    private sealed record ClaudeRequest(
        string Model,
        int MaxTokens,
        bool Stream,
        string System,
        List<ClaudeMessage> Messages);

    private sealed record ClaudeMessage(string Role, string Content);
    private sealed record ClaudeResponse(List<ClaudeContent>? Content);
    private sealed record ClaudeContent(string? Type, string? Text);
    private sealed record ClaudeStreamEvent(string? Type, ClaudeDelta? Delta);
    private sealed record ClaudeDelta(string? Type, string? Text);
}

public sealed class ClaudeOptions
{
    /// <summary>Store via: <c>dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."</c></summary>
    public string ApiKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://api.anthropic.com/v1/messages";
    public string Model { get; set; } = "claude-opus-4-6";
    public int MaxTokens { get; set; } = 1024;
}
