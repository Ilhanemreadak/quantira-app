using FluentValidation;

namespace Quantira.Application.Alerts.Commands.CreateAlert;

/// <summary>
/// FluentValidation validator for <see cref="CreateAlertCommand"/>.
/// </summary>
public sealed class CreateAlertCommandValidator
    : AbstractValidator<CreateAlertCommand>
{
    public CreateAlertCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.AssetId)
            .NotEmpty()
            .WithMessage("Asset ID is required.");

        RuleFor(x => x.ConditionJson)
            .NotEmpty()
            .WithMessage("Alert condition is required.")
            .MaximumLength(500)
            .WithMessage("Condition must not exceed 500 characters.");

        RuleFor(x => x.ExpiresAt)
            .GreaterThan(DateTime.UtcNow)
            .WithMessage("Expiry date must be in the future.")
            .When(x => x.ExpiresAt.HasValue);
    }
}