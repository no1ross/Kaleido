namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when a Kaleido request fails validation — bad field, unsupported operator, invalid page size, etc.
/// Results in a 400 Bad Request when caught by the endpoint handlers.
/// The <see cref="Code"/> is forwarded directly into the HTTP response body (e.g. <see cref="Kaleido.Queryable.QueryableErrorCodes"/>, <see cref="Kaleido.Processor.ProcessorErrorCodes"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class KaleidoValidationException : Exception
{
    /// <summary>
    /// Creates the exception for a request the caller got wrong.
    /// </summary>
    /// <param name="code">
    /// The machine-readable error code, from the area's catalog (e.g.
    /// <see cref="Kaleido.Queryable.QueryableErrorCodes"/>).
    /// </param>
    /// <param name="message">
    /// The explanation for the caller. It is returned in the 400 response body (or, thrown from a
    /// Process step handler, in the failed step's opt-in framework message), so it must not carry
    /// sensitive data.
    /// </param>
    public KaleidoValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Creates the exception for a request the caller got wrong, wrapping the exception that
    /// revealed it.
    /// </summary>
    /// <param name="code">
    /// The machine-readable error code, from the area's catalog (e.g.
    /// <see cref="Kaleido.Queryable.QueryableErrorCodes"/>).
    /// </param>
    /// <param name="message">
    /// The explanation for the caller. It is returned in the 400 response body (or, thrown from a
    /// Process step handler, in the failed step's opt-in framework message), so it must not carry
    /// sensitive data.
    /// </param>
    /// <param name="innerException">The exception that revealed the invalid input.</param>
    public KaleidoValidationException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable error code (e.g. <see cref="Kaleido.Queryable.QueryableErrorCodes"/>, <see cref="Kaleido.Processor.ProcessorErrorCodes"/>). Wire-safe — forwarded directly into 400 response bodies.</summary>
    public string Code { get; }
}
