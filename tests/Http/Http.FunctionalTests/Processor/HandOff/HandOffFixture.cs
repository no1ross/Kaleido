using Kaleido.Http.Client;
using Kaleido.Http.FunctionalTests.Authorization;
using Kaleido.Http.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Processor.HandOff;

// Two Kaleido services on their own TestServers (#198): "orders" hands a process off to "returns"
// through its Kaleido client for "returns", as services behind a router do. The origin authenticates
// its callers (TestAuthHandler), so it checks the target's steps for them. The target has no
// authentication, so it trusts the origin's correlation headers - the calling processor among them,
// which is what makes the call a hand-off.
public sealed class HandOffFixture : IAsyncLifetime
{
    public const string OriginService = "orders";
    public const string TargetService = "returns";

    private IHost? _target;
    private IHost? _origin;

    public HttpClient Origin { get; private set; } = null!;
    public HttpClient Target { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _target = await StartAsync(TargetService, typeof(Target.OpenReturnStep).Namespace!, KaleidoAuthorizationMode.None, targetServer: null);
        _origin = await StartAsync(OriginService, typeof(Origin.ReturnOrderStep).Namespace!, KaleidoAuthorizationMode.Authenticated, _target.GetTestServer());

        Origin = _origin.GetTestClient();
        Target = _target.GetTestClient();
    }

    private static Task<IHost> StartAsync(string serviceName, string modelNamespace, KaleidoAuthorizationMode authorization, TestServer? targetServer) =>
        new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddRouting();

                    var settings = new Dictionary<string, string?>(StringComparer.Ordinal);

                    if (targetServer is not null)
                    {
                        services.AddAuthentication(TestAuthHandler.SchemeName)
                            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                        services.AddAuthorization();
                        settings[$"Kaleido:Clients:{TargetService}:BaseUrl"] = "http://returns.test/";
                    }

                    services.AddKaleido(new ConfigurationBuilder().AddInMemoryCollection(settings).Build(), new()
                    {
                        ServiceName = serviceName,
                        AuthorizationMode = authorization,
                        Assemblies = [typeof(HandOffFixture).Assembly],
                        TypeFilter = type => string.Equals(type.Namespace, modelNamespace, StringComparison.Ordinal),
                    })
                        .AddInMemoryStores()
                        .AddHttp(new() { IncludeFrameworkMessages = true })
                        .AddHttpClients();

                    if (targetServer is not null)
                    {
                        services.AddHttpClient(TargetService).ConfigurePrimaryHttpMessageHandler(targetServer.CreateHandler);
                    }
                });
                web.Configure(app =>
                {
                    app.UseRouting();

                    if (targetServer is not null)
                    {
                        app.UseAuthentication();
                        app.UseAuthorization();
                    }

                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync();

    public async ValueTask DisposeAsync()
    {
        Origin.Dispose();
        Target.Dispose();

        foreach (var host in new[] { _origin, _target })
        {
            if (host is not null)
            {
                await host.StopAsync();
                host.Dispose();
            }
        }
    }
}
