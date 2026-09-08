namespace Kaleido.Registry;

/// <summary>
/// Configures the HTTP endpoint surface exposed by Kaleido.Registry.
/// </summary>
public sealed class RegistryRouteOptions
{
    /// <summary>
    /// The route prefix segment used to build the registry endpoint URL.
    /// Defaults to <c>"kaleido"</c>, producing <c>/kaleido/registry</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "kaleido";

    internal string RegistryRoute =>
        string.IsNullOrWhiteSpace(RoutePrefix)
            ? "/registry"
            : $"/{RoutePrefix.Trim().Trim('/')}/registry";
}
