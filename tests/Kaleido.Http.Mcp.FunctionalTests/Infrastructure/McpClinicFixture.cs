using Kaleido.Http.Mcp.Http;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.FunctionalTests.Infrastructure;

/// <summary>
/// One TestServer hosting the "clinic" Kaleido service and the MCP adapter pointed back at it
/// (base URL = the server itself), the same shape as the PriorAuth router. MCP clients are the
/// SDK's own <see cref="McpClient"/>, so the protocol is exercised end to end.
/// </summary>
public sealed class McpClinicFixture : IAsyncLifetime
{
    private IHost? host;

    private TestServer Server =>
        host?.GetTestServer() ?? throw new InvalidOperationException("The host has not started.");

    public async ValueTask InitializeAsync()
    {
        host =
            await new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();

                    web.ConfigureServices(services =>
                    {
                        services.AddRouting();
                        services.AddAuthentication(BearerTestAuthHandler.SchemeName)
                            .AddScheme<AuthenticationSchemeOptions, BearerTestAuthHandler>(BearerTestAuthHandler.SchemeName, _ => { });
                        services.AddAuthorization();

                        var configuration = new ConfigurationBuilder()
                            .AddInMemoryCollection(new Dictionary<string, string?>
                            {
                                ["Kaleido:Mcp:BaseUrl"] = "http://localhost/",
                                ["Kaleido:Mcp:RegistryPath"] = "/clinic/registry",
                                ["Kaleido:Mcp:RegistryCacheDuration"] = "00:00:00",
                                ["Kaleido:Mcp:AdditionalInstructions"] = "A visit starts with a patient search."
                            })
                            .Build();

                        services.AddKaleido(configuration, o =>
                            {
                                o.ServiceName = "clinic";
                                o.DisplayName = "Clinic";
                                o.Assemblies = [typeof(McpClinicFixture).Assembly];
                                o.AuthorizationMode = KaleidoAuthorizationMode.Authenticated;
                            })
                            .AddHttp();

                        services.AddKaleidoMcp(configuration);

                        // The adapter's calls go to this TestServer (in production: the network).
                        services.AddHttpClient(KaleidoApiClient.HttpClientName)
                            .ConfigurePrimaryHttpMessageHandler(sp =>
                                ((TestServer)sp.GetRequiredService<IServer>()).CreateHandler());
                    });

                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapKaleidoHttp();
                            endpoints.MapKaleidoMcp("/mcp");
                        });
                    });
                })
                .StartAsync();
    }

    /// <summary>
    /// An MCP client connected as <paramref name="authorization"/> (null = anonymous). Pass an
    /// <paramref name="elicitation"/> handler to act as a client whose user answers forms.
    /// </summary>
    public async Task<McpClient> ConnectAsync(
        string? authorization,
        Func<ElicitRequestParams?, CancellationToken, ValueTask<ElicitResult>>? elicitation = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);

        if (authorization is not null)
        {
            headers["Authorization"] = authorization;
        }

        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri("http://localhost/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = headers
            },
            Server.CreateClient());

        var options = new McpClientOptions();

        if (elicitation is not null)
        {
            // Like a real client that can show forms: advertise elicitation and handle it.
            options.Capabilities = new ClientCapabilities { Elicitation = new ElicitationCapability { Form = new() } };
            options.Handlers = new McpClientHandlers { ElicitationHandler = elicitation };
        }

        return await McpClient.CreateAsync(transport, options, cancellationToken: TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.StopAsync();
            host.Dispose();
        }
    }
}
