using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quantira.Application.Chat.Services;
using Quantira.Application.Common.Interfaces;
using Quantira.Infrastructure.AI.Prompts;
using Quantira.Infrastructure.AI.Services;

namespace Quantira.Infrastructure.AI;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── HTTP clients (all registered; only the active provider is resolved) ──
        services.AddHttpClient<ClaudeAIService>().AddStandardResilienceHandler();
        services.AddHttpClient<OpenAIService>().AddStandardResilienceHandler();
        services.AddHttpClient<GeminiAIService>().AddStandardResilienceHandler();
        services.AddHttpClient<OllamaAIService>().AddStandardResilienceHandler();
        services.AddHttpClient<DeepSeekAIService>().AddStandardResilienceHandler();

        // ── Options ──────────────────────────────────────────────────
        services.Configure<ClaudeOptions>(configuration.GetSection("Claude"));
        services.Configure<OpenAIOptions>(configuration.GetSection("OpenAI"));
        services.Configure<GeminiOptions>(configuration.GetSection("Gemini"));
        services.Configure<OllamaOptions>(configuration.GetSection("Ollama"));
        services.Configure<DeepSeekOptions>(configuration.GetSection("DeepSeek"));

        // ── Active provider — driven by appsettings "AIProvider" key ─
        var provider = configuration["AIProvider"] ?? "Claude";

        services.AddScoped<IAIService>(provider switch
        {
            "OpenAI"   => sp => sp.GetRequiredService<OpenAIService>(),
            "Gemini"   => sp => sp.GetRequiredService<GeminiAIService>(),
            "Ollama"   => sp => sp.GetRequiredService<OllamaAIService>(),
            "DeepSeek" => sp => sp.GetRequiredService<DeepSeekAIService>(),
            _          => sp => sp.GetRequiredService<ClaudeAIService>()
        });

        services.AddScoped<IPortfolioContextBuilder, PortfolioContextBuilder>();

        return services;
    }
}
