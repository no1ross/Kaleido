public sealed record NamedQuerySummaryContract
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required string ExecuteUrl { get; init; }

    public required string MetadataUrl { get; init; }
}
