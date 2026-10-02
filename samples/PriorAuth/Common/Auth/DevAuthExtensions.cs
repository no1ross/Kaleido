using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Auth;

/// <summary>
/// Dev-auth wiring for the sample services. <see cref="AddDevAuth"/>
/// registers the dev-token scheme + authorization services;
/// <see cref="UseDevAuth"/> appends the authentication/authorization
/// middleware pair (call before <c>MapKaleido()</c>).
/// </summary>
public static class DevAuthExtensions
{
    public static IServiceCollection AddDevAuth(
        this IServiceCollection services)
    {
        services.AddAuthentication(DevTokenAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, DevTokenAuthenticationHandler>(
                DevTokenAuthenticationHandler.SchemeName, _ => { });
        services.AddAuthorization();

        return services;
    }

    public static IApplicationBuilder UseDevAuth(
        this IApplicationBuilder app) =>
        app
            .UseAuthentication()
            .UseAuthorization();

    /// <summary>
    /// Registers <see cref="DevTokenForwardingHandler"/> for the given named
    /// Kaleido clients so outbound calls carry the inbound user token or an
    /// <c>internal</c> service token.
    /// </summary>
    public static IServiceCollection AddDevTokenForwarding(
        this IServiceCollection services,
        params string[] clientNames)
    {
        services.AddTransient<DevTokenForwardingHandler>();

        foreach (var name in clientNames)
        {
            services.AddHttpClient(name)
                .AddHttpMessageHandler<DevTokenForwardingHandler>();
        }

        return services;
    }
}
