using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Quantira.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs the start, completion and duration
/// of every command and query passing through the pipeline.
/// Runs first in the pipeline so it captures the total elapsed time
/// including validation, caching and handler execution.
/// Slow requests (over 500ms) are logged at Warning level so they can
/// be surfaced in Application Insights dashboards without flooding
/// the Info log stream.
/// </summary>
/// <typeparam name="TRequest">The command or query type.</typeparam>
/// <typeparam name="TResponse">The handler response type.</typeparam>
public sealed class LoggingBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const int SlowRequestThresholdMs = 500;

    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation(
            "[START] {RequestName} {@Request}",
            requestName, request);

        var sw = Stopwatch.StartNew();

        try
        {
            var response = await next();
            sw.Stop();

            if (sw.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                _logger.LogWarning(
                    "[SLOW] {RequestName} completed in {ElapsedMs}ms — exceeds {Threshold}ms threshold. {@Request}",
                    requestName, sw.ElapsedMilliseconds, SlowRequestThresholdMs, request);
            }
            else
            {
                _logger.LogInformation(
                    "[END] {RequestName} completed in {ElapsedMs}ms",
                    requestName, sw.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "[ERROR] {RequestName} failed after {ElapsedMs}ms",
                requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}