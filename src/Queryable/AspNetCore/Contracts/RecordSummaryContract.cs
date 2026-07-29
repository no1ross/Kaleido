namespace Kaleido.Queryable.AspNetCore.Contracts;

public sealed record RecordSummaryContract
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? MetadataUrl { get; init; }

    public string? QueryUrl { get; init; }

    public IReadOnlyCollection<NamedQuerySummaryContract>? NamedQueries { get; init; }
}
