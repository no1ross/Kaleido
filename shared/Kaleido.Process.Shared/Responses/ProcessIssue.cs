using Kaleido.Samples.ECommerce;

namespace Kaleido.Samples.ECommerce.Responses;

public sealed record ProcessIssue
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public Severity Severity { get; init; }
}