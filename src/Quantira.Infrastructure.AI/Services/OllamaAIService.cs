using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.Common.Interfaces;

namespace Quantira.Infrastructure.AI.Services;

public sealed class OllamaAIService : BaseAiService
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
        : base(logger, "Ollama")
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri(_options.BaseUrl);
    }

    public override async Task<string> GetAdviceAsync(
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

    public override async IAsyncEnumerable<string> StreamAdviceAsync(
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
                chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOpts);
            }
            catch
            {
                continue;
            }

            if (chunk?.Message?.Content is not null)
                yield return chunk.Message.Content;

            if (chunk?.Done == true) break;
        }
    }

    private List<OllamaMessage> BuildMessages(string context, string question) =>
    [
        new OllamaMessage("system", BuildSystemPrompt(context)),
        new OllamaMessage("user", question)
    ];

    private sealed record OllamaChatRequest(
        string Model,
        bool Stream,
        List<OllamaMessage> Messages);

    private sealed record OllamaMessage(string Role, string Content);

    private sealed record OllamaChatResponse(OllamaMessage? Message, bool? Done);
}

public sealed class OllamaOptions
{
    /// <summary>Ollama API base URL. Default: http://localhost:11434.</summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model to use. Must be pulled first: <c>ollama pull llama3.2</c>
    /// </summary>
    public string Model { get; set; } = "llama3.2";
}
