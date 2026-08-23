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

    public string? ParticipantId
    {
        get;
        init;
    }

    public Guid? ParticipantInstanceId
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
        && string.IsNullOrWhiteSpace(ParticipantId)
        && ParticipantInstanceId is null
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
