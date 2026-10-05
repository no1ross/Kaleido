namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when a Kaleido request fails validation — bad field, unsupported operator, invalid page size, etc.
/// Results in a 400 Bad Request when caught by the endpoint handlers.
/// The <see cref="Code"/> is forwarded directly into the HTTP response body (e.g. <see cref="Kaleido.Queryable.QueryableErrorCodes"/>, <see cref="Kaleido.Processor.ProcessorErrorCodes"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class KaleidoValidationException : Exception
{
    public KaleidoValidationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public KaleidoValidationException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable error code (e.g. <see cref="Kaleido.Queryable.QueryableErrorCodes"/>, <see cref="Kaleido.Processor.ProcessorErrorCodes"/>). Wire-safe — forwarded directly into 400 response bodies.</summary>
    public string Code { get; }
}
