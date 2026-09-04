namespace Kaleido.Samples.PriorAuth.Intake.Process.Models;

public sealed record CaptureRequestedServiceResponse
{
    public string ProcessorName { get; init; } = string.Empty;
}
