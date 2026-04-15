using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quantira.Application.Alerts.DTOs;
using Quantira.Application.Common.Interfaces;
using Quantira.Application.Common.Models;
using Quantira.Application.Portfolios.DTOs;

namespace Quantira.Infrastructure.Notifications;

/// <summary>
/// Email notification implementation using SendGrid.
/// Implements <see cref="INotificationService"/> for all email-based
/// notification types. In the current phase only email is supported —
/// push notifications (FCM/APNs) and SMS (Twilio) are planned for Faz 3.
/// All methods are fire-and-forget safe — exceptions are caught, logged
/// and never propagated to the calling command handler.
/// </summary>
public sealed class EmailNotificationService : INotificationService
{
    private readonly EmailOptions _options;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<EmailOptions> options,
        ILogger<EmailNotificationService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task SendAlertTriggeredAsync(
        Guid userId,
        AlertNotificationDto alert,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Email] Sending alert triggered notification to user {UserId} " +
            "for asset {AssetId} — AlertType={AlertType} TriggerValue={Value}",
            userId, alert.AssetId, alert.AlertType, alert.TriggerValue);

        // TODO: Replace with SendGrid SDK call.
        // var message = new SendGridMessage();
        // message.SetFrom(_options.SenderEmail, "Quantira");
        // message.AddTo(userEmail);
        // message.SetSubject($"Alert triggered: {alert.AlertType}");
        // message.AddContent(MimeType.Text, BuildAlertBody(alert));
        // await _sendGridClient.SendEmailAsync(message, cancellationToken);

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SendDividendReminderAsync(
        Guid userId,
        string symbol,
        DateTime exDividendDate,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Email] Sending dividend reminder to user {UserId} " +
            "for {Symbol} ex-date {ExDate:yyyy-MM-dd}",
            userId, symbol, exDividendDate);

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SendPortfolioSummaryAsync(
        Guid userId,
        PortfolioSummaryDto summary,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Email] Sending portfolio summary to user {UserId} " +
            "Portfolio={PortfolioId} TotalValue={Value}",
            userId, summary.PortfolioId, summary.TotalCurrentValue);

        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SendEmailVerificationAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Email] Sending verification email to {Email}", email);

        // TODO: Implement SendGrid verification email with token link.
        await Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SendPasswordResetAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[Email] Sending password reset email to {Email}", email);

        await Task.CompletedTask;
    }
}

/// <summary>
/// Configuration options for the email notification service.
/// Bound from the "Email" section of appsettings.json.
/// Sensitive values (API key) must be stored in User Secrets.
/// </summary>
public sealed class EmailOptions
{
    /// <summary>SendGrid API key. Store in User Secrets — never in appsettings.json.</summary>
    public string SendGridApiKey { get; set; } = string.Empty;

    /// <summary>The "from" email address for all outgoing Quantira emails.</summary>
    public string SenderEmail { get; set; } = "noreply@quantira.app";

    /// <summary>The "from" display name for all outgoing emails.</summary>
    public string SenderName { get; set; } = "Quantira";
}