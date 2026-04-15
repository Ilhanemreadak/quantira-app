namespace Quantira.Application.Common.Models;

/// <summary>
/// Generic wrapper for paginated query results.
/// Returned by any query handler that supports paging
/// (e.g. <c>GetTradeHistoryQueryHandler</c>).
/// The <see cref="TotalCount"/> allows the frontend to calculate
/// the total number of pages without issuing a separate count query.
/// </summary>
/// <typeparam name="T">The type of items in the current page.</typeparam>
public sealed class PagedResult<T>
{
    /// <summary>The items in the current page.</summary>
    public IReadOnlyList<T> Items { get; }

    /// <summary>Total number of records matching the query filter (unpaged).</summary>
    public int TotalCount { get; }

    /// <summary>The current page number (1-based).</summary>
    public int Page { get; }

    /// <summary>The maximum number of items per page.</summary>
    public int PageSize { get; }

    /// <summary>The total number of pages given <see cref="TotalCount"/> and <see cref="PageSize"/>.</summary>
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Returns <c>true</c> if there is a page before the current one.</summary>
    public bool HasPreviousPage => Page > 1;

    /// <summary>Returns <c>true</c> if there is a page after the current one.</summary>
    public bool HasNextPage => Page < TotalPages;

    public PagedResult(IReadOnlyList<T> items, int totalCount, int page, int pageSize)
    {
        Items = items;
        TotalCount = totalCount;
        Page = page;
        PageSize = pageSize;
    }

    /// <summary>
    /// Creates an empty paged result for the given page configuration.
    /// Useful in handlers when a query returns no records.
    /// </summary>
    public static PagedResult<T> Empty(int page, int pageSize)
        => new([], 0, page, pageSize);
}