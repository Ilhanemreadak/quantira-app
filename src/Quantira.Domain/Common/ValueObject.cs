namespace Quantira.Domain.Common;

/// <summary>
/// Base class for value objects in the Quantira domain.
/// A value object is an immutable type whose equality is determined
/// by its component values rather than by identity. Two value objects
/// with the same components are always considered equal.
/// Use value objects to model domain concepts that have no lifecycle
/// of their own, such as <c>Money</c>, <c>Currency</c>, or <c>DateRange</c>.
/// Subclasses must implement <see cref="GetEqualityComponents"/> to define
/// which properties participate in equality and hash code computation.
/// </summary>
public abstract class ValueObject
{
    /// <summary>
    /// Returns the ordered sequence of values that define equality for
    /// this value object. All meaningful properties should be included.
    /// </summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
    {
        if (obj is null || obj.GetType() != GetType()) return false;
        return GetEqualityComponents()
            .SequenceEqual(((ValueObject)obj).GetEqualityComponents());
    }

    public override int GetHashCode()
        => GetEqualityComponents()
            .Aggregate(0, HashCode.Combine);

    public static bool operator ==(ValueObject? left, ValueObject? right)
        => left?.Equals(right) ?? right is null;

    public static bool operator !=(ValueObject? left, ValueObject? right)
        => !(left == right);
}