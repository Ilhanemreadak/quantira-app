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
    private static readonly HashSet<string> SkippedPaths =
    [
        "/health",
        "/health/live",
        "/health/ready",
        "/jobs"
    ];

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (SkippedPaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
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
}