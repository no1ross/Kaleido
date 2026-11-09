using Kaleido.Http.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Mcp.FunctionalTests;

/// <summary>
/// An MCP reader with no capabilities of its own participates in the central registry and
/// composes with the adapter. Development hosts validate the container on build, so a missing
/// dependency fails early; this host does the same.
/// </summary>
public sealed class RouterShapedHostTests
{
    [Fact]
    public async Task AMcpReaderHost_WithCentralRegistry_Builds_AndResolvesTheAdapter()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Host.UseDefaultServiceProvider(o =>
        {
            o.ValidateScopes = true;
            o.ValidateOnBuild = true;
        });
        builder.Configuration["Kaleido:ServiceName"] = "mcp-reader";
        builder.Configuration["Kaleido:BaseUrl"] = "http://registry.test/";
        builder.Configuration["Kaleido:OnBehalfOfCalls"] = "Exchange";

        builder.Services.AddKaleido(builder.Configuration, new()
        {
            Assemblies = [typeof(RouterShapedHostTests).Assembly],
            TypeFilter = _ => false,
            ServiceVersion = "1",
            AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust,
        })
            .AddInMemoryStores()
            .AddHttp()
            .AddHttpClients()
            .AddKaleidoClientHandler<Infrastructure.TestTokenExchangeHandler>()
            .AddControlRegistryClient()
            .AddMcp();

        await using var app = builder.Build();
        using var scope = app.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IKaleidoRegistryService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<Infrastructure.TestTokenExchangeHandler>());
    }
}
