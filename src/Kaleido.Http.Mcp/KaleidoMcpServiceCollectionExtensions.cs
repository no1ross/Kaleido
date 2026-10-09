using Kaleido.Http.Mcp.Http;
using Kaleido.Http.Mcp.Observability;
using Kaleido.Http.Mcp.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp;

public static class KaleidoMcpServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Kaleido MCP adapter: an MCP server whose tools are generated, per caller, from
    /// a Kaleido registry. Bind <c>Kaleido:Mcp</c> (<c>BaseUrl</c>, <c>RegistryPath</c>, optional
    /// <c>Instructions</c>/<c>InstructionsFile</c>/<c>AdditionalInstructions</c>,
    /// <c>RegistryCacheDuration</c>), then map the endpoint with <c>MapKaleidoMcp()</c>.
    /// </summary>
    /// <remarks>
    /// The adapter talks to Kaleido over HTTP only and forwards the MCP caller's
    /// <c>Authorization</c> header, so it can run in the same host as a router or on its own
    /// server. It needs no <c>AddKaleido()</c>.
    /// </remarks>
    public static IServiceCollection AddKaleidoMcp(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KaleidoMcpOptionsBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var builder = new KaleidoMcpOptionsBuilder();
        configuration.GetSection(KaleidoMcpOptions.SectionName).Bind(builder);
        configure?.Invoke(builder);

        var options = builder.Build();

        services.AddSingleton(options);
        services.AddHttpContextAccessor();
        services.TryAddSingleton(TimeProvider.System);

        services.AddSingleton<IMcpRequestIdAccessor, McpRequestIdAccessor>();
        services.AddSingleton<IMcpObservability, McpObservability>();
        services.AddTransient<KaleidoForwardingHandler>();
        services
            .AddHttpClient(KaleidoApiClient.HttpClientName, client => client.BaseAddress = options.BaseUrl)
            .AddHttpMessageHandler<KaleidoForwardingHandler>();

        services.AddSingleton<IToolNameBuilder, ToolNameBuilder>();
        services.AddSingleton<IToolSchemaBuilder, ToolSchemaBuilder>();
        services.AddSingleton<IToolCatalogBuilder, ToolCatalogBuilder>();
        services.AddSingleton<IInformationRequestElicitor, InformationRequestElicitor>();
        services.AddScoped<IKaleidoApiClient, KaleidoApiClient>();
        services.AddScoped<IToolCatalogProvider, ToolCatalogProvider>();
        services.AddScoped<IKaleidoToolInvoker, KaleidoToolInvoker>();
        services.AddScoped<IKaleidoMcpToolHandler, KaleidoMcpToolHandler>();

        services
            .AddMcpServer(server =>
            {
                server.ServerInfo = new Implementation
                {
                    Name = options.ServerName,
                    Version = options.ServerVersion
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

        return services;
    }

    // The SDK runs each request in its own scope; resolve the scoped handler from it.
    private static IKaleidoMcpToolHandler Handler(IServiceProvider? services) =>
        services?.GetRequiredService<IKaleidoMcpToolHandler>()
        ?? throw new KaleidoFrameworkException(
            FrameworkErrorCodes.MissingRegistration,
            "The MCP request has no service provider; AddKaleidoMcp() must register the server.");
}
