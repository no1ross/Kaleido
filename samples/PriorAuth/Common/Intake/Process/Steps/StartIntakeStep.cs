using Kaleido.Process;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Steps;

[KaleidoAuthorization(Roles = "intake")]
[ProcessStep(
    Name = "StartIntake",
    DisplayName = "Intake - Start",
    Description = "Initiates an intake session and returns the process ID for correlation across all service calls.",
    Version = "1.0.0")]
[AvailableUntil(typeof(CaptureMemberStep))]
[AvailableUntil(typeof(CaptureRequestedServiceStep))]
public sealed record StartIntakeStep;
