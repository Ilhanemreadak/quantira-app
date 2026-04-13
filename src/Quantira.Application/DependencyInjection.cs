using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Quantira.Application.Common.Behaviors;

namespace Quantira.Application;

/// <summary>
/// Extension method that registers all <c>Quantira.Application</c> layer
/// dependencies into the ASP.NET Core dependency injection container.
/// Called once from <c>Program.cs</c> in the WebAPI project.
/// Registers MediatR handlers, FluentValidation validators and the
/// full pipeline behavior chain in the correct execution order:
/// Logging → Validation → Caching → Transaction → Handler.
/// Using <c>Assembly.GetExecutingAssembly()</c> ensures that any new
/// command, query, handler or validator added to this project is
/// automatically discovered without touching this file.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(
        this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // ── MediatR ──────────────────────────────────────────────────
        // Auto-registers all IRequestHandler<,> and INotificationHandler<>
        // implementations found in this assembly.
        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(assembly));

        // ── FluentValidation ─────────────────────────────────────────
        // Auto-registers all AbstractValidator<T> implementations
        // found in this assembly. ValidationBehavior resolves them
        // via IEnumerable<IValidator<TRequest>>.
        services.AddValidatorsFromAssembly(assembly);

        // ── Pipeline Behaviors ───────────────────────────────────────
        // Registered in execution order — first registered = outermost wrapper.
        // Logging wraps everything so total elapsed time is captured.
        // Validation runs before caching and transactions to fail fast.
        // Caching runs before transaction to bypass the handler on cache hits.
        // Transaction wraps only the handler execution for commands
        // that implement ITransactionalCommand.
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(LoggingBehavior<,>));

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehavior<,>));

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(CachingBehavior<,>));

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(TransactionBehavior<,>));

        return services;
    }
}