using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.Alerts.DTOs;
using Quantira.Application.Common.Interfaces;
using Quantira.Application.Common.Models;
using Quantira.Application.Portfolios.DTOs;
using Quantira.Infrastructure.Persistence;
using System.Net.Http.Json;
using System.Text.Json;

namespace Quantira.Infrastructure.Notifications;

/// <summary>
/// Email notification implementation using the Resend API.
/// Resend offers 3000 emails/month on the free tier with no credit card.
/// All methods are fire-and-forget safe — exceptions are caught and logged
/// but never propagate to the calling command handler.
/// API docs: https://resend.com/docs/api-reference/emails/send-email
/// </summary>
public sealed class ResendEmailService : INotificationService
{
    private const string ApiUrl = "https://api.resend.com/emails";

    private readonly HttpClient _httpClient;
    private readonly ResendOptions _options;
    private readonly ILogger<ResendEmailService> _logger;
    private readonly QuantiraDbContext _dbContext;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ResendEmailService(
        HttpClient httpClient,
        IOptions<ResendOptions> options,
        ILogger<ResendEmailService> logger,
        QuantiraDbContext dbContext)
    {
        _options = options.Value;
        _logger = logger;
        _dbContext = dbContext;
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add(
            "Authorization", $"Bearer {_options.ApiKey}");
    }

    /// <inheritdoc/>
    public async Task SendAlertTriggeredAsync(
        Guid userId,
        AlertNotificationDto alert,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        var userEmail = await _dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(userEmail))
        {
            _logger.LogWarning(
                "[ResendEmailService] User email not found for {UserId}. Alert notification skipped.",
                userId);
            return;
        }

        var subject = $"Quantira Alert: {alert.AlertType} triggered for {alert.AssetSymbol}";

        var html = $"""
            <h2>Alert Triggered</h2>
            <p>Your <strong>{alert.AlertType}</strong> alert for
            <strong>{alert.AssetSymbol}</strong> has been triggered.</p>
            <table>
              <tr><td>Symbol</td><td>{alert.AssetSymbol}</td></tr>
              <tr><td>Alert Type</td><td>{alert.AlertType}</td></tr>
              <tr><td>Trigger Value</td><td>{alert.TriggerValue:F4}</td></tr>
              <tr><td>Time</td><td>{alert.TriggeredAt:yyyy-MM-dd HH:mm} UTC</td></tr>
            </table>
            <p><small>This is an automated message from Quantira.</small></p>
            """;

        await SendAsync(userEmail, subject, html, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendDividendReminderAsync(
        Guid userId,
        string symbol,
        DateTime exDividendDate,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        // User email lookup would normally come from IUserRepository.
        // For now we log and skip until user email resolution is wired up.
        _logger.LogInformation(
            "[ResendEmailService] Dividend reminder for {Symbol} ex-date {Date:yyyy-MM-dd}",
            symbol, exDividendDate);

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SendPortfolioSummaryAsync(
        Guid userId,
        PortfolioSummaryDto summary,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        _logger.LogInformation(
            "[ResendEmailService] Portfolio summary for portfolio {PortfolioId}",
            summary.PortfolioId);

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SendEmailVerificationAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        var subject = "Verify your Quantira account";
        var link = $"{_options.AppBaseUrl}/verify-email?token={verificationToken}";

        var html = $"""
            <h2>Welcome to Quantira</h2>
            <p>Please verify your email address by clicking the button below.</p>
            <p>
              <a href="{link}"
                 style="background:#6366f1;color:white;padding:12px 24px;
                        border-radius:6px;text-decoration:none;font-weight:bold;">
                Verify Email
              </a>
            </p>
            <p>Or copy this link: <a href="{link}">{link}</a></p>
            <p>This link expires in 24 hours.</p>
            <p><small>If you did not create a Quantira account, ignore this email.</small></p>
            """;

        await SendAsync(email, subject, html, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendPasswordResetAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled) return;

        var subject = "Reset your Quantira password";
        var link = $"{_options.AppBaseUrl}/reset-password?token={resetToken}";

        var html = $"""
            <h2>Password Reset</h2>
            <p>We received a request to reset your Quantira password.</p>
            <p>
              <a href="{link}"
                 style="background:#6366f1;color:white;padding:12px 24px;
                        border-radius:6px;text-decoration:none;font-weight:bold;">
                Reset Password
              </a>
            </p>
            <p>Or copy this link: <a href="{link}">{link}</a></p>
            <p>This link expires in 1 hour.</p>
            <p><small>If you did not request a password reset, ignore this email.</small></p>
            """;

        await SendAsync(email, subject, html, cancellationToken);
    }

    // ── Private helpers ──────────────────────────────────────────────

    private async Task SendAsync(
        string to,
        string subject,
        string html,
        CancellationToken cancellationToken)
    {
        try
        {
            var payload = new
            {
                from = $"{_options.SenderName} <{_options.SenderEmail}>",
                to = new[] { to },
                subject = subject,
                html = html
            };

            var response = await _httpClient.PostAsJsonAsync(
                ApiUrl, payload, JsonOpts, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError(
                    "[ResendEmailService] Failed to send email to {To}. " +
                    "Status: {Status} Body: {Body}",
                    to, response.StatusCode, body);
                return;
            }

            _logger.LogInformation(
                "[ResendEmailService] Email sent to {To} — Subject: {Subject}",
                to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[ResendEmailService] Exception while sending email to {To}", to);
        }
    }
}

/// <summary>
/// Configuration options for the Resend email service.
/// Bound from the "Resend" section of appsettings.json.
/// API key must be stored in User Secrets.
/// </summary>
public sealed class ResendOptions
{
    /// <summary>
    /// Resend API key. Store via User Secrets:
    /// dotnet user-secrets set "Resend:ApiKey" "re_..."
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>From email address. Must be verified in Resend dashboard.</summary>
    public string SenderEmail { get; set; } = "noreply@quantira.app";

    /// <summary>From display name.</summary>
    public string SenderName { get; set; } = "Quantira";

    /// <summary>
    /// Base URL of the frontend app for generating email links.
    /// Example: https://quantira.app or http://localhost:5173 for dev.
    /// </summary>
    public string AppBaseUrl { get; set; } = "http://localhost:5173";

    /// <summary>
    /// Set to false to disable all email sending (e.g. in dev/test).
    /// Logs intent without making API calls.
    /// </summary>
    public bool Enabled { get; set; } = true;
}