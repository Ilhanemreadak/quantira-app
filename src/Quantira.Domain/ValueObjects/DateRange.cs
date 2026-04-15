using Quantira.Domain.Common;
using Quantira.Domain.Exceptions;

namespace Quantira.Domain.ValueObjects;

/// <summary>
/// Represents a closed date range with an inclusive start and end.
/// Used for report period filtering, alert expiry windows, and
/// price history queries. All dates are compared and stored in UTC.
/// Prevents invalid ranges (end before start) at construction time
/// so callers never need to validate range consistency themselves.
/// </summary>
public sealed class DateRange : ValueObject
{
    /// <summary>The inclusive start of the date range (UTC).</summary>
    public DateTime Start { get; }

    /// <summary>The inclusive end of the date range (UTC).</summary>
    public DateTime End { get; }

    private DateRange(DateTime start, DateTime end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Creates a new <see cref="DateRange"/>.
    /// </summary>
    /// <param name="start">Range start (UTC).</param>
    /// <param name="end">Range end (UTC).</param>
    /// <exception cref="DomainException">
    /// Thrown when <paramref name="end"/> is before <paramref name="start"/>.
    /// </exception>
    public static DateRange Of(DateTime start, DateTime end)
    {
        if (end < start)
            throw new DomainException(
                $"End date ({end:O}) cannot be before start date ({start:O}).");

        return new DateRange(
            DateTime.SpecifyKind(start, DateTimeKind.Utc),
            DateTime.SpecifyKind(end, DateTimeKind.Utc));
    }

    /// <summary>
    /// Creates a date range spanning the last <paramref name="days"/> days
    /// up to and including now (UTC).
    /// </summary>
    public static DateRange LastDays(int days)
    {
        var now = DateTime.UtcNow;
        return Of(now.AddDays(-days), now);
    }

    /// <summary>
    /// Creates a date range for the current calendar month (UTC).
    /// </summary>
    public static DateRange CurrentMonth()
    {
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1).AddTicks(-1);
        return Of(start, end);
    }

    /// <summary>The total duration of this range.</summary>
    public TimeSpan Duration => End - Start;

    /// <summary>Returns <c>true</c> if the given UTC date falls within this range.</summary>
    public bool Contains(DateTime date) => date >= Start && date <= End;

    /// <summary>Returns <c>true</c> if this range overlaps with <paramref name="other"/>.</summary>
    public bool Overlaps(DateRange other) => Start <= other.End && End >= other.Start;

    public override string ToString() => $"{Start:yyyy-MM-dd} → {End:yyyy-MM-dd}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}