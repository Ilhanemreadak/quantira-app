using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.Common.Interfaces;

namespace Quantira.Infrastructure.AI.Services;

public sealed class DeepSeekAIService : BaseAiService
{
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
        : base(logger, "DeepSeek")
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
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

    private DeepSeekRequest BuildRequest(
        string context,
        string question,
        bool stream) =>
        new(
            Model: _options.Model,
            Messages:
            [
                new DeepSeekMessage(Role: "system", Content: BuildSystemPrompt(context)),
                new DeepSeekMessage(Role: "user", Content: question)
            ],
            MaxTokens: _options.MaxTokens,
            Temperature: _options.Temperature,
            TopP: _options.TopP,
            Stream: stream,
            ChatTemplateKwargs: new DeepSeekChatTemplateKwargs(Thinking: false));

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
}

public sealed class DeepSeekOptions
{
    /// <summary>Store via: <c>dotnet user-secrets set "DeepSeek:ApiKey" "nvapi-..."</c></summary>
    public string ApiKey { get; set; } = string.Empty;
    public string ApiUrl { get; set; } = "https://integrate.api.nvidia.com/v1/chat/completions";
    public string Model { get; set; } = "deepseek-ai/deepseek-v3.2";
    public int MaxTokens { get; set; } = 8192;
    public double Temperature { get; set; } = 1.0;
    public double TopP { get; set; } = 0.95;
}
