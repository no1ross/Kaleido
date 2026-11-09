using Kaleido.Http.Server;
using Kaleido.Platform;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Authorization;

/// <summary>
/// The authorization test capabilities (<c>AuthorizationTestModels.cs</c>) on a host that authenticates as a production service does (#574): ASP.NET's JWT bearer handler
/// with the test identity's tokens, roles in <c>roles</c>, run as Production, called over https.
/// </summary>
public sealed class JwtAuthorizationFixture
    : IAsyncLifetime
{
    private IHost? _host;
    private TestControlRegistry? _registry;

    public HttpClient Client { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        _registry = await TestControlRegistry.StartAsync();
        _host =
            await new HostBuilder()
                .UseEnvironment(Environments.Production)
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder.UseTestServer();

                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddTestJwtBearer();
                        services.AddAuthorization(options =>
                            options.AddPolicy("clinician-only", p => p.RequireRole("clinician")));
                        services.AddSingleton<IAuthorizationHandler, SuperuserRoleHandler>();

                        services.AddKaleido(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                        {
                            ["Kaleido:BaseUrl"] = "http://registry.test/",
                        }).Build(), new()
                        {
                            ServiceName = "kaleido",
                            ServiceVersion = "1",
                            DisplayName = "JWT Auth Test Processor",
                            Assemblies = [typeof(JwtAuthorizationFixture).Assembly],
                            AuthorizationMode = KaleidoAuthorizationMode.Authenticated,
                        })
                            .AddInMemoryStores()
                            .AddHttp()
                            .AddControlRegistryClient();
                        services.AddHttpClient("kaleido-registry").ConfigurePrimaryHttpMessageHandler(() => _registry!.CreateHandler());
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapKaleidoHttp();
                            endpoints.MapGet("/test/registry", async (IKaleidoRegistryService registry, CancellationToken ct) =>
                                Results.Json(await registry.GetForCallerAsync(refresh: false, ct), KaleidoJsonOptions.Options));
                        });
                    });
                })
                .StartAsync();

        await _registry.WaitForRegistrationAsync(_host);
        Client = _host.GetTestClient();
        Client.BaseAddress = new Uri("https://localhost/");
    }

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        if (_registry is not null)
        {
            await _registry.DisposeAsync();
        }
    }
}
