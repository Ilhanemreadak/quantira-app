namespace Quantira.Domain.Common;

/// <summary>
/// Base class for aggregate roots in the Quantira domain.
/// An aggregate root is the entry point to a cluster of domain objects
/// (entities and value objects) that must be treated as a single unit
/// for data changes. All external interactions with the aggregate must
/// go through the root — never directly through child entities.
/// Extends <see cref="Entity{TId}"/> and adds audit timestamps and
/// soft-delete support. All timestamps are stored and compared in UTC.
/// </summary>
/// <typeparam name="TId">
/// The type of the aggregate's identifier. Typically <see cref="Guid"/>.
/// </typeparam>
public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    /// <summary>UTC timestamp of when this aggregate was first created.</summary>
    public DateTime CreatedAt { get; protected set; }

    /// <summary>
    /// UTC timestamp of the last modification to this aggregate.
    /// Updated automatically by <see cref="MarkUpdated"/>.
    /// </summary>
    public DateTime UpdatedAt { get; protected set; }

    /// <summary>
    /// UTC timestamp of when this aggregate was soft-deleted.
    /// <c>null</c> means the aggregate is active.
    /// </summary>
    public DateTime? DeletedAt { get; protected set; }

    /// <summary>
    /// Returns <c>true</c> if this aggregate has been soft-deleted.
    /// Soft-deleted aggregates are filtered out by global query filters
    /// in <c>QuantiraDbContext</c> and are never returned to the application layer.
    /// </summary>
    public bool IsDeleted => DeletedAt.HasValue;

    protected AggregateRoot()
    {
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Updates the <see cref="UpdatedAt"/> timestamp to the current UTC time.
    /// Should be called at the end of any method that mutates the aggregate's state.
    /// </summary>
    protected void MarkUpdated()
        => UpdatedAt = DateTime.UtcNow;

    /// <summary>
    /// Soft-deletes this aggregate by setting <see cref="DeletedAt"/> to the
    /// current UTC time. The record is retained in the database for audit
    /// and compliance purposes but is excluded from all standard queries.
    /// </summary>
    protected void MarkDeleted()
    {
        DeletedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }
}