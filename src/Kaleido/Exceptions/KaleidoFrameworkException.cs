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
    /// <summary>
    /// Creates the exception for an integrity violation the framework detected, such as a member
    /// that reflection could not find or a resolved type that does not match.
    /// </summary>
    /// <param name="code">The diagnostic code, a <see cref="FrameworkErrorCodes"/> value.</param>
    /// <param name="message">
    /// A description of the violation. Keep it free of sensitive data: the HTTP exception handler
    /// writes it to the 500 response body.
    /// </param>
    public KaleidoFrameworkException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Creates the exception for an integrity violation the framework detected, wrapping the
    /// exception that revealed it.
    /// </summary>
    /// <param name="code">The diagnostic code, a <see cref="FrameworkErrorCodes"/> value.</param>
    /// <param name="message">
    /// A description of the violation. Keep it free of sensitive data: the HTTP exception handler
    /// writes it to the 500 response body.
    /// </param>
    /// <param name="innerException">The exception that revealed the violation.</param>
    public KaleidoFrameworkException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>Stable machine-readable diagnostic code (see <see cref="FrameworkErrorCodes"/>). Logged, and written with <see cref="Exception.Message"/> to the 500 response body by the HTTP exception handler.</summary>
    public string Code { get; }
}

/// <summary>
/// Stable machine-readable diagnostic codes for Kaleido framework integrity violations.
/// They are logged, and the HTTP exception handler also writes them to the 500 response body.
/// Use the stack trace alongside the code to locate the source of the violation.
/// </summary>
public static class FrameworkErrorCodes
{
    /// <summary>A required method or property could not be located via reflection.</summary>
    public const string ReflectionError = "reflection_error";

    /// <summary>A registered or resolved type does not match the expected type at runtime.</summary>
    public const string TypeMismatch = "type_mismatch";

    /// <summary>A required entry was not found in an internal registry at runtime.</summary>
    public const string MissingRegistration = "missing_registration";

    /// <summary>A handler returned an unexpected or null result.</summary>
    public const string InvalidHandlerResult = "invalid_handler_result";

    /// <summary>
    /// A published input or result has a type the metadata cannot describe: a dictionary whose key type
    /// is not <see langword="string"/> (JSON property names are strings). Raised when the capability is
    /// registered, as a <see cref="KaleidoConfigurationException"/>.
    /// </summary>
    public const string UnsupportedDataType = "unsupported_data_type";
}
