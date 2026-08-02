namespace Kaleido.Process.Participant.Registry;

public sealed record ProcessStepRegistration(
    Type StepType,
    Type HandlerType,
    ProcessStepMetadata Metadata);
