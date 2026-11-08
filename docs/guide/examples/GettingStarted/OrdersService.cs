using Kaleido.Http.Client.Registration;
using Kaleido.Http.Client.Registry;
using Kaleido.Http.Server;
using Kaleido.Mcp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Guide.Examples.GettingStarted;

// The getting-started service's registration, as a host's Program.cs would make it.
public static class OrdersService
{
    public static void AddOrders(IServiceCollection services, IConfiguration configuration)
    {
        // snippet: getting-started-host
        services.AddSingleton<OrderBook>();

        services.AddKaleido(configuration, new()
        {
            ServiceName = "orders",
            ServiceVersion = "1",
            Assemblies = [typeof(OrderSource).Assembly],
            AuthorizationMode = KaleidoAuthorizationMode.None, // local evaluation only
        })
            .AddInMemoryStores()   // local evaluation only: nothing survives a restart
            .AddHttp()
            .AddControlRegistryClient();
        // end snippet
    }

    public static void MapOrders(IEndpointRouteBuilder endpoints)
    {
        // snippet: getting-started-map
        endpoints.MapKaleidoHttp();
        // end snippet
    }

    // The same service, also published to AI agents: the MCP adapter builds its tools from the
    // registry and calls the service through a Kaleido client (Kaleido:Clients:orders in configuration).
    public static void AddOrdersWithMcp(IServiceCollection services, IConfiguration configuration)
    {
        // snippet: getting-started-mcp
        services.AddSingleton<OrderBook>();

        services.AddKaleido(configuration, new()
        {
            ServiceName = "orders",
            ServiceVersion = "1",
            Assemblies = [typeof(OrderSource).Assembly],
            AuthorizationMode = KaleidoAuthorizationMode.None, // local evaluation only
        })
            .AddInMemoryStores()   // local evaluation only: nothing survives a restart
            .AddHttp()
            .AddHttpClients()   // Kaleido:OnBehalfOfCalls = Exchange: AddMcp() requires it
            .AddControlRegistryClient()
            .AddMcp();
        // end snippet
    }

    public static void MapOrdersWithMcp(WebApplication app)
    {
        // snippet: getting-started-mcp-map
        app.MapKaleidoHttp();
        app.MapKaleidoMcp("/mcp");
        // end snippet
    }
}
