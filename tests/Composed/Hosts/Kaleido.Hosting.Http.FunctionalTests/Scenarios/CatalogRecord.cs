namespace Kaleido.Hosting.Http.FunctionalTests.Scenarios;

public sealed record CatalogRecord : IQueryContext
{
    public string Name { get; init; } = string.Empty;
}
