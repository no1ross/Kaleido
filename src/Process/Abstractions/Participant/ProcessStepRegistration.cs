using Kaleido.Process.Participant.Metadata;

namespace Kaleido.Process.Participant;

public sealed record ProcessStepRegistration(
    Type StepType,
    Type HandlerType,
    ProcessStepMetadata Metadata);
