using Microsoft.Extensions.Http;

namespace Kaleido.Http.Client;

/// <summary>
/// Makes Kaleido client calls carry a front-door caller's own credentials. A front door — a router
/// proxy, an AI-tool (MCP) adapter — calls services <b>as the caller</b>: the inbound
/// <c>Authorization</c> value is forwarded unchanged, so each service authorizes the real user with
/// the user's real roles, exactly as for a direct call (the same rule a reverse proxy applies to
/// the requests it forwards).
/// </summary>
/// <remarks>
/// <para>Opt-in per call: only calls made inside <see cref="ForwardCaller"/> are affected. Registry
/// fetches and ordinary service-to-service calls keep the authentication the host configured on its
/// named clients (service or on-behalf-of tokens).</para>
/// <para>Inside the scope, whatever the host's configuration put on the request is replaced: the
/// caller's value is sent as is, and an anonymous caller (no value) stays anonymous. The client never
/// mints, upgrades or adds identity — in particular no service token or actor claim reaches a service
/// through a front door.</para>
/// </remarks>
internal interface IKaleidoCallerForwarding
{
    /// <summary>True inside a <see cref="ForwardCaller"/> scope (on this async flow).</summary>
    bool IsForwarding { get; }

    /// <summary>The caller's <c>Authorization</c> value in the current scope (null = anonymous).</summary>
    string? CallerAuthorization { get; }

    /// <summary>
    /// Starts a scope in which Kaleido client calls send <paramref name="callerAuthorization"/> (the
    /// inbound <c>Authorization</c> header, unchanged) instead of the host's configured credentials.
    /// Pass null or empty for an anonymous caller. Dispose the scope to end it.
    /// </summary>
    IDisposable ForwardCaller(string? callerAuthorization);
}

internal sealed class KaleidoCallerForwarding : IKaleidoCallerForwarding
{
    private static readonly AsyncLocal<CallerScope?> Current = new();

    public bool IsForwarding => Current.Value is not null;

    public string? CallerAuthorization => Current.Value?.Authorization;

    public IDisposable ForwardCaller(string? callerAuthorization)
    {
        var previous = Current.Value;

        Current.Value = new CallerScope(
            string.IsNullOrWhiteSpace(callerAuthorization) ? null : callerAuthorization);

        return new Restore(previous);
    }

    private sealed record CallerScope(string? Authorization);

    private sealed class Restore(CallerScope? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}

/// <summary>
/// The innermost handler of every Kaleido named client: inside a
/// <see cref="IKaleidoCallerForwarding.ForwardCaller"/> scope it replaces the request's
/// <c>Authorization</c> with the caller's (or removes it for an anonymous caller).
/// </summary>
internal sealed class CallerForwardingHandler(
    IKaleidoCallerForwarding forwarding)
    : DelegatingHandler
{
    private const string AuthorizationHeader = "Authorization";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (forwarding.IsForwarding)
        {
            request.Headers.Remove(AuthorizationHeader);

            if (forwarding.CallerAuthorization is { } authorization)
            {
                request.Headers.TryAddWithoutValidation(AuthorizationHeader, authorization);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// Adds <see cref="CallerForwardingHandler"/> to Kaleido's named clients after every handler the
/// host configured, so it runs last before the network and its <c>Authorization</c> is the one
/// sent. Other named clients in the application are left alone.
/// </summary>
internal sealed class CallerForwardingHandlerFilter(
    KaleidoProcessorClientRouteOptionsMap processClientMap,
    KaleidoQueryableClientRouteOptionsMap queryableClientMap,
    IKaleidoCallerForwarding forwarding)
    : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        ArgumentNullException.ThrowIfNull(next);

        return builder =>
        {
            next(builder);

            if (builder.Name is { } name &&
                (processClientMap.Options.ContainsKey(name) || queryableClientMap.Options.ContainsKey(name)))
            {
                builder.AdditionalHandlers.Add(new CallerForwardingHandler(forwarding));
            }
        };
    }
}
