namespace Kaleido.Queryable;

/// <summary>
/// Configures the HTTP endpoint surface exposed by Kaleido.Queryable.AspNetCore.
/// </summary>
public class QueryableRouteOptions : KaleidoOptions
{
    public string RoutePrefix { get; set; } = "kaleido";
    public string QueryRoute { get; set; } = "query";
    public string MetadataRoute { get; set; } = "metadata";
    public string QueriesRoute { get; set; } = "queries";
}
