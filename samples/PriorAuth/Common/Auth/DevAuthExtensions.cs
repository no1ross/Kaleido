using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Samples.PriorAuth.Auth;

/// <summary>Claim types issued by the sample's dev auth.</summary>
public static class DevAuthClaims
{
    /// <summary>
    /// The calling service on a service-to-service hop. Present only when a
    /// service makes the call (on behalf of a user or on its own); never on a
    /// direct user call.
    /// </summary>
    public const string Actor = "kaleido_actor";
}

/// <summary>Authorization policy names used by sample capabilities.</summary>
public static class DevAuthPolicies
{
    /// <summary>
    /// The call came from another service (actor claim present). Use for
    /// capabilities that must never be called directly by a consumer; combine
    /// with <c>Roles</c> to also require the user's role.
    /// </summary>
    public const string InternalCaller = "InternalCaller";

    public const string AuthenticatedUser = "AuthenticatedUser";
}

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

        // Registered on every host: leaves enforce it, and the router
        // evaluates it when filtering the aggregated registry per caller.
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                DevAuthPolicies.InternalCaller,
                policy => policy.RequireClaim(DevAuthClaims.Actor));
            options.AddPolicy(
                DevAuthPolicies.AuthenticatedUser,
                policy => policy.RequireAuthenticatedUser());
        });

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
        services.AddHttpContextAccessor();
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
                        "Dev persona token — get one from POST /auth/login on the router. " +
                        "The document lists only the operations the authorized caller may call: " +
                        "reload the page after authorizing."
                });

            // One document-wide requirement: Swagger UI only sends the
            // Authorization header for operations that declare a requirement,
            // so without it the Authorize token is stored but never sent.
            options.AddSecurityRequirement(document =>
                new Microsoft.OpenApi.OpenApiSecurityRequirement
                {
                    [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
                });

            // The per-operation requirement/401/403 noise Kaleido's
            // RequireAuthorization metadata produces is stripped; operations
            // inherit the document-wide requirement above.
            options.OperationFilter<DevSwaggerOperationFilter>();
            options.DocumentFilter<DevSwaggerDocumentFilter>();

            // Per caller: drop the operations the caller may not call, judged by
            // the same endpoint authorization metadata the server enforces.
            options.OperationAsyncFilter<DevSwaggerCallerOperationFilter>();
            options.DocumentFilter<DevSwaggerCallerDocumentFilter>();
        });

        return services;
    }

    /// <summary>
    /// Swagger UI for a dev-auth host: persists the Authorize token and sends it
    /// on every request, including the spec request itself, so the
    /// per-caller document (<see cref="AddDevSwagger"/>) matches the
    /// authorized persona. Reload the page after authorizing.
    /// </summary>
    public static IApplicationBuilder UseDevSwaggerUI(
        this IApplicationBuilder app,
        Action<Swashbuckle.AspNetCore.SwaggerUI.SwaggerUIOptions>? configure = null) =>
        app.UseSwaggerUI(options =>
        {
            configure?.Invoke(options);

            options.EnablePersistAuthorization();

            // Swagger UI stores persisted authorizations in localStorage under
            // "authorized"; attach the Bearer value when a request has none
            // (the spec fetch never carries it on its own). Keep the function
            // on ONE line: Swashbuckle embeds it as JSON inside a JS string in
            // index.js, where line breaks become raw newlines and JSON.parse
            // fails, leaving a blank page.
            options.UseRequestInterceptor(
                "(request) => { try { var authorized = JSON.parse(window.localStorage.getItem('authorized') || 'null'); var token = authorized && authorized.Bearer && authorized.Bearer.value; if (token && !request.headers.Authorization) { request.headers.Authorization = 'Bearer ' + token; } } catch (e) { } return request; }");
        });

    /// <summary>
    /// Registers <see cref="DevTokenForwardingHandler"/> for the given named
    /// Kaleido clients so outbound calls carry an on-behalf-of token (the
    /// user plus this service as actor) or a service token.
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
        // Null (not an empty list): an empty `security: []` on an operation
        // means "no auth" and would stop Swagger UI sending the token.
        operation.Security = null;
        operation.Responses?.Remove("401");
        operation.Responses?.Remove("403");
    }
}

/// <summary>
/// Marks the operations the current caller may not call, evaluating the
/// endpoint's own authorization metadata (<c>[AllowAnonymous]</c>,
/// <c>RequireAuthorization</c> policies, roles) against the caller with
/// ASP.NET's <see cref="Microsoft.AspNetCore.Authorization.IAuthorizationService"/>.
/// Hiding is a view, not a boundary: the server still enforces every call.
/// </summary>
internal sealed class DevSwaggerCallerOperationFilter(
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor,
    Microsoft.AspNetCore.Authorization.IAuthorizationPolicyProvider policyProvider)
    : Swashbuckle.AspNetCore.SwaggerGen.IOperationAsyncFilter
{
    internal const string HiddenOperationsKey = "DevSwagger.HiddenOperations";

    public async Task ApplyAsync(
        Microsoft.OpenApi.OpenApiOperation operation,
        Swashbuckle.AspNetCore.SwaggerGen.OperationFilterContext context,
        CancellationToken cancellationToken)
    {
        if (httpContextAccessor.HttpContext is not { } httpContext)
        {
            return;
        }

        var metadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        if (metadata.OfType<Microsoft.AspNetCore.Authorization.IAllowAnonymous>().Any())
        {
            return;
        }

        var policy =
            await Microsoft.AspNetCore.Authorization.AuthorizationPolicy.CombineAsync(
                policyProvider,
                metadata.OfType<Microsoft.AspNetCore.Authorization.IAuthorizeData>(),
                metadata.OfType<Microsoft.AspNetCore.Authorization.AuthorizationPolicy>());

        if (policy is null)
        {
            return;
        }

        var authorizationService =
            httpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();

        var result = await authorizationService.AuthorizeAsync(httpContext.User, policy);

        if (!result.Succeeded)
        {
            Hidden(httpContext).Add(operation);
        }
    }

    internal static HashSet<Microsoft.OpenApi.OpenApiOperation> Hidden(
        Microsoft.AspNetCore.Http.HttpContext httpContext)
    {
        if (httpContext.Items[HiddenOperationsKey] is not HashSet<Microsoft.OpenApi.OpenApiOperation> hidden)
        {
            hidden = [];
            httpContext.Items[HiddenOperationsKey] = hidden;
        }

        return hidden;
    }
}

/// <summary>
/// Removes the operations <see cref="DevSwaggerCallerOperationFilter"/> marked,
/// then any path or tag left without operations.
/// </summary>
internal sealed class DevSwaggerCallerDocumentFilter(
    Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
    : Swashbuckle.AspNetCore.SwaggerGen.IDocumentFilter
{
    public void Apply(
        Microsoft.OpenApi.OpenApiDocument document,
        Swashbuckle.AspNetCore.SwaggerGen.DocumentFilterContext context)
    {
        if (httpContextAccessor.HttpContext is not { } httpContext ||
            document.Paths is null)
        {
            return;
        }

        var hidden = DevSwaggerCallerOperationFilter.Hidden(httpContext);

        foreach (var (path, item) in document.Paths.ToArray())
        {
            if (item.Operations is not { } operations)
            {
                continue;
            }

            foreach (var (method, operation) in operations.ToArray())
            {
                if (hidden.Contains(operation))
                {
                    operations.Remove(method);
                }
            }

            if (operations.Count == 0)
            {
                document.Paths.Remove(path);
            }
        }

        if (document.Tags is null)
        {
            return;
        }

        var usedTags =
            document.Paths.Values
                .SelectMany(item => item.Operations?.Values ?? Enumerable.Empty<Microsoft.OpenApi.OpenApiOperation>())
                .SelectMany(operation => operation.Tags ?? Enumerable.Empty<Microsoft.OpenApi.OpenApiTagReference>())
                .Select(tag => tag.Name)
                .ToHashSet(StringComparer.Ordinal);

        foreach (var tag in document.Tags.ToArray())
        {
            if (!usedTags.Contains(tag.Name))
            {
                document.Tags.Remove(tag);
            }
        }
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
