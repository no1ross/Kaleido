using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Kaleido.Hosting.Http;

/// <summary>
/// The host's explicit Kaleido HTTP route selections. Conventions applied here reach only the
/// selected capability or central Registry endpoint groups, not unrelated host routes.
/// </summary>
public interface IKaleidoHttpEndpointBuilder : IEndpointConventionBuilder
{
    /// <summary>The root route builder shared by the explicit endpoint-mapping extensions.</summary>
    IEndpointRouteBuilder Endpoints { get; }
}

internal sealed class KaleidoHttpEndpointBuilder(RouteGroupBuilder root) : IKaleidoHttpEndpointBuilder
{
    private readonly HashSet<string> _mapped = new(StringComparer.Ordinal);

    public IEndpointRouteBuilder Endpoints => root;

    internal bool LocalAuthorizationChecked { get; set; }

    internal void RequireAvailable(string capability)
    {
        if (_mapped.Contains(capability))
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.DuplicateRegistration,
                $"{capability} HTTP endpoints were mapped twice.");
        }

        if (_mapped.Contains("CentralRegistry") || (string.Equals(capability, "CentralRegistry", StringComparison.Ordinal) && _mapped.Count > 0))
        {
            throw new KaleidoConfigurationException(ConfigurationErrorCodes.InvalidOptions,
                "The central Registry authority and local service capabilities must use separate HTTP hosts.");
        }
    }

    internal void MarkMapped(string capability) => _mapped.Add(capability);

    public void Add(Action<EndpointBuilder> convention)
    {
        ArgumentNullException.ThrowIfNull(convention);
        ((IEndpointConventionBuilder)root).Add(convention);
    }
}
