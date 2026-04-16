using Quantira.Domain.Common;

namespace Quantira.Domain.Tests.Common;

public sealed class AggregateRootBaseTests
{
    [Fact]
    public void Constructor_ShouldInitializeCreatedAtAndUpdatedAt()
    {
        // Arrange
        var beforeCreate = DateTime.UtcNow;

        // Act
        var aggregate = new DummyAggregateRoot(Guid.NewGuid());
        var afterCreate = DateTime.UtcNow;

        // Assert
        Assert.InRange(aggregate.CreatedAt, beforeCreate, afterCreate);
        Assert.InRange(aggregate.UpdatedAt, beforeCreate, afterCreate);
        Assert.Null(aggregate.DeletedAt);
        Assert.False(aggregate.IsDeleted);
    }

    [Fact]
    public void MarkUpdated_ShouldRefreshUpdatedAt()
    {
        // Arrange
        var aggregate = new DummyAggregateRoot(Guid.NewGuid());
        var previousUpdatedAt = aggregate.UpdatedAt;

        // Act
        aggregate.Touch();

        // Assert
        Assert.True(aggregate.UpdatedAt >= previousUpdatedAt);
    }

    [Fact]
    public void MarkDeleted_ShouldSetDeletedAtAndUpdatedAt()
    {
        // Arrange
        var aggregate = new DummyAggregateRoot(Guid.NewGuid());
        var previousUpdatedAt = aggregate.UpdatedAt;

        // Act
        aggregate.Remove();

        // Assert
        Assert.NotNull(aggregate.DeletedAt);
        Assert.True(aggregate.IsDeleted);
        Assert.True(aggregate.UpdatedAt >= previousUpdatedAt);
    }

    private sealed class DummyAggregateRoot : AggregateRoot<Guid>
    {
        public DummyAggregateRoot(Guid id)
        {
            Id = id;
        }

        public void Touch()
        {
            MarkUpdated();
        }

        public void Remove()
        {
            MarkDeleted();
        }
    }
}