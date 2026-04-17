using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quantira.Domain.Entities;

namespace Quantira.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Position"/> entity.
/// Positions have a composite unique constraint on (PortfolioId, AssetId)
/// because a portfolio can only have one open position per asset at any time.
/// Money value objects are mapped as owned entities using separate amount
/// and currency columns for clean SQL querying.
/// </summary>
public sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("Positions");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.PortfolioId)
            .IsRequired();

        builder.Property(p => p.AssetId)
            .IsRequired();

        builder.Property(p => p.Quantity)
            .IsRequired()
            .HasPrecision(18, 8)
            .HasDefaultValue(0m);

        builder.Property(p => p.UnrealizedPnLPct)
            .HasPrecision(10, 4);

        builder.Property(p => p.LastUpdated)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        // ── Value Objects ─────────────────────────────────────────────
        builder.OwnsOne(p => p.AvgCostPrice, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("AvgCostPrice")
                .HasPrecision(18, 4)
                .IsRequired()
                .HasDefaultValue(0m);

            money.OwnsOne(m => m.Currency, currency =>
                currency.Property(c => c.Code)
                    .HasColumnName("CostCurrency")
                    .HasMaxLength(3)
                    .IsRequired());
        });

        builder.OwnsOne(p => p.TotalCost, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("TotalCost")
                .HasPrecision(18, 4)
                .IsRequired()
                .HasDefaultValue(0m);

            money.Ignore(m => m.Currency);
        });

        builder.OwnsOne(p => p.CurrentValue, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("CurrentValue")
                .HasPrecision(18, 4);

            money.Ignore(m => m.Currency);
        });

        builder.OwnsOne(p => p.UnrealizedPnL, money =>
        {
            money.Property(m => m.Amount)
                .HasColumnName("UnrealizedPnL")
                .HasPrecision(18, 4);

            money.Ignore(m => m.Currency);
        });

        // ── Indexes ──────────────────────────────────────────────────
        builder.HasIndex(p => new { p.PortfolioId, p.AssetId })
            .IsUnique()
            .HasDatabaseName("UIX_Positions_Portfolio_Asset");

        builder.HasIndex(p => p.AssetId)
            .HasDatabaseName("IX_Positions_AssetId");
    }
}