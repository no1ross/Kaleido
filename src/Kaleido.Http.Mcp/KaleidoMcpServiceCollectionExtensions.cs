using Kaleido.Http.Client;
using Kaleido.Http.Mcp.Observability;
using Kaleido.Http.Observability;
using Kaleido.Http.Mcp.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp;

/// <summary>
/// Registers the Kaleido MCP adapter (the MCP server, its options, tool catalog and tool invoker)
/// on an <see cref="IKaleidoBuilder"/>; the entry point is <see cref="AddMcp"/>.
/// </summary>
public static class KaleidoMcpServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Kaleido MCP adapter to a Kaleido host: an MCP server whose tools are the host's
    /// registry as each caller may see it, called through the host's Kaleido clients as that
    /// caller. Map the endpoint with <c>MapKaleidoMcp()</c>.
    /// </summary>
    /// <param name="builder">The Kaleido builder, after <c>AddHttp()</c> and <c>AddHttpClients()</c>.</param>
    /// <param name="options">
    /// The settings set in code (server name and version, instructions, elicitation rounds, tool
    /// listing); the <c>Kaleido:Mcp</c> configuration section is laid over a copy of them
    /// (configuration overrides code, setting by setting; ADR 0030). <see langword="null"/> uses the
    /// defaults.
    /// </param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// <para>The adapter reuses the HTTP stack instead of reimplementing it:</para>
    /// <list type="bullet">
    /// <item><description><b>Tools</b> come from <see cref="IKaleidoRegistryService"/> — the same
    /// snapshot and per-caller filtering as <c>GET /{service}/registry</c>. Map the registry with
    /// aggregation (<c>MapKaleidoHttp(new() { AggregateRegistry = true })</c>) to publish the
    /// downstream services' capabilities.</description></item>
    /// <item><description><b>Calls</b> go through the Kaleido clients of each tool's service, inside
    /// a caller-forwarding scope: services see the MCP caller's own token and authorize the real
    /// user, never the host's service credentials.</description></item>
    /// <item><description><b>Correlation</b> is the host's: the MCP request's
    /// <c>X-Kaleido-Request-Id</c> flows to every call and is returned in each tool result.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="KaleidoConfigurationException">
    /// <c>AddHttp()</c> was not called first (<see cref="McpErrorCodes.MissingHttp"/>), no Kaleido
    /// client is configured (<see cref="McpErrorCodes.MissingClients"/>), the instructions file
    /// is missing (<see cref="McpErrorCodes.MissingInstructionsFile"/>), or a setting is out of
    /// range (<see cref="McpErrorCodes.InvalidOptions"/>).
    /// </exception>
    public static IKaleidoBuilder AddMcp(
        this IKaleidoBuilder builder,
        KaleidoMcpOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        RequirePrerequisites(services);

        options = builder.Configuration
            .GetSection(KaleidoMcpOptions.SectionName)
            .BindOver(options ?? new KaleidoMcpOptions(), McpErrorCodes.InvalidOptions);
        KaleidoMcpOptions.Validate(options);
        var instructions = KaleidoMcpServerInstructions.Resolve(options);
        var version = KaleidoMcpServerVersion.Resolve(options, System.Reflection.Assembly.GetEntryAssembly());

        services.AddSingleton(options);
        services.AddSingleton(instructions);
        services.AddSingleton(version);

        services.AddSingleton<IToolNameBuilder, ToolNameBuilder>();
        services.AddSingleton<IToolSchemaBuilder, ToolSchemaBuilder>();
        services.AddSingleton<IToolCatalogBuilder, ToolCatalogBuilder>();
        services.AddSingleton<IInformationRequestElicitor, InformationRequestElicitor>();
        services.AddScoped<IMcpObservability, McpObservability>();
        services.AddScoped<IToolCatalogProvider, ToolCatalogProvider>();
        services.AddScoped<IKaleidoToolInvoker, KaleidoToolInvoker>();

        // The guided-lookup state the client carries between form rounds is encrypted and signed (#452).
        services.AddDataProtection();
        services.AddScoped<IMcpLookupStateProtector, McpLookupStateProtector>();
        services.AddScoped<IKaleidoMcpToolHandler, KaleidoMcpToolHandler>();

        services
            .AddMcpServer(server =>
            {
                server.ServerInfo = new Implementation
                {
                    Name = options.ServerName,
                    Version = version.Value,
                };
                server.ServerInstructions = instructions.Text;
            })
            // Stateless (the SDK default for the 2026-07-28 revision): no session affinity, so the
            // adapter can be load-balanced. Questions use MRTR rather than server-to-client calls.
            .WithHttpTransport()
            .WithListToolsHandler((request, cancellationToken) =>
                Handler(request.Services).ListToolsAsync(request, cancellationToken))
            .WithCallToolHandler((request, cancellationToken) =>
                Handler(request.Services).CallToolAsync(request, cancellationToken));

        return builder;
    }

    // AddHttp() (the registry), the Kaleido clients, and how tool calls are authorized must come first.
    private static void RequirePrerequisites(IServiceCollection services)
    {
        if (!services.Any(x => x.ServiceType == typeof(IKaleidoRegistryService)))
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.MissingHttp,
                "AddMcp() needs the Kaleido HTTP stack: call AddHttp() before AddMcp().");
        }

        if (!services.Any(x => x.ImplementationInstance is KaleidoClientOptions { Clients.Count: > 0 }))
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.MissingClients,
                "AddMcp() calls services through the Kaleido clients: configure Kaleido:Clients and call AddHttpClients() before AddMcp().");
        }

        // Fail closed: tool calls are made for the agent's user, and the host must state how they are
        // authorized - the caller's token unchanged, or exchanged per service (ADR 0022).
        if (!services.Any(x => x.ServiceType == typeof(IKaleidoCallCredentialProvider)))
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.MissingCallCredentials,
                "AddMcp() makes tool calls for the agent's user: register how they are authorized before AddMcp() - ForwardCallerToken() to send the user's token unchanged (every service accepts it), or AddCallCredentials<TProvider>() to exchange it per service.");
        }
    }

    // Resolve the handler from the HTTP request's own scope (not the SDK's per-message scope), and set
    // up that request's correlation first: the MCP endpoint is the SDK's, not a Kaleido route group, so
    // no Kaleido endpoint filter runs (#295). The request id the agent sent is then the one every call
    // carries and every result returns.
    private static IKaleidoMcpToolHandler Handler(IServiceProvider? services)
    {
        var httpContext = services?.GetService<IHttpContextAccessor>()?.HttpContext;
        httpContext?.InitializeKaleidoCorrelation();
        httpContext?.InitializeKaleidoCaller();

        var provider =
            httpContext?.RequestServices
            ?? services
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                "The MCP request has no service provider; AddMcp() must register the server.");

        return provider.GetRequiredService<IKaleidoMcpToolHandler>();
    }
}
