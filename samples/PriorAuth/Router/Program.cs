using Kaleido;
using Kaleido.Http;
using Kaleido.Http.Client;
using Kaleido.Http.Registry;
using Kaleido.Samples.PriorAuth.Auth;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

var authKey =
    builder.Configuration[DevTokenIssuer.AuthKeyConfigName]
    ?? DevTokenIssuer.DefaultKey;

// Dev personas — a real deployment would integrate an IdP.
var personas = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
{
    ["alice"] = ["intake"],
    ["bob"] = ["radiology"],
    ["carol"] = ["admin", "intake", "radiology"]
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

            // No inbound token → this is an unauthenticated browser call or a
            // service-to-service hop; stamp the router's internal service
            // token so downstream RequireAuthorization checks pass.
            if (!transformContext.ProxyRequest.Headers.Contains(HeaderNames.Authorization))
            {
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                    HeaderNames.Authorization,
                    $"Bearer {DevTokenIssuer.IssueServiceToken("router", authKey)}");
            }

            return ValueTask.CompletedTask;
        });
    });

builder.Services.AddKaleido(builder.Configuration)
    // AddHttp registers the HTTP service layer MapRegistry resolves
    // (IProcessResponseFactory, authorizer) + the Exception/
    // Observability middleware so errors render as Kaleido error JSON.
    .AddHttp()
    .AddHttpClients();

builder.Services.AddDevAuth();

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

app.MapRegistry();
app.MapReverseProxy();

app.Run();

public sealed record DevLoginRequest(string Name);

public sealed record DevLoginResponse(string Token);
