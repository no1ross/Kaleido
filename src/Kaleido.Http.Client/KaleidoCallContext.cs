using System.Security.Claims;

namespace Kaleido.Http.Client;

/// <summary>
/// Why a Kaleido client call is made and for whom, read-only, for the host's own handlers on the Kaleido
/// clients (<c>AddKaleidoClientHandler&lt;THandler&gt;()</c>, #574): a client-credentials handler sets the
/// service's token on its own calls; an exchange handler exchanges <see cref="CallerAuthorization"/> for the
/// caller's calls. Read it with <see cref="Of"/>.
/// </summary>
/// <remarks>
/// Attached to the request where it is built (the request's scope) and read by the handlers (the client's
/// pipeline), which cannot see the request's services. A request without one is the service's own call.
/// <see cref="CallerAuthorization"/> holds a credential: never log it.
/// </remarks>
public sealed class KaleidoCallContext
{
    internal static readonly HttpRequestOptionsKey<KaleidoCallContext> Key = new("Kaleido.CallContext");

    private static readonly KaleidoCallContext ServiceOwn = new(KaleidoCallPurpose.ServiceOwn, caller: null, callerAuthorization: null);

    internal KaleidoCallContext(
        KaleidoCallPurpose purpose,
        ClaimsPrincipal? caller,
        string? callerAuthorization)
    {
        Purpose = purpose;
        Caller = caller;
        CallerAuthorization = callerAuthorization;
    }

    /// <summary>Why the call is made.</summary>
    public KaleidoCallPurpose Purpose { get; }

    /// <summary>
    /// The authenticated caller of the inbound request, for <see cref="KaleidoCallPurpose.OnBehalfOfCaller"/>;
    /// <see langword="null"/> for an anonymous caller and for <see cref="KaleidoCallPurpose.ServiceOwn"/>.
    /// </summary>
    public ClaimsPrincipal? Caller { get; }

    /// <summary>
    /// The inbound request's <c>Authorization</c> value (the caller's token, e.g. to exchange), for
    /// <see cref="KaleidoCallPurpose.OnBehalfOfCaller"/>; <see langword="null"/> otherwise.
    /// </summary>
    public string? CallerAuthorization { get; }

    /// <summary>The context of <paramref name="request"/>: the service's own call when none is attached.</summary>
    /// <param name="request">A request sent through a Kaleido client.</param>
    public static KaleidoCallContext Of(HttpRequestMessage request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.Options.TryGetValue(Key, out var context) ? context : ServiceOwn;
    }

    // A call made while handling an inbound request, for its caller (null: anonymous).
    internal static KaleidoCallContext ForCaller(ClaimsPrincipal? caller, string? callerAuthorization) =>
        new(KaleidoCallPurpose.OnBehalfOfCaller, caller, callerAuthorization);

    // The service's own call (a registry fetch shared by every caller): never authorized as the caller.
    internal static void MarkServiceOwn(HttpRequestMessage request) =>
        request.Options.Set(Key, ServiceOwn);
}
