using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quantira.Domain.Entities;
using Quantira.Domain.ValueObjects;

namespace Quantira.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Portfolio"/> aggregate.
/// Maps all properties, owned entities (value objects), indexes,
/// and the soft-delete global query filter.
/// Splitting the configuration per entity keeps the DbContext clean
/// and makes it easy to find the mapping for any given table.
/// </summary>
public sealed class PortfolioConfiguration : IEntityTypeConfiguration<Portfolio>
{
    public void Configure(EntityTypeBuilder<Portfolio> builder)
    {
        builder.ToTable("Portfolios");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.UserId)
            .IsRequired();

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.Description)
            .HasMaxLength(500);

        builder.Property(p => p.CostMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(p => p.IsDefault)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(p => p.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(p => p.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(p => p.DeletedAt);

        // ── Value Object: BaseCurrency ────────────────────────────────
        // Owned entity maps Currency value object to a single column.
        builder.OwnsOne(p => p.BaseCurrency, currency =>
        {
            currency.Property(c => c.Code)
                .HasColumnName("BaseCurrency")
                .IsRequired()
                .HasMaxLength(3);
        });

        // ── Indexes ──────────────────────────────────────────────────
        // Partial unique index — one default portfolio per user.
        builder.HasIndex(p => new { p.UserId, p.IsDefault })
            .HasFilter("[IsDefault] = 1")
            .IsUnique()
            .HasDatabaseName("UIX_Portfolios_UserId_Default");

        // Soft-delete filter — active portfolios only.
        builder.HasQueryFilter(p => p.DeletedAt == null);

        // ── Relationships ─────────────────────────────────────────────
        builder.HasMany(p => p.Positions)
            .WithOne()
            .HasForeignKey(pos => pos.PortfolioId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Trades)
            .WithOne()
            .HasForeignKey(t => t.PortfolioId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ignore domain events collection — not persisted.
        builder.Ignore(p => p.DomainEvents);
    }
}