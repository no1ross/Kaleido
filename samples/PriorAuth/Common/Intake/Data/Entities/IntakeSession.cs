namespace Kaleido.Samples.PriorAuth.Intake.Data.Entities;

public sealed class IntakeSession
{
    public Guid IntakeSessionId { get; set; }

    public Guid ProcessId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public IntakeSessionMember? Member { get; set; }

    public IntakeSessionProcedure? Procedure { get; set; }
}
