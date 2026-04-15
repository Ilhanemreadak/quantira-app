using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Quantira.Domain.Entities;

namespace Quantira.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core Fluent API configuration for the <see cref="Alert"/> aggregate.
/// The composite index on (UserId, Status, AssetId) directly supports
/// the <c>AlertCheckJob</c> query pattern: active alerts for evaluation
/// and user-filtered alert lists for the UI.
/// </summary>
public sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        builder.ToTable("Alerts");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()")
            .ValueGeneratedOnAdd();

        builder.Property(a => a.UserId)
            .IsRequired();

        builder.Property(a => a.AssetId)
            .IsRequired();

        builder.Property(a => a.AlertType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(a => a.ConditionJson)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Status)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(a => a.TriggeredAt);

        builder.Property(a => a.ExpiresAt);

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(a => a.UpdatedAt)
            .IsRequired()
            .HasDefaultValueSql("SYSUTCDATETIME()");

        builder.Property(a => a.DeletedAt);

        // ── Indexes ──────────────────────────────────────────────────
        // AlertCheckJob query: all active alerts.
        builder.HasIndex(a => a.Status)
            .HasFilter("[Status] = 'Active'")
            .HasDatabaseName("IX_Alerts_Status_Active");

        // User alert list query: user's alerts by type.
        builder.HasIndex(a => new { a.UserId, a.Status, a.AssetId })
            .HasDatabaseName("IX_Alerts_User_Status_Asset");

        // Soft-delete filter.
        builder.HasQueryFilter(a => a.DeletedAt == null);

        builder.Ignore(a => a.DomainEvents);
        builder.Ignore(a => a.IsActive);
    }
}