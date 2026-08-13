using Kaleido.Samples.ECommerce.Process;

namespace Kaleido.Samples.ECommerce.Process.Responses;

public sealed record ProcessIssue
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public Severity Severity { get; init; }
}