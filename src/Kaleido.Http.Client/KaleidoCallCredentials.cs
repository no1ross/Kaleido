using System.Security.Claims;

namespace Kaleido.Http.Client;

/// <summary>
/// The host's decision of which <c>Authorization</c> a Kaleido client call sends - one seam for every
/// outgoing call, whatever the hop: a router's MCP tool call, a step handler's handoff or lookup, a
/// registry fetch (ADR 0022).
/// </summary>
/// <remarks>
/// <para>Kaleido asks the provider once per outgoing request, naming the destination service and why
/// the call is made (<see cref="KaleidoCallPurpose"/>). A typical provider exchanges the caller's
/// token for one addressed to the destination (OAuth on-behalf-of / RFC 8693 token exchange), or, for
/// the service's own calls, returns a service token; <c>ForwardCallerToken()</c> registers a built-in
/// provider that sends the caller's token unchanged (deployments where every service accepts it).</para>
/// <para>Kaleido enforces one rule whatever the provider answers: a call made for a caller never goes
/// out with the host's own credentials. An anonymous caller is sent anonymously without asking the
/// provider, and <see cref="KaleidoCallCredential.Configured"/> is refused for a caller's call.</para>
/// <para>Register the provider as a singleton (<c>AddCallCredentials&lt;TProvider&gt;()</c>); it is
/// called concurrently and for every request, so cache exchanged tokens yourself.</para>
/// </remarks>
public interface IKaleidoCallCredentialProvider
{
    /// <summary>The credential the call described by <paramref name="request"/> sends.</summary>
    /// <param name="request">The destination, the purpose and the caller of the call.</param>
    /// <param name="cancellationToken">The call's cancellation.</param>
    /// <returns>
    /// <see cref="KaleidoCallCredential.Header(string)"/> to send a value,
    /// <see cref="KaleidoCallCredential.None"/> to send none, or (for
    /// <see cref="KaleidoCallPurpose.ServiceOwn"/> only) <see cref="KaleidoCallCredential.Configured"/>
    /// to keep what the host's client handlers set.
    /// </returns>
    ValueTask<KaleidoCallCredential> GetCredentialAsync(
        KaleidoCallCredentialRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Why a Kaleido client call is made; decided by Kaleido.</summary>
public enum KaleidoCallPurpose
{
    /// <summary>
    /// For the caller of the inbound request being handled: an MCP tool call, a step handler's
    /// handoff or lookup, a delegated query. The provider must answer for that caller - never with the
    /// host's own credentials.
    /// </summary>
    OnBehalfOfCaller = 0,

    /// <summary>
    /// The service's own call: a registry fetch or refresh (shared by every caller), a health probe,
    /// or any call made outside an inbound request.
    /// </summary>
    ServiceOwn = 1,
}

/// <summary>A Kaleido client call the host must authorize: where it goes, why, and for whom.</summary>
public sealed record KaleidoCallCredentialRequest
{
    /// <summary>The destination service (the client's route prefix, e.g. <c>member</c>).</summary>
    public required string ServiceName { get; init; }

    /// <summary>The configured Kaleido client the call is made through (<c>Kaleido:Clients:{name}</c>).</summary>
    public required string ClientName { get; init; }

    /// <summary>Why the call is made.</summary>
    public required KaleidoCallPurpose Purpose { get; init; }

    /// <summary>
    /// The authenticated caller of the inbound request, for <see cref="KaleidoCallPurpose.OnBehalfOfCaller"/>;
    /// <see langword="null"/> for <see cref="KaleidoCallPurpose.ServiceOwn"/>.
    /// </summary>
    public ClaimsPrincipal? Caller { get; init; }

    /// <summary>
    /// The inbound request's <c>Authorization</c> value (the caller's token, e.g. for an exchange), for
    /// <see cref="KaleidoCallPurpose.OnBehalfOfCaller"/>; <see langword="null"/> otherwise.
    /// </summary>
    public string? CallerAuthorization { get; init; }
}

/// <summary>What a provider answers: send a value, send none, or keep the host's configured one.</summary>
public enum KaleidoCallCredentialKind
{
    /// <summary>Send <see cref="KaleidoCallCredential.Value"/> as the <c>Authorization</c> header.</summary>
    Header = 0,

    /// <summary>Send no <c>Authorization</c> header (an anonymous call).</summary>
    None = 1,

    /// <summary>Keep what the host's client handlers set (only for <see cref="KaleidoCallPurpose.ServiceOwn"/>).</summary>
    Configured = 2,
}

/// <summary>The credential a Kaleido client call sends, as decided by an <see cref="IKaleidoCallCredentialProvider"/>.</summary>
public sealed record KaleidoCallCredential
{
    private KaleidoCallCredential(KaleidoCallCredentialKind kind, string? value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>Send no <c>Authorization</c> header.</summary>
    public static KaleidoCallCredential None { get; } = new(KaleidoCallCredentialKind.None, value: null);

    /// <summary>
    /// Keep what the host's client handlers set (e.g. a service token on the client). Refused for a
    /// <see cref="KaleidoCallPurpose.OnBehalfOfCaller"/> call: a caller's call never goes out with the
    /// host's own credentials.
    /// </summary>
    public static KaleidoCallCredential Configured { get; } = new(KaleidoCallCredentialKind.Configured, value: null);

    /// <summary>What to do with the call's <c>Authorization</c> header.</summary>
    public KaleidoCallCredentialKind Kind { get; }

    /// <summary>The header value for <see cref="KaleidoCallCredentialKind.Header"/>; otherwise <see langword="null"/>.</summary>
    public string? Value { get; }

    /// <summary>Send <paramref name="authorization"/> (e.g. <c>Bearer eyJ…</c>) as the <c>Authorization</c> header.</summary>
    /// <exception cref="ArgumentException"><paramref name="authorization"/> is null, empty or whitespace.</exception>
    public static KaleidoCallCredential Header(string authorization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorization);
        return new KaleidoCallCredential(KaleidoCallCredentialKind.Header, authorization);
    }
}
