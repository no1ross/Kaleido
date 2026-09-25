namespace Kaleido.Observability;

/// <summary>
/// Provides read access to the current request's Kaleido correlation context.
/// </summary>
/// <remarks>
/// <para>
/// Registered as a scoped singleton by <c>AddKaleido()</c>. The context is
/// populated at the start of each HTTP request by <c>ObservabilityMiddleware</c>,
/// which reads inbound <c>X-Kaleido-*</c> headers and calls
/// <c>IKaleidoCorrelationContextInitializer.Initialize</c> on the same underlying
/// instance.
/// </para>
/// <para>
/// Consumers (process services, client factories, custom middleware) should inject
/// this interface to read the active <see cref="KaleidoCorrelationContext"/> — for
/// example, to forward the correlation headers on outbound HTTP calls or to stamp
/// process execution requests with the inbound process ID.
/// </para>
/// <para>
/// Use <c>TryAddScoped</c> before <c>AddKaleido()</c> to substitute a test-controlled
/// accessor in unit and functional tests.
/// </para>
/// </remarks>
public interface IKaleidoCorrelationContextAccessor
{
    /// <summary>
    /// Gets the correlation context for the current request scope.
    /// Returns a default (empty) context when no correlation headers were present
    /// on the inbound request or when called outside of an HTTP request scope.
    /// </summary>
    KaleidoCorrelationContext Current
    {
        get;
    }
}

/// <summary>
/// Write side of the correlation context: populates the current request's
/// <see cref="KaleidoCorrelationContext"/> from inbound transport headers.
/// </summary>
/// <remarks>
/// Implemented by the same scoped instance as <see cref="IKaleidoCorrelationContextAccessor"/>.
/// Transport-layer middleware (e.g. <c>ObservabilityMiddleware</c> in <c>Kaleido.Http</c>)
/// resolves this interface to stamp the context once per request. Consumers should
/// inject <see cref="IKaleidoCorrelationContextAccessor"/> for read access only.
/// </remarks>
public interface IKaleidoCorrelationContextInitializer
{
    /// <summary>
    /// Sets the correlation context for the current request scope.
    /// Should be called exactly once per request, before any process or queryable
    /// handling begins.
    /// </summary>
    void Initialize(KaleidoCorrelationContext context);
}

internal sealed class KaleidoCorrelationContextAccessor
    : IKaleidoCorrelationContextAccessor,
      IKaleidoCorrelationContextInitializer
{
    private KaleidoCorrelationContext _current =
        new();

    public KaleidoCorrelationContext Current =>
        _current;

    public void Initialize(KaleidoCorrelationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        _current = context;
    }
}
