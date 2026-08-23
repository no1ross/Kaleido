namespace Kaleido.Observability;

public sealed record KaleidoCorrelationContext
{
    public string? RequestId
    {
        get;
        init;
    }

    public Guid? ParticipantProcessInstanceId
    {
        get;
        init;
    }

    public Guid? OrchestratorProcessInstanceId
    {
        get;
        init;
    }

    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(RequestId)
        && ParticipantProcessInstanceId is null
        && OrchestratorProcessInstanceId is null;
}

public interface IKaleidoCorrelationContextAccessor
{
    KaleidoCorrelationContext Current
    {
        get;
    }
}
