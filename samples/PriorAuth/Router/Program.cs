using Kaleido;
using Kaleido.Http;
using Kaleido.Http.Client;
using Kaleido.Samples.PriorAuth.Auth;
using Kaleido.Observability.OpenTelemetry;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;
using Kaleido.Samples.PriorAuth.Router;

var builder = WebApplication.CreateBuilder(args);

var authKey =
    builder.Configuration[DevTokenIssuer.AuthKeyConfigName]
    ?? DevTokenIssuer.DefaultKey;

// Dev personas — a real deployment would integrate an IdP. Roles describe
// the user only: Intake accepts any logged-in user (no role), Radiology
// requires "radiology". alice has no domain role, so Intake can't route her
// to Radiology.
var personas = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["alice"] = [],
    ["bob"] = ["radiology"],
    ["carol"] = ["admin", "radiology"]
};

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        context.AddRequestTransform(transformContext =>
        {
            if (!transformContext.ProxyRequest.Headers.Contains(KaleidoCorrelationHeaders.RequestId))
            {
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                    KaleidoCorrelationHeaders.RequestId,
                    Guid.NewGuid().ToString());
            }

            // The inbound Authorization header (if any) is forwarded as-is.
            // Anonymous requests stay anonymous — downstream services return
            // 401 for anything that isn't AllowAnonymous.
            return ValueTask.CompletedTask;
        });
    });

// ZeroTrust: the router filters the aggregate per caller with the leaf rules;
// undeclared capabilities are not exposed.
builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust;
        o.Assemblies = [typeof(Program).Assembly];
    })
    // AddHttp registers the HTTP service layer MapRegistry resolves
    // (IProcessorResponseFactory, authorizer) + the Exception/
    // Observability middleware so errors render as Kaleido error JSON.
    .AddHttp()
    .AddHttpClients()
    .AddOpenTelemetry();

// Registry aggregation is a service-to-service call: always stamp the
// router's service token (never the inbound user token) so the cached
// aggregate stays caller-agnostic — the router's own FilterForCaller
// scopes the result per user at the edge.
var routerServiceToken =
    $"Bearer {DevTokenIssuer.IssueServiceToken("router", authKey)}";

foreach (var clientName in builder.Configuration
             .GetSection("Kaleido:Clients")
             .GetChildren()
             .Select(c => c.Key))
{
    builder.Services.AddHttpClient(
        clientName,
        client => client.DefaultRequestHeaders.TryAddWithoutValidation(
            HeaderNames.Authorization, routerServiceToken));
}

builder.Services.AddDevAuth();
builder.Services.AddDevSwagger();

builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

// Dev login — exchanges a persona name for a signed dev token.
app.MapPost("/auth/login", (DevLoginRequest request) =>
    personas.TryGetValue(request.Name, out var roles)
        ? Results.Ok(
            new DevLoginResponse(
                DevTokenIssuer.Issue(request.Name, roles, authKey)))
        : Results.BadRequest(new { error = $"Unknown persona '{request.Name}'." }));

app.MapHealthChecks("/health");

app.UseDevAuth();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    // One Swagger UI for every service: the router's own spec plus each
    // service's spec, proxied by the ReverseProxy routes
    // /swagger/services/{name}/swagger.json. The services are the router's
    // Kaleido clients (the same list the aggregate registry uses). "Try it
    // out" calls the router, which forwards to the service with the caller's
    // token; authorize once and it persists across definitions.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "router");

        foreach (var name in builder.Configuration
                     .GetSection("Kaleido:Clients")
                     .GetChildren()
                     .Select(client => client.Key)
                     .Order(StringComparer.OrdinalIgnoreCase))
        {
            options.SwaggerEndpoint($"/swagger/services/{name}/swagger.json", name);
        }

        options.EnablePersistAuthorization();
    });
}

app.MapKaleidoHttp(o => o.AggregateRegistry = true);
app.MapReverseProxy();

app.Run();

namespace Kaleido.Samples.PriorAuth.Router
{
    public sealed record DevLoginRequest(string Name);

    public sealed record DevLoginResponse(string Token);
}
