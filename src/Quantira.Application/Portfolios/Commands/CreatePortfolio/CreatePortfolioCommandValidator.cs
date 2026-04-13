using FluentValidation;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Portfolios.Commands.CreatePortfolio;

/// <summary>
/// FluentValidation validator for <see cref="CreatePortfolioCommand"/>.
/// Runs automatically via <c>ValidationBehavior</c> before the handler.
/// Includes an async database check to enforce unique portfolio names
/// per user without duplicating that logic in the handler.
/// </summary>
public sealed class CreatePortfolioCommandValidator
    : AbstractValidator<CreatePortfolioCommand>
{
    public CreatePortfolioCommandValidator(IPortfolioRepository portfolioRepository)
    {
        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Portfolio name is required.")
            .MaximumLength(100)
            .WithMessage("Portfolio name must not exceed 100 characters.");

        RuleFor(x => x.BaseCurrency)
            .NotEmpty()
            .WithMessage("Base currency is required.")
            .Length(3)
            .WithMessage("Base currency must be a valid 3-letter ISO 4217 code.")
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Base currency must contain only letters.");

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithMessage("Description must not exceed 500 characters.")
            .When(x => x.Description is not null);

        RuleFor(x => x)
            .MustAsync(async (command, ct) =>
                !await portfolioRepository.ExistsAsync(
                    command.UserId, command.Name, ct))
            .WithName("Name")
            .WithMessage("A portfolio with this name already exists.");
    }
}