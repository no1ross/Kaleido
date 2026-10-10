using System.ComponentModel;
using Kaleido.Samples.PriorAuth.CodeSet.Queryable.ContextSources;
using System.ComponentModel.DataAnnotations;
using Kaleido.Processor;

namespace Kaleido.Samples.PriorAuth.Radiology.Process.Steps;

[ProcessStep(
    DisplayName = "Capture Requested Service",
    Description = "Adds a requested service to the current prior authorization.",
    Version = "1.0.0")]
// Radiology is entered through StartRadiologyIntake (the member may have been
// captured in Intake), so adding services opens once the intake has started.
[AvailableAfter<StartRadiologyIntakeStep>]
[AvailableUntil<CaptureRequestingProviderStep>]
[Repeatable]
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
}
