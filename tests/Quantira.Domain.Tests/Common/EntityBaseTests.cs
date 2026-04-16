using Quantira.Domain.Common;

namespace Quantira.Domain.Tests.Common;

public sealed class EntityBaseTests
{
    [Fact]
    public void Equals_WithNullObject_ShouldReturnFalse()
    {
        // Arrange
        var entity = new DummyEntity(Guid.NewGuid());

        // Act
        var isEqual = entity.Equals(null);

        // Assert
        Assert.False(isEqual);
    }

    [Fact]
    public void Equals_WithSameReference_ShouldReturnTrue()
    {
        // Arrange
        var entity = new DummyEntity(Guid.NewGuid());

        // Act
        var isEqual = entity.Equals(entity);

        // Assert
        Assert.True(isEqual);
    }

    [Fact]
    public void Equality_WithSameTypeAndSameId_ShouldBeEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var left = new DummyEntity(id);
        var right = new DummyEntity(id);

        // Act
        var areEqual = left == right;

        // Assert
        Assert.True(areEqual);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentId_ShouldNotBeEqual()
    {
        // Arrange
        var left = new DummyEntity(Guid.NewGuid());
        var right = new DummyEntity(Guid.NewGuid());

        // Act
        var areNotEqual = left != right;

        // Assert
        Assert.True(areNotEqual);
        Assert.NotEqual(left, right);
    }

    [Fact]
    public void Equality_WithDifferentTypeAndSameId_ShouldNotBeEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var left = new DummyEntity(id);
        var right = new AnotherDummyEntity(id);

        // Act
        var isEqual = left.Equals(right);

        // Assert
        Assert.False(isEqual);
    }

    [Fact]
    public void DomainEvents_AfterAddAndClear_ShouldReflectExpectedState()
    {
        // Arrange
        var entity = new DummyEntity(Guid.NewGuid());
        var domainEvent = new DummyDomainEvent();

        // Act
        entity.Raise(domainEvent);
        var eventCountAfterRaise = entity.DomainEvents.Count;
        entity.ClearDomainEvents();
        var eventCountAfterClear = entity.DomainEvents.Count;

        // Assert
        Assert.Equal(1, eventCountAfterRaise);
        Assert.Equal(0, eventCountAfterClear);
    }

    [Fact]
    public void OperatorEquality_WithBothNull_ShouldReturnTrue()
    {
        // Arrange
        DummyEntity? left = null;
        DummyEntity? right = null;

        // Act
        var areEqual = left == right;

        // Assert
        Assert.True(areEqual);
    }

    [Fact]
    public void OperatorEquality_WithLeftNullAndRightNonNull_ShouldReturnFalse()
    {
        // Arrange
        DummyEntity? left = null;
        var right = new DummyEntity(Guid.NewGuid());

        // Act
        var areEqual = left == right;

        // Assert
        Assert.False(areEqual);
    }

    private sealed class DummyEntity : Entity<Guid>
    {
        public DummyEntity(Guid id)
        {
            Id = id;
        }

        public void Raise(IDomainEvent domainEvent)
        {
            AddDomainEvent(domainEvent);
        }
    }

    private sealed class AnotherDummyEntity : Entity<Guid>
    {
        public AnotherDummyEntity(Guid id)
        {
            Id = id;
        }
    }

    private sealed class DummyDomainEvent : IDomainEvent
    {
        public Guid EventId { get; } = Guid.NewGuid();
        public DateTime OccurredAt { get; } = DateTime.UtcNow;
    }
}