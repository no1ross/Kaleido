namespace Kaleido.IntegrationTests.TestArtifacts;

[QueryContext(
    Name = "test-query",
    Description = "Test query context",
    Version = "1.0.0")]
public sealed record TestQueryContext
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
