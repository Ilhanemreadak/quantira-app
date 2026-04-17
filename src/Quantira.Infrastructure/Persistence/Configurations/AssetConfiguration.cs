using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quantira.Domain.Entities;
using Quantira.Domain.Enums;

namespace Quantira.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Asset"/> aggregate.
/// Assets are shared reference data with a unique symbol constraint.
/// The <see cref="AssetType"/> enum is stored as a string for readability
/// and to avoid migration churn when new values are added.
/// </summary>
public sealed class AssetConfiguration : IEntityTypeConfiguration<Asset>
{
    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.Symbol)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(a => a.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.AssetType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(a => a.Exchange)
            .HasMaxLength(20);

        builder.Property(a => a.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(a => a.Sector)
            .HasMaxLength(50);

        builder.Property(a => a.DataProviderKey)
            .HasMaxLength(100);

        builder.Property(a => a.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(a => a.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(a => a.DeletedAt);

        // ── Indexes ──────────────────────────────────────────────────
        // Filtered unique index — symbol is unique among active assets.
        builder.HasIndex(a => a.Symbol)
            .HasFilter("[IsActive] = 1")
            .IsUnique()
            .HasDatabaseName("UIX_Assets_Symbol_Active");

        builder.HasIndex(a => a.AssetType)
            .HasDatabaseName("IX_Assets_AssetType");

        // Soft-delete filter.
        builder.HasQueryFilter(a => a.DeletedAt == null);

        builder.Ignore(a => a.DomainEvents);
    }
}