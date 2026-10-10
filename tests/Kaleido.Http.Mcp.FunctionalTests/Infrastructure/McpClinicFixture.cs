using Kaleido.Http.Client;
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
/// One TestServer hosting the "clinic" Kaleido service with the MCP adapter on top: the tools come
/// from the host's registry, and tool calls go through a Kaleido client for "clinic" (pointing back
/// at this server) as the MCP caller. MCP clients are the SDK's own <see cref="McpClient"/>, so
/// the protocol is exercised end to end.
/// </summary>
public class McpClinicFixture : IAsyncLifetime
{
    // The host's own clients carry a service credential, as a router's do; tools must never use it.
    public const string HostServiceToken = "Bearer svc-host|doctor";

    private readonly bool withDownstreamThatIsDown;

    private IHost? host;

    /// <summary>The Authorization header of every request the clinic service received (path, header).</summary>
    public System.Collections.Concurrent.ConcurrentQueue<(string Path, string Authorization)> ServiceRequests { get; } = new();

    public McpClinicFixture()
        : this(withDownstreamThatIsDown: false)
    {
    }

    protected McpClinicFixture(bool withDownstreamThatIsDown)
    {
        this.withDownstreamThatIsDown = withDownstreamThatIsDown;
    }

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

                        var settings = new Dictionary<string, string?>
                        {
                            ["Kaleido:Clients:clinic:BaseUrl"] = "http://localhost/",
                            ["Kaleido:Clients:clinic:RoutePrefix"] = "clinic",
                            ["Kaleido:Mcp:AdditionalInstructions"] = "A visit starts with a patient search."
                        };

                        if (withDownstreamThatIsDown)
                        {
                            settings["Kaleido:Clients:radiology:BaseUrl"] = "http://radiology.test/";
                            settings["Kaleido:Clients:radiology:RoutePrefix"] = "radiology";
                        }

                        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

                        services.AddKaleido(configuration, o =>
                            {
                                o.ServiceName = "clinic";
                                o.DisplayName = "Clinic";
                                o.Assemblies = [typeof(McpClinicFixture).Assembly];
                                o.AuthorizationMode = KaleidoAuthorizationMode.Authenticated;
                            })
                            .AddHttp()
                            .AddHttpClients()
                            .AddMcp();

                        // The clinic client calls this TestServer (in production: the network),
                        // with the host's service credential on every call.
                        services.AddHttpClient("clinic", client => client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", HostServiceToken))
                            .ConfigurePrimaryHttpMessageHandler(sp =>
                                ((TestServer)sp.GetRequiredService<IServer>()).CreateHandler());

                        if (withDownstreamThatIsDown)
                        {
                            services.AddHttpClient("radiology")
                                .ConfigurePrimaryHttpMessageHandler(() => new UnreachableHandler());
                        }
                    });

                    web.Configure(app =>
                    {
                        app.Use(async (context, next) =>
                        {
                            if (context.Request.Path.StartsWithSegments("/clinic"))
                            {
                                ServiceRequests.Enqueue((context.Request.Path, context.Request.Headers.Authorization.ToString()));
                            }

                            await next(context);
                        });
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapKaleidoHttp(o => o.AggregateRegistry = withDownstreamThatIsDown);
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
        Func<ElicitRequestParams?, CancellationToken, ValueTask<ElicitResult>>? elicitation = null,
        string? requestId = null)
    {
        var headers = new Dictionary<string, string>(StringComparer.Ordinal);

        if (authorization is not null)
        {
            headers["Authorization"] = authorization;
        }

        if (requestId is not null)
        {
            headers[KaleidoCorrelationHeaders.RequestId] = requestId;
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

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused (radiology.test).");
    }

    public async ValueTask DisposeAsync()
    {
        if (host is not null)
        {
            await host.StopAsync();
            host.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
