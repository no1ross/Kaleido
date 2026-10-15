using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.Auth;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

// Handoff entry: only callable by a service (Intake) on behalf of a user who
// holds the radiology role. Never called directly by a consumer.
[KaleidoAuthorization(Roles = "radiology", Policy = DevAuthPolicies.InternalCaller)]
[ProcessStep(
    DisplayName = "Start Radiology Intake",
    Description = "Initializes a radiology prior authorization from intake handoff data. " +
                  "Accepts member and procedure information collected by the intake processor.",
    Version = "1.0.0")]
[AvailableUntil<CaptureRequestingProviderStep>]
public sealed record StartRadiologyIntakeStep : IProcessStep
{
    [Description("The member the request is for, if Intake captured one before the handoff.")]
    public Guid? MemberId { get; init; }

    [Description("The member's enrollment (plan coverage), if Intake captured one before the handoff.")]
    public Guid? MemberEnrollmentId { get; init; }

    [Description("The date the service will be rendered, if known.")]
    public DateOnly? DateOfService { get; init; }

    [Required]
    [StringLength(50)]
    [Description("The procedure code of the service that needs to be rendered.")]
    public string CodeValue { get; init; } = string.Empty;

    [Required]
    [Description("The code system (CPT, HCPCS) of the procedure code.")]
    public required ProcedureCodeSystem CodeSystem { get; init; }

    // What the caller stated about the service at intake (see Intake's CaptureRequestedServiceStep).
    [Description("The body part to image, as the caller stated it at intake.")]
    public MriBodyPart? BodyPart { get; init; }

    [Description("The side (left, right, bilateral, none), as the caller stated it at intake.")]
    public Laterality? Laterality { get; init; }

    [Description("Whether contrast was ordered, as the caller stated it at intake.")]
    public ContrastOption? Contrast { get; init; }
}
