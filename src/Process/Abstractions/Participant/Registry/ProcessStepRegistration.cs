namespace Kaleido.Process.Participant.Registry;

public sealed record ProcessStepRegistration(
    Type StepType,
    Type StepResultType,
    Type HandlerType,
    IReadOnlyCollection<ProcessStepRegistration> Dependencies,
    IReadOnlyCollection<ProcessStepRegistration> AvailableAfter,
    IReadOnlyCollection<ProcessStepRegistration> AvailableUntil,
    ProcessStepMetadata Metadata);
