namespace Kaleido.Process.Participant.Registry;

public sealed record ProcessStepRegistration(
    Type StepType,
    Type StepResultType,
    Type HandlerType,
    ProcessStepMetadata Metadata);
