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
    /// Registers Swagger with a Bearer security definition for the dev-token
    /// scheme so Swagger UI exposes an Authorize button. Paste a token from
    /// the router's <c>POST /auth/login</c>.
    /// </summary>
    public static IServiceCollection AddDevSwagger(
        this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.AddSecurityDefinition(
                "Bearer",
                new Microsoft.OpenApi.OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = Microsoft.OpenApi.SecuritySchemeType.Http,
                    Scheme = "bearer",
                    In = Microsoft.OpenApi.ParameterLocation.Header,
                    Description =
                        "Dev persona token — get one from POST /auth/login on the router."
                });

            // Security definition stays (Authorize button works), but the
            // per-operation requirement/401/403 noise Kaleido's
            // RequireAuthorization metadata produces is stripped.
            options.OperationFilter<DevSwaggerOperationFilter>();
            options.DocumentFilter<DevSwaggerDocumentFilter>();
        });

        return services;
    }

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

/// <summary>
/// Removes the per-operation security requirement and 401/403 response
/// entries from every operation. Auth still applies at runtime — this is
/// documentation noise: the dev token authorizes globally, so repeating the
/// padlock and denial responses on every capability endpoint adds clutter.
/// </summary>
internal sealed class DevSwaggerOperationFilter : Swashbuckle.AspNetCore.SwaggerGen.IOperationFilter
{
    public void Apply(
        Microsoft.OpenApi.OpenApiOperation operation,
        Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context)
    {
        operation.Security?.Clear();
        operation.Responses?.Remove("401");
        operation.Responses?.Remove("403");
    }
}

/// <summary>
/// Moves Kaleido framework tag groups (Registry, Processes, Queryable) to the
/// end of the document so per-capability DisplayName groups surface first.
/// </summary>
internal sealed class DevSwaggerDocumentFilter : Swashbuckle.AspNetCore.SwaggerGen.IDocumentFilter
{
    private static readonly string[] FrameworkTags = ["Registry", "Processes", "Queryable"];

    public void Apply(
        Microsoft.OpenApi.OpenApiDocument document,
        Swashbuckle.AspNetCore.SwaggerGen.DocumentFilterContext context)
    {
        if (document.Tags is null)
        {
            return;
        }

        var ordered = document.Tags
            .OrderByDescending(
                tag => !FrameworkTags.Contains(tag.Name, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        document.Tags.Clear();

        foreach (var tag in ordered)
        {
            document.Tags.Add(tag);
        }
    }
}
