using Quantira.Application.Alerts.DTOs;
using Quantira.Application.Portfolios.DTOs;

namespace Quantira.Application.Common.Interfaces;

/// <summary>
/// Abstracts user notification delivery from the application layer.
/// The infrastructure implementation routes each notification to the
/// appropriate channel (email via SendGrid, push via FCM/APNs, SMS via Twilio)
/// based on the user's preferences stored in the database.
/// All methods are fire-and-forget from the domain's perspective —
/// notification failures are logged and retried by Hangfire but never
/// propagate back to the originating command handler.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a notification informing the user that one of their
    /// price or indicator alerts has been triggered.
    /// </summary>
    Task SendAlertTriggeredAsync(
        Guid userId,
        AlertNotificationDto alert,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a reminder notification two days before an ex-dividend date
    /// for an asset in the user's portfolio.
    /// </summary>
    Task SendDividendReminderAsync(
        Guid userId,
        string symbol,
        DateTime exDividendDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Delivers the user's weekly or monthly portfolio performance summary.
    /// Triggered by a scheduled Hangfire job.
    /// </summary>
    Task SendPortfolioSummaryAsync(
        Guid userId,
        PortfolioSummaryDto summary,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the email verification message during user registration.
    /// Contains a time-limited verification token.
    /// </summary>
    Task SendEmailVerificationAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a password reset email containing a time-limited reset token.
    /// </summary>
    Task SendPasswordResetAsync(
        string email,
        string resetToken,
        CancellationToken cancellationToken = default);
}