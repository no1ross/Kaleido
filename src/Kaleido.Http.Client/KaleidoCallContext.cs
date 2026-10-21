using System.Security.Claims;

namespace Kaleido.Http.Client;

// Why a request is made and for whom, attached to the request where it is built (request scope:
// CorrelationHeaderStamper) and read by the innermost handler (client scope), which cannot see the
// request's services. A request without one is the service's own call.
internal sealed record KaleidoCallContext(
    KaleidoCallPurpose Purpose,
    ClaimsPrincipal? Caller,
    string? CallerAuthorization)
{
    public static readonly HttpRequestOptionsKey<KaleidoCallContext> Key = new("Kaleido.CallContext");

    private static readonly KaleidoCallContext ServiceOwn = new(KaleidoCallPurpose.ServiceOwn, Caller: null, CallerAuthorization: null);

    public static KaleidoCallContext Of(HttpRequestMessage request) =>
        request.Options.TryGetValue(Key, out var context) ? context : ServiceOwn;

    // The service's own call (a registry fetch shared by every caller): never authorized as the caller.
    public static void MarkServiceOwn(HttpRequestMessage request) =>
        request.Options.Set(Key, ServiceOwn);
}
