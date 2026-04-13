namespace Quantira.Domain.Exceptions;

/// <summary>
/// Represents an error that originates from a violated domain business rule.
/// Thrown when an operation would leave the domain in an invalid or inconsistent state
/// (e.g. adding a trade to a deleted portfolio, setting a negative quantity).
/// Caught by <c>ExceptionHandlingMiddleware</c> in the WebAPI layer and
/// mapped to HTTP 422 Unprocessable Entity so the client receives a
/// meaningful error message without exposing internal stack traces.
/// </summary>
public sealed class DomainException : Exception
{
    /// <summary>
    /// Initializes a new <see cref="DomainException"/> with a descriptive message
    /// explaining which business rule was violated.
    /// </summary>
    /// <param name="message">
    /// A human-readable explanation of the violated rule.
    /// Should be specific enough to help the caller understand what went wrong.
    /// </param>
    public DomainException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new <see cref="DomainException"/> with a message and an
    /// inner exception. Use when wrapping a lower-level exception in domain terms.
    /// </summary>
    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}