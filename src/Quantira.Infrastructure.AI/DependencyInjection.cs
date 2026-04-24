using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quantira.Application.Chat.Services;
using Quantira.Application.Common.Interfaces;
using Quantira.Infrastructure.AI.Prompts;
using Quantira.Infrastructure.AI.Services;


namespace Quantira.Infrastructure.AI;

/// <summary>
/// Extension method that registers all <c>Quantira.Infrastructure.AI</c>
/// dependencies into the ASP.NET Core DI container.
/// Kept in a separate project from <c>Quantira.Infrastructure</c> so the
/// AI provider can be swapped or disabled independently without touching
/// the core infrastructure layer.
/// Called from <c>Program.cs</c> after <c>AddInfrastructure()</c>.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureAI(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── Claude API HTTP client ───────────────────────────────────
        // Standard resilience handler adds retry (3 attempts, exponential
        // backoff) and circuit breaker automatically via .NET 10
        // Microsoft.Extensions.Http.Resilience.
        services.AddHttpClient<ClaudeAIService>()
            .AddStandardResilienceHandler();

        // ── DeepSeek (NVIDIA) HTTP client ────────────────────────────
        services.AddHttpClient<DeepSeekAIService>()
            .AddStandardResilienceHandler();

        // ── Options ──────────────────────────────────────────────────
        services.Configure<ClaudeOptions>(
            configuration.GetSection("Claude"));

        services.Configure<DeepSeekOptions>(
            configuration.GetSection("DeepSeek"));

        // ── Service registrations ────────────────────────────────────
        services.AddScoped<IAIService, ClaudeAIService>();

        services.AddScoped<IPortfolioContextBuilder, PortfolioContextBuilder>();

        return services;
    }
}