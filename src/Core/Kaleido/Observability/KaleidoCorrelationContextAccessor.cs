using Kaleido.Observability;

namespace Kaleido.Observability;

internal interface IKaleidoCorrelationContextInitializer
{
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
