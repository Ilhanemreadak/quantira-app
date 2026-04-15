using FluentValidation;
using Quantira.Domain.Interfaces;

namespace Quantira.Application.Assets.Commands.CreateAsset;

/// <summary>
/// FluentValidation validator for <see cref="CreateAssetCommand"/>.
/// Includes an async uniqueness check to prevent duplicate symbol registration.
/// </summary>
public sealed class CreateAssetCommandValidator
    : AbstractValidator<CreateAssetCommand>
{
    public CreateAssetCommandValidator(IAssetRepository assetRepository)
    {
        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithMessage("Symbol is required.")
            .MaximumLength(20)
            .WithMessage("Symbol must not exceed 20 characters.")
            .MustAsync(async (symbol, ct) =>
                !await assetRepository.ExistsBySymbolAsync(symbol, ct))
            .WithMessage("An asset with this symbol already exists.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Asset name is required.")
            .MaximumLength(200)
            .WithMessage("Asset name must not exceed 200 characters.");

        RuleFor(x => x.Currency)
            .NotEmpty()
            .WithMessage("Currency is required.")
            .Length(3)
            .WithMessage("Currency must be a valid 3-letter ISO 4217 code.")
            .Matches("^[A-Za-z]{3}$")
            .WithMessage("Currency must contain only letters.");

        RuleFor(x => x.Exchange)
            .MaximumLength(20)
            .WithMessage("Exchange code must not exceed 20 characters.")
            .When(x => x.Exchange is not null);

        RuleFor(x => x.Sector)
            .MaximumLength(50)
            .WithMessage("Sector must not exceed 50 characters.")
            .When(x => x.Sector is not null);

        RuleFor(x => x.DataProviderKey)
            .MaximumLength(100)
            .WithMessage("Data provider key must not exceed 100 characters.")
            .When(x => x.DataProviderKey is not null);
    }
}