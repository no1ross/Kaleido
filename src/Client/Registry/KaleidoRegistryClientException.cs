namespace Kaleido.Client.Registry;

/// <summary>A central registry call failed; the transport status is available when the registry answered.</summary>
/// <param name="code">A stable <see cref="RegistryClientErrorCodes"/> diagnostic code.</param>
/// <param name="message">What failed, without credentials.</param>
/// <param name="status">The transport's status, when available.</param>
/// <param name="innerException">The underlying transport failure.</param>
[ExcludeFromCodeCoverage]
public sealed class KaleidoRegistryClientException(string code, string message, string? status = null, Exception? innerException = null)
    : Exception(message, innerException)
{
    /// <summary>The stable diagnostic code.</summary>
    public string Code { get; } = code;

    /// <summary>The transport status of the response, if one was received.</summary>
    public string? Status { get; } = status;
}

/// <summary>Stable diagnostic codes for central registry client failures.</summary>
public static class RegistryClientErrorCodes
{
    /// <summary>The central registry rejected a request.</summary>
    public const string RequestFailed = "registryclient_request_failed";

    /// <summary>The central registry answered successfully without a required document.</summary>
    public const string EmptyResponse = "registryclient_empty_response";

    /// <summary>The central registry answered with an invalid document.</summary>
    public const string InvalidResponse = "registryclient_invalid_response";
}
