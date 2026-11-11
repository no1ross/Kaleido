namespace Kaleido.Process.Context;

/// <summary>
/// <para>Represents the current state of an individual process step.</para>
/// <para>
/// This is intentionally a lightweight summary and should not
/// contain historical information. Operational history is emitted
/// through process events.
/// </para>
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record StepContext
{
    /// <summary>
    /// Unique process step name.
    /// </summary>
    public string StepName
    {
        get;
        init;
    }
        = string.Empty;

    /// <summary>
    /// Last known execution status for this step.
    /// </summary>
    public StepExecutionStatus Status
    {
        get;
        init;
    }

    /// <summary>
    /// Request identifier associated with the most recent
    /// update to this step.
    /// </summary>
    public string? LatestRequestId
    {
        get;
        init;
    }

    /// <summary>
    /// Timestamp of the first execution attempt; set once, never overwritten.
    /// </summary>
    public DateTimeOffset? FirstExecuted
    {
        get;
        init;
    }

    /// <summary>
    /// The caller of the first execution attempt (a tag); set once, never overwritten.
    /// </summary>
    public KaleidoCaller? StartedBy
    {
        get;
        init;
    }

    /// <summary>
    /// Timestamp of the most recent execution attempt.
    /// </summary>
    public DateTimeOffset? LastExecuted
    {
        get;
        init;
    }

    /// <summary>
    /// The caller of the most recent execution attempt (a tag).
    /// </summary>
    public KaleidoCaller? LastUpdatedBy
    {
        get;
        init;
    }
}
