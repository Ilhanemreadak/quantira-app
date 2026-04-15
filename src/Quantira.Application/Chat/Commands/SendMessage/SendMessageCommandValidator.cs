using FluentValidation;

namespace Quantira.Application.Chat.Commands.SendMessage;

/// <summary>
/// FluentValidation validator for <see cref="SendMessageCommand"/>.
/// Enforces message length limits to stay within AI model token budgets
/// and prevent abuse.
/// </summary>
public sealed class SendMessageCommandValidator
    : AbstractValidator<SendMessageCommand>
{
    private const int MaxMessageLength = 2000;

    public SendMessageCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.Message)
            .NotEmpty()
            .WithMessage("Message cannot be empty.")
            .MaximumLength(MaxMessageLength)
            .WithMessage($"Message must not exceed {MaxMessageLength} characters.");
    }
}