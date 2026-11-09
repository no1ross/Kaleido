using Kaleido.Mcp.Observability;
using Kaleido.Mcp.Tools;
using Kaleido.Server.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Protocol;

namespace Kaleido.Mcp;

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
    /// central registry's hydrated copy, filtered per caller. Every host gets the complete
    /// registry through its registered central registry client.</description></item>
    /// <item><description><b>Calls</b> go through the Kaleido clients of each tool's service, made for the
    /// MCP caller: each carries the token the host's handler exchanged for that user
    /// (<c>Kaleido:OnBehalfOfCalls = Exchange</c>), never the agent's token and never the host's own
    /// credential, so the services authorize the real user (#574).</description></item>
    /// <item><description><b>Correlation</b> is the host's: the MCP request's
    /// <c>X-Kaleido-Request-Id</c> flows to every call and is returned in each tool result.</description></item>
    /// </list>
    /// </remarks>
    /// <exception cref="KaleidoConfigurationException">
    /// <c>AddHttp()</c> was not called first (<see cref="McpErrorCodes.MissingHttp"/>), no Kaleido
    /// client is configured (<see cref="McpErrorCodes.MissingClients"/>), <c>Kaleido:OnBehalfOfCalls</c> is not
    /// <c>Exchange</c> (<see cref="McpErrorCodes.OnBehalfOfCallsNotExchanged"/>), the instructions file
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

        // The translated catalogs belong to this registration: hosts in one process never share them (#464).
        services.AddSingleton<IToolCatalogCache, ToolCatalogCache>();
        services.AddSingleton<IInformationRequestElicitor, InformationRequestElicitor>();
        services.AddScoped<IMcpObservability, McpObservability>();
        services.AddScoped<IToolCatalogProvider, ToolCatalogProvider>();
        services.AddScoped<IKaleidoToolInvoker, KaleidoToolInvoker>();

        // The guided-lookup state the client carries between form rounds is encrypted and signed (#452).
        services.AddDataProtection();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IMcpStateProtector, McpStateProtector>();
        services.AddHostedService<McpKeyRingCheck>();
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

        if (!services.Any(x => x.ServiceType == typeof(IKaleidoRegistryClient)))
        {
            throw new KaleidoConfigurationException(
                ConfigurationErrorCodes.RegistryNotConfigured,
                "AddMcp() requires the central registry client. Configure Kaleido:BaseUrl and call AddControlRegistryClient() before AddMcp().");
        }

        if (services.FirstOrDefault(x => x.ImplementationInstance is KaleidoClientOptions)?.ImplementationInstance is not KaleidoClientOptions clients)
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.MissingClients,
                "AddMcp() calls services through the gateway client: configure Kaleido:BaseUrl and call AddHttpClients() before AddMcp().");
        }

        // Fail closed: tool calls are made for the agent's user, whose token was issued for this MCP server.
        // The MCP authorization specification forbids passing it through: the host exchanges it (#574).
        if (clients.OnBehalfOfCalls != KaleidoOnBehalfOfCalls.Exchange)
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.OnBehalfOfCallsNotExchanged,
                "AddMcp() makes tool calls for the agent's user, whose token is issued for this MCP server and must not be passed through: set Kaleido:OnBehalfOfCalls to Exchange, and add the handler that exchanges the user's token with AddKaleidoClientHandler<THandler>().");
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
