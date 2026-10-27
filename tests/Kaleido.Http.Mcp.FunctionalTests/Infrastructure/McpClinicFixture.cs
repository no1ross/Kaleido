using System.Diagnostics;
using Kaleido.Http.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
    private readonly bool withConfirmation;
    private readonly bool withLookupStep;
    private readonly string? keyRing;
    private readonly string applicationName;

    private IHost? host;

    /// <summary>The Authorization header of every request the clinic service received (path, header).</summary>
    public System.Collections.Concurrent.ConcurrentQueue<(string Path, string Authorization)> ServiceRequests { get; } = new();

    /// <summary>The X-Kaleido-Process-Id header of every request the clinic service received (path, header).</summary>
    public System.Collections.Concurrent.ConcurrentQueue<(string Path, string ProcessId)> ServiceProcessIds { get; } = new();

    /// <summary>The origin headers (X-Kaleido-Origin, X-Kaleido-Origin-Tool) of every request the clinic service received.</summary>
    public System.Collections.Concurrent.ConcurrentQueue<(string Path, string Authorization, string Origin, string OriginTool)> ServiceOrigins { get; } = new();

    /// <summary>Client timeout of the host's Kaleido clients (short, so a slow downstream test ends quickly).</summary>
    public static readonly TimeSpan ClientTimeout = TimeSpan.FromSeconds(3);

    /// <summary>Clinic service requests carrying one of these request ids are held for the given time (a slow downstream).</summary>
    public System.Collections.Concurrent.ConcurrentDictionary<string, TimeSpan> SlowRequests { get; } = new(StringComparer.Ordinal);

    /// <summary>Request ids of held clinic service requests that were aborted before they finished.</summary>
    public System.Collections.Concurrent.ConcurrentQueue<string> AbortedServiceRequests { get; } = new();

    /// <summary>Every log entry of the host: level, formatted message and exception text.</summary>
    public System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Text)> Logs { get; } = new();

    /// <summary>Every tag value of the host's Kaleido spans (sources named Kaleido*), with the span's request id.</summary>
    public System.Collections.Concurrent.ConcurrentQueue<(string? RequestId, string Key, string? Value)> SpanTags { get; } = new();

    private ActivityListener? spanListener;

    public McpClinicFixture()
        : this(withDownstreamThatIsDown: false)
    {
    }

    protected McpClinicFixture(bool withDownstreamThatIsDown, bool withSignIn = false, bool withFrontDoor = false, bool withConfirmation = false, bool withLookupStep = false, string? keyRing = null, string applicationName = "clinic")
    {
        this.withDownstreamThatIsDown = withDownstreamThatIsDown;
        this.withSignIn = withSignIn;
        this.withFrontDoor = withFrontDoor;
        this.withConfirmation = withConfirmation;
        this.withLookupStep = withLookupStep;
        this.keyRing = keyRing;
        this.applicationName = applicationName;
    }

    /// <summary>A raw HTTP client on the TestServer, for protocol-level assertions.</summary>
    public HttpClient CreateHttpClient() => Server.CreateClient();

    private TestServer Server =>
        host?.GetTestServer() ?? throw new InvalidOperationException("The host has not started.");

    public async ValueTask InitializeAsync()
    {
        spanListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name.StartsWith("Kaleido", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                var requestId = activity.GetTagItem(KaleidoTelemetryTags.RequestId) as string;

                foreach (var tag in activity.TagObjects)
                {
                    SpanTags.Enqueue((requestId, tag.Key, tag.Value?.ToString()));
                }
            },
        };
        ActivitySource.AddActivityListener(spanListener);

        host =
            await new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();

                    web.ConfigureServices(services =>
                    {
                        services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Debug).AddProvider(new CapturingLoggerProvider(Logs)));
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
                            ["Kaleido:Mcp:AdditionalInstructions"] = "A visit starts with a patient search.",
                            ["Kaleido:Resilience:TotalTimeout"] = ClientTimeout.ToString("c", System.Globalization.CultureInfo.InvariantCulture),
                        };

                        if (withConfirmation)
                        {
                            settings["Kaleido:Mcp:RequireConfirmation"] = "true";
                        }

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

                        // An instance of a multi-instance deployment: its Data Protection keys in a folder
                        // it may share with other instances (docs/guide/deployment.md, #452).
                        if (keyRing is not null)
                        {
                            services.AddDataProtection()
                                .SetApplicationName(applicationName)
                                .PersistKeysToFileSystem(new DirectoryInfo(keyRing));
                        }

                        services.AddKaleido(configuration, new()
                        {
                            ServiceName = "clinic",
                            DisplayName = "Clinic",
                            Assemblies = [typeof(McpClinicFixture).Assembly],

                            // The guided-lookup step only where its tests run: elsewhere it would change the
                            // tool list and what is next after a visit closes.
                            TypeFilter = withLookupStep ? null : type => type != typeof(AssignBedStep) && type != typeof(AssignBedHandler),
                            AuthorizationMode = KaleidoAuthorizationMode.Authenticated,
                        })
                            .AddInMemoryStores()
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
                                ServiceOrigins.Enqueue((context.Request.Path, context.Request.Headers.Authorization.ToString(), context.Request.Headers[KaleidoCorrelationHeaders.Origin].ToString(), context.Request.Headers[KaleidoCorrelationHeaders.OriginTool].ToString()));

                                var requestId = context.Request.Headers[KaleidoCorrelationHeaders.RequestId].ToString();

                                if (SlowRequests.TryGetValue(requestId, out var hold))
                                {
                                    try
                                    {
                                        await Task.Delay(hold, context.RequestAborted);
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        AbortedServiceRequests.Enqueue(requestId);
                                        return;
                                    }
                                }
                            }

                            await next(context);
                        });
                        app.UseRouting();
                        app.UseAuthentication();
                        app.UseAuthorization();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapKaleidoHttp(new() { AggregateRegistry = withDownstreamThatIsDown });
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
    /// <paramref name="elicitation"/> handler to act as a client whose user answers forms, and an
    /// <paramref name="outgoing"/> handler to see or change what the client sends.
    /// </summary>
    public async Task<McpClient> ConnectAsync(
        string? authorization,
        Func<ElicitRequestParams?, CancellationToken, ValueTask<ElicitResult>>? elicitation = null,
        string? requestId = null,
        string? protocolVersion = null,
        DelegatingHandler? outgoing = null)
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
            outgoing is null
                ? Server.CreateClient()
                : new HttpClient(Wrap(outgoing)) { BaseAddress = Server.BaseAddress });

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

    private DelegatingHandler Wrap(DelegatingHandler outgoing)
    {
        outgoing.InnerHandler = Server.CreateHandler();
        return outgoing;
    }

    private sealed class CapturingLoggerProvider(System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Text)> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(logs);

        public void Dispose()
        {
        }
    }

    private sealed class CapturingLogger(System.Collections.Concurrent.ConcurrentQueue<(LogLevel Level, string Text)> logs) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            logs.Enqueue((logLevel, formatter(state, exception) + (exception is null ? string.Empty : " " + exception)));
    }

    private sealed class UnreachableHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Connection refused (radiology.test).");
    }

    public async ValueTask DisposeAsync()
    {
        spanListener?.Dispose();

        if (host is not null)
        {
            await host.StopAsync();
            host.Dispose();
        }

        GC.SuppressFinalize(this);
    }
}
