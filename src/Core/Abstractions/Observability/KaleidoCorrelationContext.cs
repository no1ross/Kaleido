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

    public string? ProcessorId
    {
        get;
        init;
    }

    public Guid? ProcessorInstanceId
    {
        get;
        init;
    }

    public string? OrchestratorId
    {
        get;
        init;
    }

    public Guid? OrchestratorInstanceId
    {
        get;
        init;
    }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(RequestId)
        && ProcessId is null
        && string.IsNullOrWhiteSpace(ProcessorId)
        && ProcessorInstanceId is null
        && string.IsNullOrWhiteSpace(OrchestratorId)
        && OrchestratorInstanceId is null;
}

public interface IKaleidoCorrelationContextAccessor
{
    KaleidoCorrelationContext Current
    {
        get;
    }
}
