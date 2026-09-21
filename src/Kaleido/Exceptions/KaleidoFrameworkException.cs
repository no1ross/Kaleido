namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when the Kaleido framework encounters an internal integrity violation at runtime.
/// This indicates a bug in the framework or broken DI wiring, not a user error.
/// Results in a 500 Internal Server Error when caught by the exception middleware.
/// </summary>
public sealed class KaleidoFrameworkException : Exception
{
    public KaleidoFrameworkException(string message)
        : base(message) { }

    public KaleidoFrameworkException(string message, Exception innerException)
        : base(message, innerException) { }
}
