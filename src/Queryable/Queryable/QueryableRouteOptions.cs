namespace Kaleido.Queryable.AspNetCore;

/// <summary>
/// Configures the HTTP endpoint surface exposed by Kaleido.Queryable.AspNetCore.
/// </summary>
public sealed class QueryableRouteOptions
{
    public string RoutePrefix { get; set; } = "/kaleido/queryable";
    public string QueryRoute { get; set; } = "query";
    public string MetadataRoute { get; set; } = "metadata";
    public string QueriesRoute { get; set; } = "queries";

}
