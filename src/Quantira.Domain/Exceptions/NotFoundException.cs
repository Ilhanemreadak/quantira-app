namespace Quantira.Domain.Exceptions;

/// <summary>
/// Thrown when a requested domain entity or aggregate cannot be found
/// in the data store. Caught by <c>ExceptionHandlingMiddleware</c> and
/// mapped to HTTP 404 Not Found. Keeps handler code clean by avoiding
/// explicit null checks — handlers throw this exception and the middleware
/// handles the HTTP mapping centrally.
/// </summary>
public sealed class NotFoundException : Exception
{
    /// <summary>The name of the entity type that was not found.</summary>
    public string EntityName { get; }

    /// <summary>The identifier that was looked up.</summary>
    public object EntityId { get; }

    /// <summary>
    /// Initializes a new <see cref="NotFoundException"/>.
    /// </summary>
    /// <param name="entityName">
    /// The name of the entity type (e.g. <c>nameof(Portfolio)</c>).
    /// </param>
    /// <param name="entityId">The identifier that was not found.</param>
    public NotFoundException(string entityName, object entityId)
        : base($"{entityName} with id '{entityId}' was not found.")
    {
        EntityName = entityName;
        EntityId = entityId;
    }
}