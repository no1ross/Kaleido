namespace Kaleido.Queryable.AspNetCore;

/// <summary>
/// Configures the HTTP endpoint surface exposed by Kaleido.Queryable.AspNetCore.
/// </summary>
public sealed class KaleidoQueryableEndpointOptions
{
    /// <summary>
    /// Gets or sets the root route prefix used for all Kaleido Queryable endpoints.
    /// </summary>
    public string RoutePrefix { get; set; } = "/kaleido";

    /// <summary>
    /// Gets or sets the route used to discover queryable registrations.
    /// Defaults to <c>/queries</c>, producing <c>GET /kaleido/queries</c>.
    /// </summary>
    public string QueriesRoute { get; set; } = "/queries";

    /// <summary>
    /// Gets or sets the route used to execute a query against a queryable registration.
    /// Defaults to <c>/queries/{key}</c>, producing <c>POST /kaleido/queries/{key}</c>.
    /// </summary>
    public string QueryRoute { get; set; } = "/queries/{key}";
}
