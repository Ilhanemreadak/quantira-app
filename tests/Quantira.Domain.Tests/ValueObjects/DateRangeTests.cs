using Quantira.Domain.Exceptions;
using Quantira.Domain.ValueObjects;

namespace Quantira.Domain.Tests.ValueObjects;

public sealed class DateRangeTests
{
    [Fact]
    public void Of_WithValidRange_ShouldCreateUtcRange()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Local);
        var end = new DateTime(2026, 1, 31, 18, 0, 0, DateTimeKind.Unspecified);

        // Act
        var range = DateRange.Of(start, end);

        // Assert
        Assert.Equal(DateTimeKind.Utc, range.Start.Kind);
        Assert.Equal(DateTimeKind.Utc, range.End.Kind);
        Assert.Equal(DateTime.SpecifyKind(start, DateTimeKind.Utc), range.Start);
        Assert.Equal(DateTime.SpecifyKind(end, DateTimeKind.Utc), range.End);
    }

    [Fact]
    public void Of_WithEndBeforeStart_ShouldThrowDomainException()
    {
        // Arrange
        var start = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddDays(-1);

        // Act
        var action = () => DateRange.Of(start, end);

        // Assert
        Assert.Throws<DomainException>(action);
    }

    [Fact]
    public void LastDays_ShouldCreateRangeEndingNow()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var range = DateRange.LastDays(7);
        var after = DateTime.UtcNow;

        // Assert
        Assert.InRange(range.End, before, after);
        Assert.True(range.Start <= range.End);
        Assert.True(range.Duration.TotalDays <= 7.0001);
    }

    [Fact]
    public void CurrentMonth_ShouldCreateExpectedBoundaries()
    {
        // Arrange
        var now = DateTime.UtcNow;

        // Act
        var range = DateRange.CurrentMonth();

        // Assert
        Assert.Equal(new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc), range.Start);
        Assert.Equal(range.Start.AddMonths(1).AddTicks(-1), range.End);
    }

    [Fact]
    public void Contains_ShouldBeInclusiveAtBoundaries()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var range = DateRange.Of(start, end);

        // Act
        var containsStart = range.Contains(start);
        var containsEnd = range.Contains(end);
        var containsOutside = range.Contains(end.AddTicks(1));

        // Assert
        Assert.True(containsStart);
        Assert.True(containsEnd);
        Assert.False(containsOutside);
    }

    [Fact]
    public void Contains_WhenDateIsBeforeStart_ShouldReturnFalse()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc);
        var range = DateRange.Of(start, end);

        // Act
        var contains = range.Contains(start.AddTicks(-1));

        // Assert
        Assert.False(contains);
    }

    [Fact]
    public void Overlaps_WithOverlappingRange_ShouldReturnTrue()
    {
        // Arrange
        var left = DateRange.Of(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        var right = DateRange.Of(new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var overlaps = left.Overlaps(right);

        // Assert
        Assert.True(overlaps);
    }

    [Fact]
    public void Overlaps_WithNonOverlappingRange_ShouldReturnFalse()
    {
        // Arrange
        var left = DateRange.Of(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc));
        var right = DateRange.Of(new DateTime(2026, 1, 11, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var overlaps = left.Overlaps(right);

        // Assert
        Assert.False(overlaps);
    }

    [Fact]
    public void Overlaps_WhenCurrentRangeStartsAfterOtherEnds_ShouldReturnFalse()
    {
        // Arrange
        var left = DateRange.Of(new DateTime(2026, 1, 21, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 30, 0, 0, 0, DateTimeKind.Utc));
        var right = DateRange.Of(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 20, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var overlaps = left.Overlaps(right);

        // Assert
        Assert.False(overlaps);
    }

    [Fact]
    public void Equality_WithSameStartAndEnd_ShouldBeEqual()
    {
        // Arrange
        var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var left = DateRange.Of(start, end);
        var right = DateRange.Of(start, end);

        // Act
        var areEqual = left == right;

        // Assert
        Assert.True(areEqual);
        Assert.Equal(left, right);
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void ToString_ShouldReturnExpectedText()
    {
        // Arrange
        var range = DateRange.Of(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var text = range.ToString();

        // Assert
        Assert.Equal("2026-01-01 → 2026-01-02", text);
    }
}