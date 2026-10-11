using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Kaleido.Samples.PriorAuth.CodeSet.Queryable.ContextSources;

namespace Kaleido.Samples.PriorAuth.Intake.Process.Steps;

[ProcessStep(
    DisplayName = "Intake - Capture Requested Service",
    Description = "Validates the procedure code and determines which processor should handle this prior authorization request.",
    Version = "1.0.0")]
public sealed record CaptureRequestedServiceStep : IProcessStep
{
    [Required]
    [StringLength(50)]
    [Description("The procedure code of the service that needs to be rendered, found with Procedure Codes from what the caller describes or the code they give.")]
    [Display(Name = "Requested service", Prompt = "What service needs to be rendered?")]
    [Lookup(
        Service = "codeset",
        Source = nameof(ProcedureCodeQueryContextSource),
        Field = "CodeValue",
        Display = ["CodeValue", "ShortDescription", "CodeSystem", "RequiresAuthorization"])]
    public string CodeValue { get; init; } = string.Empty;

    [Required]
    [Description("The code system (CPT, HCPCS) of the same Procedure Codes result.")]
    [Lookup(
        Service = "codeset",
        Source = nameof(ProcedureCodeQueryContextSource),
        Field = "CodeSystem")]
    public ProcedureCodeSystem CodeSystem { get; init; }

    // What the caller stated about this service up front. Stored with the service by binding key
    // (BodyPart, Laterality, Contrast); configured questions with the same binding key propose them.
    [Description("The body part to image, only if the caller said it (e.g. \"knee\"); leave it out otherwise and never infer it from the procedure code. Proposed back to the caller for confirmation when the service asks.")]
    [Display(Name = "Body part")]
    public MriBodyPart? BodyPart { get; init; }

    [Description("The side (left, right, bilateral, none), only if the caller said it; leave it out otherwise. Proposed back to the caller for confirmation, never answered for them.")]
    [Display(Name = "Side")]
    public Laterality? Laterality { get; init; }

    [Description("Whether contrast was ordered, only if the caller said it (e.g. \"without contrast\"); leave it out otherwise and never infer it from the procedure code. Proposed back to the caller for confirmation.")]
    [Display(Name = "Contrast")]
    public ContrastOption? Contrast { get; init; }
}
