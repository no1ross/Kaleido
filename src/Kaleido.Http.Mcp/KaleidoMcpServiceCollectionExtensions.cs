using Kaleido.Http.Client;
using Kaleido.Http.Mcp.Observability;
using Kaleido.Http.Mcp.Tools;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp;

public static class KaleidoMcpServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Kaleido MCP adapter to a Kaleido host: an MCP server whose tools are the host's
    /// registry as each caller may see it, called through the host's Kaleido clients as that
    /// caller. Map the endpoint with <c>MapKaleidoMcp()</c>.
    /// </summary>
    /// <param name="builder">The Kaleido builder, after <c>AddHttp()</c> and <c>AddHttpClients()</c>.</param>
    /// <param name="configure">
    /// Adjusts the settings bound from <c>Kaleido:Mcp</c> (server name and version, instructions,
    /// elicitation rounds).
    /// </param>
    /// <returns>The builder, for chaining.</returns>
    /// <remarks>
    /// <para>The adapter reuses the HTTP stack instead of reimplementing it:</para>
    /// <list type="bullet">
    /// <item><description><b>Tools</b> come from <see cref="IKaleidoRegistryService"/> — the same
    /// snapshot and per-caller filtering as <c>GET /{service}/registry</c>. Map the registry with
    /// aggregation (<c>MapKaleidoHttp(o =&gt; o.AggregateRegistry = true)</c>) to publish the
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
        Action<KaleidoMcpOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var services = builder.Services;

        if (!services.Any(x => x.ServiceType == typeof(IKaleidoRegistryService)))
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.MissingHttp,
                "AddMcp() needs the Kaleido HTTP stack: call AddHttp() before AddMcp().");
        }

        if (!services.Any(x => x.ServiceType == typeof(IKaleidoCallerForwarding)))
        {
            throw new KaleidoConfigurationException(
                McpErrorCodes.MissingClients,
                "AddMcp() calls services through the Kaleido clients: configure Kaleido:Clients and call AddHttpClients() before AddMcp().");
        }

        var optionsBuilder = new KaleidoMcpOptionsBuilder();
        builder.Configuration.GetSection(KaleidoMcpOptions.SectionName).Bind(optionsBuilder);
        configure?.Invoke(optionsBuilder);

        var options = optionsBuilder.Build();

        services.AddSingleton(options);

        services.AddSingleton<IToolNameBuilder, ToolNameBuilder>();
        services.AddSingleton<IToolSchemaBuilder, ToolSchemaBuilder>();
        services.AddSingleton<IToolCatalogBuilder, ToolCatalogBuilder>();
        services.AddSingleton<IInformationRequestElicitor, InformationRequestElicitor>();
        services.AddScoped<IMcpObservability, McpObservability>();
        services.AddScoped<IToolCatalogProvider, ToolCatalogProvider>();
        services.AddScoped<IKaleidoToolInvoker, KaleidoToolInvoker>();
        services.AddScoped<IKaleidoMcpToolHandler, KaleidoMcpToolHandler>();

        services
            .AddMcpServer(server =>
            {
                server.ServerInfo = new Implementation
                {
                    Name = options.ServerName,
                    Version = options.ServerVersion,
                };
                server.ServerInstructions = options.Instructions;
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

    // Resolve the handler from the HTTP request's own scope (not the SDK's per-message scope), so
    // it shares the request's correlation context that Kaleido's middleware set — the request id
    // the agent sent is the one every call carries and every result returns.
    private static IKaleidoMcpToolHandler Handler(IServiceProvider? services)
    {
        var provider =
            services?.GetService<IHttpContextAccessor>()?.HttpContext?.RequestServices
            ?? services
            ?? throw new KaleidoFrameworkException(
                FrameworkErrorCodes.MissingRegistration,
                "The MCP request has no service provider; AddMcp() must register the server.");

        return provider.GetRequiredService<IKaleidoMcpToolHandler>();
    }
}
