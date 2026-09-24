namespace Kaleido.Exceptions;

/// <summary>
/// Thrown when the Kaleido framework encounters an internal integrity violation at runtime.
/// This indicates a bug in the framework or broken DI wiring, not a user error.
/// Results in a 500 Internal Server Error when caught by the exception middleware.
/// The <see cref="Code"/> is a stable machine-readable diagnostic code (see <see cref="FrameworkErrorCodes"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed class KaleidoFrameworkException : Exception
{
    public KaleidoFrameworkException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public KaleidoFrameworkException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable diagnostic code (see <see cref="FrameworkErrorCodes"/>). Log-only — not forwarded to HTTP responses.</summary>
    public string Code { get; }
}

/// <summary>
/// Stable machine-readable diagnostic codes for Kaleido framework integrity violations.
/// These are log-only — they are never forwarded to HTTP response bodies.
/// Use the stack trace alongside the code to locate the source of the violation.
/// </summary>
public static class FrameworkErrorCodes
{
    /// <summary>A required method or property could not be located via reflection.</summary>
    public const string ReflectionError     = "reflection_error";

    /// <summary>A registered or resolved type does not match the expected type at runtime.</summary>
    public const string TypeMismatch        = "type_mismatch";

    /// <summary>A required entry was not found in an internal registry at runtime.</summary>
    public const string MissingRegistration = "missing_registration";

    /// <summary>A handler returned an unexpected or null result.</summary>
    public const string InvalidHandlerResult = "invalid_handler_result";

    /// <summary>A CLR type was passed to DataTypeMapper that it does not support.</summary>
    public const string UnsupportedDataType = "unsupported_data_type";

    /// <summary>DataTypeMapper failed to convert a value to the target type.</summary>
    public const string DataConversionError = "data_conversion_error";
}
