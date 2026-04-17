using Microsoft.Extensions.Options;
using Quantira.WebAPI.Configuration;

namespace Quantira.WebAPI.Middleware;

/// <summary>
/// Lightweight middleware that logs every HTTP request with its method,
/// path, status code and elapsed time. Runs after
/// <see cref="ExceptionHandlingMiddleware"/> so the status code is
/// always the final mapped code, not the raw exception status.
/// Skips health check and Hangfire dashboard endpoints to keep
/// logs focused on application traffic.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private static readonly HashSet<string> DefaultSkippedPaths =
    [
        "/health",
        "/health/live",
        "/health/ready"
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;
    private readonly string _dashboardPath;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger,
        IOptions<HangfireSettings> hangfireSettings)
    {
        _next = next;
        _logger = logger;
        _dashboardPath = NormalizePath(hangfireSettings.Value.Dashboard.Path);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (ShouldSkip(path))
        {
            await _next(context);
            return;
        }

        var start = System.Diagnostics.Stopwatch.GetTimestamp();

        await _next(context);

        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);

        var level = context.Response.StatusCode >= 500
            ? LogLevel.Error
            : context.Response.StatusCode >= 400
                ? LogLevel.Warning
                : LogLevel.Information;

        _logger.Log(level,
            "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
            context.Request.Method,
            path,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds.ToString("F1"));
    }

    private bool ShouldSkip(string path)
    {
        if (DefaultSkippedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            return true;

        return !string.IsNullOrWhiteSpace(_dashboardPath)
            && path.StartsWith(_dashboardPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        return path.StartsWith('/') ? path : $"/{path}";
    }
}