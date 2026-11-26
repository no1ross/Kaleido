using Kaleido.Http.Server.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kaleido.Http.Server;

/// <summary>
/// Registers shared Kaleido HTTP routing, body handling, correlation and ASP.NET authorization
/// infrastructure on an <see cref="IKaleidoBuilder"/>; capability adapters and registry participation
/// are registered separately.
/// </summary>
public static class KaleidoHttpServerServiceCollectionExtensions
{
    /// <summary>
    /// Registers HTTP transport infrastructure for Kaleido: ASP.NET Core routing,
    /// <see cref="Microsoft.AspNetCore.Http.IHttpContextAccessor"/>, correlation and exception
    /// handling and scoped HTTP error responses.
    /// </summary>
    /// <remarks>
    /// The <c>Kaleido:Http</c> configuration section is laid over a copy of <paramref name="options"/>
    /// (configuration overrides code, setting by setting; ADR 0030), validated and registered as the
    /// <see cref="KaleidoHttpServerOptions"/> singleton.
    /// </remarks>
    /// <param name="builder">The Kaleido builder.</param>
    /// <param name="options">The options set in code; <see langword="null"/> uses the defaults.</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="KaleidoConfigurationException">
    /// <c>AddHttp</c> was already called (<c>duplicate_registration</c>), or a setting is invalid
    /// (<c>invalid_http_options</c>).
    /// </exception>
    public static IKaleidoBuilder AddHttp(
        this IKaleidoBuilder builder,
        KaleidoHttpServerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // A second call would silently replace the first one's options (#301).
        if (builder.Services.Any(x => x.ServiceType == typeof(KaleidoHttpServerOptions)))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.DuplicateRegistration,
                "AddHttp() was called more than once on this Kaleido builder. Configure the HTTP transport in a single AddHttp() call.");
        }

        // Generic ASP.NET Core infrastructure needed by HTTP transport
        builder.Services.AddRouting();
        builder.Services.AddHttpContextAccessor();

        // Transport options — always registered so map-time conventions and
        // runtime services can resolve it unconditionally.
        var httpOptions = builder.Configuration
            .GetSection(KaleidoHttpServerOptions.SectionName)
            .BindOver(options ?? new KaleidoHttpServerOptions(), ConfigurationErrorCodes.InvalidHttpOptions);
        builder.Services.AddSingleton(httpOptions);

        // Capability authorization is ASP.NET's (#574): the typed routes' policies, and the same policies
        // checked by IAuthorizationService on the dynamic paths. 401/403 responses are produced by the
        // host's authentication scheme (standard ASP.NET Core).
        builder.Services.AddAuthorization();
        builder.Services.TryAddScoped<IKaleidoAuthorizer, KaleidoAuthorizer>();
        builder.Services.TryAddSingleton<Kaleido.Http.Server.Middleware.IKaleidoExceptionHandler, Kaleido.Http.Server.Middleware.KaleidoExceptionHandler>();

        return builder;
    }
}
