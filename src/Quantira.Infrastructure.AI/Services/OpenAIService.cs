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

public sealed class OpenAIService : BaseAiService
{
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";
    private const string Model = "gpt-4o";
    private const int MaxTokens = 1024;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenAIService> _logger;

    public OpenAIService(
        HttpClient httpClient,
        IOptions<OpenAIOptions> options,
        ILogger<OpenAIService> logger)
        : base(logger, "OpenAI")
    {
        _httpClient = httpClient;
        _logger = logger;

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", options.Value.ApiKey);
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

    public override async IAsyncEnumerable<string> StreamAdviceAsync(
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

            var content = evt?.Choices?.FirstOrDefault()?.Delta?.Content;
            if (content is not null)
                yield return content;
        }
    }

    private static OpenAIRequest BuildRequest(
        string context,
        string question,
        bool stream) =>
        new(
            Model: Model,
            MaxTokens: MaxTokens,
            Stream: stream,
            Messages:
            [
                new OpenAIMessage(Role: "system", Content: BuildSystemPrompt(context)),
                new OpenAIMessage(Role: "user", Content: question)
            ]);

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
}

public sealed class OpenAIOptions
{
    /// <summary>Store via: <c>dotnet user-secrets set "OpenAI:ApiKey" "sk-..."</c></summary>
    public string ApiKey { get; set; } = string.Empty;
}
