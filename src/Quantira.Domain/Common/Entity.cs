namespace Quantira.Domain.Common;

/// <summary>
/// Base class for all domain entities. An entity is an object that has
/// a distinct identity that runs through time and different representations.
/// Two entities are considered equal if and only if they share the same
/// type and the same identifier, regardless of their current state.
/// Supports domain event collection so aggregates can raise events
/// that are dispatched after persistence (via <see cref="ClearDomainEvents"/>).
/// </summary>
/// <typeparam name="TId">
/// The type of the entity's identifier. Typically <see cref="Guid"/>.
/// </typeparam>
public abstract class Entity<TId> where TId : notnull
{
    /// <summary>The unique identifier of this entity.</summary>
    public TId Id { get; protected set; } = default!;

    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>
    /// Read-only collection of domain events raised during the current
    /// operation. Dispatched by the infrastructure layer after
    /// <see cref="Microsoft.EntityFrameworkCore.DbContext.SaveChangesAsync"/> succeeds,
    /// then cleared via <see cref="ClearDomainEvents"/>.
    /// </summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>
    /// Registers a domain event to be dispatched after the current
    /// unit of work completes successfully.
    /// </summary>
    /// <param name="domainEvent">The domain event to raise.</param>
    protected void AddDomainEvent(IDomainEvent domainEvent)
        => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Clears all pending domain events. Called by the infrastructure layer
    /// after events have been dispatched to MediatR handlers.
    /// </summary>
    public void ClearDomainEvents()
        => _domainEvents.Clear();

    public override bool Equals(object? obj)
    {
        if (obj is not Entity<TId> other) return false;
        if (ReferenceEquals(this, other)) return true;
        if (GetType() != other.GetType()) return false;
        return Id.Equals(other.Id);
    }

    public override int GetHashCode()
        => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right)
        => !(left == right);
}