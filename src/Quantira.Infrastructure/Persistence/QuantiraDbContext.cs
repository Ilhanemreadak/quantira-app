using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Quantira.Domain.Common;
using Quantira.Domain.Entities;
using Quantira.Domain.Interfaces;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Quantira.Infrastructure.Persistence;

/// <summary>
/// The central EF Core database context for Quantira.
/// Extends <see cref="IdentityDbContext"/> to integrate ASP.NET Core Identity
/// tables (users, roles, claims) alongside the Quantira domain tables.
/// Implements <see cref="IUnitOfWork"/> so the application layer can call
/// <see cref="SaveChangesAsync"/> without a direct reference to EF Core.
/// Applies all entity configurations from the <c>Configurations/</c> folder
/// via <c>ApplyConfigurationsFromAssembly</c> — adding a new entity config
/// file is the only step needed to wire up a new table.
/// Dispatches domain events after a successful save via MediatR so handlers
/// are invoked within the same request scope as the originating command.
/// Global query filters enforce soft-delete across all aggregate roots
/// so deleted records are never accidentally returned.
/// </summary>
public sealed class QuantiraDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IUnitOfWork
{
    private readonly IMediator _mediator;

    public QuantiraDbContext(
        DbContextOptions<QuantiraDbContext> options,
        IMediator mediator)
        : base(options)
    {
        _mediator = mediator;
    }

    // ── Domain tables ────────────────────────────────────────────────
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Trade> Trades => Set<Trade>();
    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Auto-discovers all IEntityTypeConfiguration<T> implementations
        // in this assembly — no manual registration needed per entity.
        builder.ApplyConfigurationsFromAssembly(typeof(QuantiraDbContext).Assembly);

        // Rename Identity tables to follow Quantira naming convention.
        builder.Entity<ApplicationUser>().ToTable("Users");
        builder.Entity<IdentityRole<Guid>>().ToTable("Roles");
        builder.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        builder.Entity<IdentityUserClaim<Guid>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<Guid>>().ToTable("UserLogins");
        builder.Entity<IdentityUserToken<Guid>>().ToTable("UserTokens");
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable("RoleClaims");
    }

    /// <summary>
    /// Persists all tracked changes and dispatches any pending domain events.
    /// Domain events are dispatched after the database write succeeds so
    /// handlers always operate on committed data.
    /// </summary>
    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await base.SaveChangesAsync(cancellationToken);
        await DispatchDomainEventsAsync(cancellationToken);
        return result;
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        var entities = ChangeTracker
            .Entries<Entity<Guid>>()
            .Where(e => e.Entity.DomainEvents.Count != 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        entities.ForEach(e => e.ClearDomainEvents());

        foreach (var domainEvent in domainEvents)
            await _mediator.Publish(domainEvent, cancellationToken);
    }
}