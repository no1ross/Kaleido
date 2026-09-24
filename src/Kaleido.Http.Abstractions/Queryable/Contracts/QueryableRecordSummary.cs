namespace Kaleido.Http.Queryable.Contracts;

[ExcludeFromCodeCoverage]
public sealed record QueryableRecordSummary
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public string? MetadataUrl { get; init; }
}
