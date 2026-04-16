using Quantira.Domain.Common;

namespace Quantira.Domain.Tests.Common;

public sealed class ValueObjectBaseTests
{
    [Fact]
    public void Equals_WithNullObject_ShouldReturnFalse()
    {
        // Arrange
        var valueObject = new DummyValueObject("BTC", 10);

        // Act
        var isEqual = valueObject.Equals(null);

        // Assert
        Assert.False(isEqual);
    }

    [Fact]
    public void Equals_WithDifferentRuntimeType_ShouldReturnFalse()
    {
        // Arrange
        var left = new DummyValueObject("BTC", 10);
        var right = new AnotherDummyValueObject("BTC", 10);

        // Act
        var isEqual = left.Equals(right);

        // Assert
        Assert.False(isEqual);
    }

    [Fact]
    public void Equals_WithSameReference_ShouldReturnTrue()
    {
        // Arrange
        var valueObject = new DummyValueObject("BTC", 10);

        // Act
        var isEqual = valueObject.Equals(valueObject);

        // Assert
        Assert.True(isEqual);
    }

    [Fact]
    public void Equality_WithSameComponents_ShouldBeEqual()
    {
        // Arrange
        var left = new DummyValueObject("BTC", 10);
        var right = new DummyValueObject("BTC", 10);

        // Act
        var areEqual = left == right;

        // Assert
        Assert.True(areEqual);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equality_WithDifferentComponents_ShouldNotBeEqual()
    {
        // Arrange
        var left = new DummyValueObject("BTC", 10);
        var right = new DummyValueObject("ETH", 10);

        // Act
        var areNotEqual = left != right;

        // Assert
        Assert.True(areNotEqual);
        Assert.NotEqual(left, right);
    }

    [Fact]
    public void OperatorEquality_WithBothNull_ShouldReturnTrue()
    {
        // Arrange
        DummyValueObject? left = null;
        DummyValueObject? right = null;

        // Act
        var areEqual = left == right;

        // Assert
        Assert.True(areEqual);
    }

    [Fact]
    public void OperatorEquality_WithLeftNullAndRightNonNull_ShouldReturnFalse()
    {
        // Arrange
        DummyValueObject? left = null;
        var right = new DummyValueObject("BTC", 10);

        // Act
        var areEqual = left == right;

        // Assert
        Assert.False(areEqual);
    }

    private sealed class DummyValueObject(string code, int amount) : ValueObject
    {
        public string Code { get; } = code;
        public int Amount { get; } = amount;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Code;
            yield return Amount;
        }
    }

    private sealed class AnotherDummyValueObject(string code, int amount) : ValueObject
    {
        public string Code { get; } = code;
        public int Amount { get; } = amount;

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Code;
            yield return Amount;
        }
    }
}