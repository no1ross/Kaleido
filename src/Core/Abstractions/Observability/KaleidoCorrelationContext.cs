namespace Kaleido.Observability;

public sealed record KaleidoCorrelationContext
{
    public string? RequestId
    {
        get;
        init;
    }

    public Guid? ProcessId
    {
        get;
        init;
    }

    public Guid? ProcessorInstanceId
    {
        get;
        init;
    }

    public string? SourceProcessorName
    {
        get;
        init;
    }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(RequestId)
        && ProcessId is null
        && ProcessorInstanceId is null
        && string.IsNullOrWhiteSpace(SourceProcessorName);
}

public interface IKaleidoCorrelationContextAccessor
{
    KaleidoCorrelationContext Current
    {
        get;
    }
}
