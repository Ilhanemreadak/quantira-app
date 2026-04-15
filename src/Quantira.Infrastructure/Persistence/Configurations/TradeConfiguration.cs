using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quantira.Domain.Entities;

namespace Quantira.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Trade"/> entity.
/// Trades are immutable financial records — the table has no UpdatedAt
/// or DeletedAt columns by design. The clustered index is on
/// (PortfolioId, TradedAt DESC) to optimise the dominant query pattern:
/// "get recent trades for this portfolio".
/// </summary>
public sealed class TradeConfiguration : IEntityTypeConfiguration<Trade>
{
    public void Configure(EntityTypeBuilder<Trade> builder)
    {
        builder.ToTable("Trades");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();

        builder.Property(t => t.PortfolioId)
            .IsRequired();

        builder.Property(t => t.AssetId)
            .IsRequired();

        builder.Property(t => t.TradeType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Quantity)
            .IsRequired()
            .HasPrecision(18, 8);

        builder.Property(t => t.PriceCurrency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(t => t.Notes)
            .HasMaxLength(500);

        builder.Property(t => t.TradedAt)
            .IsRequired();

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // ── Value Objects ─────────────────────────────────────────────
        builder.OwnsOne(t => t.Price, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("Price")
                .HasPrecision(18, 4)
                .IsRequired();

            money.Ignore(m => m.Currency);
        });

        builder.OwnsOne(t => t.Commission, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("Commission")
                .HasPrecision(18, 4)
                .IsRequired()
                .HasDefaultValue(0m);

            money.Ignore(m => m.Currency);
        });

        builder.OwnsOne(t => t.TaxAmount, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TaxAmount")
                .HasPrecision(18, 4)
                .IsRequired()
                .HasDefaultValue(0m);

            money.Ignore(m => m.Currency);
        });

        // Computed properties — not persisted.
        builder.Ignore(t => t.GrossValue);
        builder.Ignore(t => t.NetValue);

        // ── Indexes ──────────────────────────────────────────────────
        // Primary query pattern: portfolio's recent trades.
        builder.HasIndex(t => new { t.PortfolioId, t.TradedAt })
            .HasDatabaseName("IX_Trades_Portfolio_TradedAt");

        builder.HasIndex(t => new { t.PortfolioId, t.AssetId })
            .HasDatabaseName("IX_Trades_Portfolio_Asset");
    }
}