using System.Net;
using System.Text.Json;
using FluentValidation;
using Quantira.Domain.Exceptions;

namespace Quantira.WebAPI.Middleware;

/// <summary>
/// Global exception handling middleware that catches all unhandled exceptions
/// thrown during request processing and maps them to appropriate HTTP responses.
/// Centralises error handling so individual controllers and handlers never
/// need to catch exceptions themselves — they simply throw and this middleware
/// produces a consistent, structured JSON error response.
/// Registered as the outermost middleware in <c>Program.cs</c> so it
/// wraps the entire request pipeline.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                new ErrorResponse(
                    Type: "ValidationError",
                    Title: "One or more validation errors occurred.",
                    Status: 400,
                    Errors: ve.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray()))),

            NotFoundException nfe => (
                HttpStatusCode.NotFound,
                new ErrorResponse(
                    Type: "NotFound",
                    Title: nfe.Message,
                    Status: 404,
                    Errors: null)),

            DomainException de => (
                HttpStatusCode.UnprocessableEntity,
                new ErrorResponse(
                    Type: "DomainError",
                    Title: de.Message,
                    Status: 422,
                    Errors: null)),

            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                new ErrorResponse(
                    Type: "Unauthorized",
                    Title: "You are not authorized to perform this action.",
                    Status: 401,
                    Errors: null)),

            _ => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse(
                    Type: "ServerError",
                    Title: "An unexpected error occurred.",
                    Status: 500,
                    Errors: null))
        };

        if ((int)statusCode >= 500)
            _logger.LogError(exception,
                "Unhandled exception on {Method} {Path}",
                context.Request.Method,
                context.Request.Path);
        else
            _logger.LogWarning(
                "Handled exception [{Type}] on {Method} {Path}: {Message}",
                exception.GetType().Name,
                context.Request.Method,
                context.Request.Path,
                exception.Message);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, JsonOpts));
    }

    private sealed record ErrorResponse(
        string Type,
        string Title,
        int Status,
        Dictionary<string, string[]>? Errors);
}