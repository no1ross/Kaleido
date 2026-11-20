using Kaleido.Eventing;
using Kaleido.Guide.Examples.Callers;
using Kaleido.Http.Client.Calls;
using Kaleido.Http.Client.Registration;
using Kaleido.Hosting.Http;
using Kaleido.Http.Server;
using Kaleido.Mcp;
using Kaleido.Process;
using Kaleido.Process.Http.Client;
using Kaleido.Process.Http.Server;
using Kaleido.Provider.InMemory;
using Kaleido.Queryable;
using Kaleido.Queryable.Http.Client;
using Kaleido.Registry.Http.Client;
using Kaleido.Server;
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
        })
            .AddEventing()
            .AddProcess()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None }) // local evaluation only
            .AddInMemoryProviders()   // local evaluation only: nothing survives a restart
            .AddHttp()
            .AddProcessHttpServer()
            .AddRegistryHttpParticipation();
        // end snippet
    }

    public static void MapOrders(IEndpointRouteBuilder endpoints)
    {
        // snippet: getting-started-map
        endpoints.MapKaleido().MapProcess().MapQueryable();
        // end snippet
    }

    // A reader-only MCP host publishes the orders service's tools from the central Registry
    // and calls its published route through the single gateway client (Kaleido:BaseUrl).
    public static void AddOrdersWithMcp(IServiceCollection services, IConfiguration configuration)
    {
        // snippet: getting-started-mcp
        services.AddAuthentication("Bearer").AddJwtBearer(options =>
        {
            options.Authority = configuration["Authentication:Authority"];
            options.Audience = configuration["Authentication:Audience"];
        });
        services.AddAuthorization();

        services.AddKaleido(configuration, new()
        {
            ServiceName = "mcp-reader",
            ServiceVersion = "1",
            Assemblies = [typeof(OrderSource).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddKaleidoClientHandler<TokenExchangeHandler>()
            .AddRegistryHttpParticipation(new() { OnBehalfOfCalls = KaleidoOnBehalfOfCalls.Exchange })
            .AddProcessHttpClient()
            .AddQueryableHttpClient()
            .AddMcp();
        // end snippet
    }

    public static void MapOrdersWithMcp(WebApplication app)
    {
        // snippet: getting-started-mcp-map
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapKaleidoMcp("/mcp").RequireAuthorization();
        // end snippet
    }
}
