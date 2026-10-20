using Kaleido.Http.Client;
using Kaleido.Http.Registry;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.Mcp.FunctionalTests;

/// <summary>
/// A host with no capabilities of its own — it only aggregates its clients, like the PriorAuth
/// router — composes with the MCP adapter and starts. Development hosts validate the container on
/// build, so a missing dependency fails at startup; this host does the same.
/// </summary>
public sealed class RouterShapedHostTests
{
    [Fact]
    public async Task ARouterShapedHost_WithMcp_Builds_AndResolvesTheAdapter()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        });
        builder.Configuration["Kaleido:ServiceName"] = "router";
        builder.Configuration["Kaleido:Clients:intake:BaseUrl"] = "http://intake.test/";

        builder.Services.AddKaleido(builder.Configuration, o =>
            {
                o.Assemblies = [typeof(RouterShapedHostTests).Assembly];
                o.TypeFilter = _ => false;
                o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust;
            })
            .AddHttp()
            .AddHttpClients()
            .ForwardCallerToken()
            .AddMcp();

        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IKaleidoRegistryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IKaleidoCallCredentialProvider>());
    }
}
