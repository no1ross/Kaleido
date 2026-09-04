namespace Kaleido.Samples.PriorAuth.ProviderSearch.Models;

public sealed record QueryableErrorResponse
{
    public IReadOnlyCollection<QueryableError> Errors { get; init; } = [];
}

public sealed record QueryableError
{
    public string Code { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
