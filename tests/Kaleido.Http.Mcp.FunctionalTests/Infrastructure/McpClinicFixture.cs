using Kaleido.Http.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore.Authentication;
using ModelContextProtocol.Authentication;
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

    // The identity provider and scope the sign-in mode publishes in its protected-resource metadata.
    public const string AuthorizationServer = "https://login.example.test/";
    public const string Scope = "kaleido.mcp";

    private readonly bool withDownstreamThatIsDown;
    private readonly bool withSignIn;
    private readonly bool withFrontDoor;

    private IHost? host;

    /// <summary>The Authorization header of every request the clinic service received (path, header).</summary>
    public System.Collections.Concurrent.ConcurrentQueue<(string Path, string Authorization)> ServiceRequests { get; } = new();

    /// <summary>The X-Kaleido-Process-Id header of every request the clinic service received (path, header).</summary>
    public System.Collections.Concurrent.ConcurrentQueue<(string Path, string ProcessId)> ServiceProcessIds { get; } = new();

    public McpClinicFixture()
        : this(withDownstreamThatIsDown: false)
    {
    }

    protected McpClinicFixture(bool withDownstreamThatIsDown, bool withSignIn = false, bool withFrontDoor = false)
    {
        this.withDownstreamThatIsDown = withDownstreamThatIsDown;
        this.withSignIn = withSignIn;
        this.withFrontDoor = withFrontDoor;
    }

    /// <summary>A raw HTTP client on the TestServer, for protocol-level assertions.</summary>
    public HttpClient CreateHttpClient() => Server.CreateClient();

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
                        var authentication = services.AddAuthentication(o =>
                            {
                                o.DefaultScheme = BearerTestAuthHandler.SchemeName;

                                // Sign-in discovery (#212): the host still validates tokens with its own
                                // scheme; the MCP SDK's scheme only answers the challenge with the
                                // protected-resource metadata, so MCP clients find the IdP themselves.
                                if (withSignIn)
                                {
                                    o.DefaultChallengeScheme = McpAuthenticationDefaults.AuthenticationScheme;
                                }
                            })
                            .AddScheme<AuthenticationSchemeOptions, BearerTestAuthHandler>(BearerTestAuthHandler.SchemeName, _ => { });

                        if (withSignIn)
                        {
                            authentication.AddMcp(o => o.ResourceMetadata = new ProtectedResourceMetadata
                            {
                                AuthorizationServers = [AuthorizationServer],
                                ScopesSupported = [Scope],
                            });
                        }
                        services.AddAuthorization();

                        var settings = new Dictionary<string, string?>
                        {
                            ["Kaleido:Clients:clinic:BaseUrl"] = "http://localhost/",
                            ["Kaleido:Clients:clinic:RoutePrefix"] = "clinic",
                            ["Kaleido:Mcp:AdditionalInstructions"] = "A visit starts with a patient search."
                        };

                        if (withFrontDoor)
                        {
                            settings["Kaleido:Mcp:ToolListing"] = "FrontDoor";
                            settings["Kaleido:Mcp:ListedTools:0"] = "clinic_patient";
                        }

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
                            // Tool calls send the agent's user's token unchanged (ADR 0022); the
                            // host's service credential below stays on the service's own calls.
                            .ForwardCallerToken()
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
                                ServiceProcessIds.Enqueue((context.Request.Path, context.Request.Headers[KaleidoCorrelationHeaders.ProcessId].ToString()));
                            }

                            await next(context);
                        });
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapKaleidoHttp(o => o.AggregateRegistry = withDownstreamThatIsDown);
                            var mcp = endpoints.MapKaleidoMcp("/mcp");

                            if (withSignIn)
                            {
                                mcp.RequireAuthorization();
                            }
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
        string? requestId = null,
        string? protocolVersion = null)
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

        // A pinned revision decides how forms travel: 2026-07-28 returns input_required results
        // (MRTR) that the SDK client answers and retries; 2025-11-25 elicits within the session.
        var options = new McpClientOptions { ProtocolVersion = protocolVersion };

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
