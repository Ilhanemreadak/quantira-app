using FluentValidation;

namespace Quantira.Application.Portfolios.Commands.AddTrade;

/// <summary>
/// FluentValidation validator for <see cref="AddTradeCommand"/>.
/// Enforces all field-level constraints before the handler runs.
/// Domain-level invariants (e.g. cannot sell more than held quantity)
/// are enforced by the <see cref="Domain.Entities.Portfolio"/> aggregate
/// and surface as <see cref="Domain.Exceptions.DomainException"/>.
/// </summary>
public sealed class AddTradeCommandValidator : AbstractValidator<AddTradeCommand>
{
    public AddTradeCommandValidator()
    {
        RuleFor(x => x.PortfolioId)
            .NotEmpty()
            .WithMessage("Portfolio ID is required.");

        RuleFor(x => x.AssetId)
            .NotEmpty()
            .WithMessage("Asset ID is required.");

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithMessage("Quantity must be greater than zero.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Price cannot be negative.");

        RuleFor(x => x.PriceCurrency)
            .NotEmpty()
            .WithMessage("Price currency is required.")
            .Length(3)
            .WithMessage("Price currency must be a valid 3-letter ISO 4217 code.")
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Price currency must contain only letters.");

        RuleFor(x => x.Commission)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Commission cannot be negative.");

        RuleFor(x => x.TaxAmount)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Tax amount cannot be negative.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Notes must not exceed 500 characters.")
            .When(x => x.Notes is not null);

        RuleFor(x => x.TradedAt)
            .LessThanOrEqualTo(DateTime.UtcNow.AddMinutes(5))
            .WithMessage("Trade date cannot be in the future.")
            .When(x => x.TradedAt.HasValue);
    }
}