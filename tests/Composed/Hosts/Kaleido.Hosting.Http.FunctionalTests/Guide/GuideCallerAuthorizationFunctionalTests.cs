using System.Net;
using System.Net.Http.Json;
using Kaleido.Guide.Examples.Callers;
using Kaleido.Guide.Examples.GettingStarted;
using Kaleido.Http.Client.Calls;
using Kaleido.Http.Client.Configuration;
using Kaleido.Http.Client.Registration;
using Kaleido.Process.Client;
using Kaleido.Queryable.Client;
using Kaleido.Queryable.Http;
using Kaleido.Registry.Http.Client;
using Kaleido.Testing.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;

namespace Kaleido.Hosting.Http.FunctionalTests.Guide;

public sealed class GuideCallerAuthorizationFunctionalTests : IAsyncLifetime
{
    private WebApplication? authority;
    private WebApplication? orders;
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        authority = await StartAuthorityAsync();
        var registered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = "https://localhost/" });
        builder.Services.AddTestJwtBearer();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "orders",
            ServiceVersion = "1",
            Assemblies = [typeof(OrderSource).Assembly],
        })
            .AddEventing()
            .AddProcess()
            .AddQueryable()
            .AddInMemoryProviders()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddProcessHttpServer()
            .AddProcessHttpBindings()
            .AddQueryableHttpBindings()
            .AddKaleidoClientHandler<GuideServiceTokenHandler>()
            .AddRegistryHttpParticipation();
        builder.Services.AddSingleton<OrderBook>();
        builder.Services.Configure<HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new RegistrationHandler(registered)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        orders = builder.Build();
        orders.UseAuthentication();
        orders.UseAuthorization();
        orders.MapKaleido().MapProcess().MapQueryable();
        await orders.StartAsync(Cancel);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
    }

    public async ValueTask DisposeAsync()
    {
        if (orders is not null)
        {
            await orders.StopAsync(CancellationToken.None);
            await orders.DisposeAsync();
        }

        if (authority is not null)
        {
            await authority.StopAsync(CancellationToken.None);
            await authority.DisposeAsync();
        }
    }

    [Fact]
    public async Task TheRegistry_ShowsEachCallerOnlyWhatItMayCall()
    {
        var registry = Assert.IsType<WebApplication>(authority);
        using var clerk = registry.GetTestClient();
        clerk.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("cleo", ["clerk"]));
        using var manager = registry.GetTestClient();
        manager.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User("max", ["manager"]));

        using var clerkResponse = await clerk.GetAsync("/registry", Cancel);
        using var managerResponse = await manager.GetAsync("/registry", Cancel);

        Assert.Equal(HttpStatusCode.OK, clerkResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, managerResponse.StatusCode);
        var clerkRegistry = await clerkResponse.Content.ReadAsStringAsync(Cancel);
        var managerRegistry = await managerResponse.Content.ReadAsStringAsync(Cancel);
        Assert.Contains("ShipOrderStep", clerkRegistry, StringComparison.Ordinal);
        Assert.DoesNotContain("RefundOrderStep", clerkRegistry, StringComparison.Ordinal);
        Assert.Contains("RefundOrderStep", managerRegistry, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACallerWithoutTheRole_IsForbidden_AndAnAnonymousCaller_IsUnauthorized()
    {
        using var clerk = Caller("cleo", ["clerk"]);
        var processId = await ShippedOrderAsync(clerk);
        using var anonymous = orders!.GetTestClient();
        using var manager = Caller("max", ["manager"]);

        using var forbidden = await SendAsync(clerk, "refundorderstep", new { }, processId);
        using var unauthorized = await SendAsync(anonymous, "placeorderstep", new { item = "Book" }, null);
        using var refunded = await SendAsync(manager, "refundorderstep", new { }, processId);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal(HttpStatusCode.OK, refunded.StatusCode);
        var result = Assert.IsType<ProcessResult>(await refunded.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(result.Steps).ExecutionStatus);
    }

    [Fact]
    public async Task AnotherSignedCallerWithTheProcessId_MayContinueIt()
    {
        using var ann = Caller("ann", []);
        using var ben = Caller("ben", []);
        var placed = await ExecuteAsync(ann, "placeorderstep", new { item = "Book" }, null);

        var confirmed = await ExecuteAsync(ben, "confirmorderstep", new { }, placed.ProcessId);

        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(confirmed.Steps).ExecutionStatus);
    }

    [Fact]
    public void TheGuideTokenExchangeRegistration_InstallsBothClientsAndTheExchangeMode()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITokenExchange>(new NoCallsTokenExchange());
        CallCredentialsRegistration.AddWithTokenExchange(services, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "https://localhost/",
        }).Build());
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IKaleidoProcessorClientFactory>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IKaleidoQueryableClientFactory>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TokenExchangeHandler>());
        Assert.Equal(KaleidoOnBehalfOfCalls.Exchange, scope.ServiceProvider.GetRequiredService<KaleidoClientOptions>().OnBehalfOfCalls);
    }

    private HttpClient Caller(string name, string[] roles)
    {
        var client = orders!.GetTestClient();
        client.BaseAddress = new Uri("https://localhost/");
        client.DefaultRequestHeaders.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.User(name, roles));
        return client;
    }

    private static async Task<Guid> ShippedOrderAsync(HttpClient client)
    {
        var placed = await ExecuteAsync(client, "placeorderstep", new { item = "Book" }, null);
        var processId = placed.ProcessId;
        await ExecuteAsync(client, "confirmorderstep", new { }, processId);
        await ExecuteAsync(client, "shiporderstep", new { carrier = "post", address = "1 Main St" }, processId);
        return processId;
    }

    private static async Task<ProcessResult> ExecuteAsync(HttpClient client, string step, object input, Guid? processId)
    {
        using var response = await SendAsync(client, step, input, processId);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string step, object input, Guid? processId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/orders/1/processes/steps/{step}")
        {
            Content = JsonContent.Create(new { processStep = input }, options: KaleidoJsonOptions.Options),
        };
        if (processId is { } id)
        {
            request.Headers.TryAddWithoutValidation(KaleidoCorrelationHeaders.ProcessId, id.ToString("D"));
        }

        return await client.SendAsync(request, Cancel);
    }

    private static async Task<WebApplication> StartAuthorityAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestJwtBearer();
        builder.Services.AddAuthorization(policies =>
        {
            policies.AddPolicy("registry-registrant", policy => policy.RequireRole("registrant"));
            policies.AddPolicy("registry-reader", policy => policy.RequireRole("registry-reader"));
        });
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(GuideCallerAuthorizationFunctionalTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.Authenticated })
            .AddHttp()
            .AddControlRegistry(new KaleidoRegistryOptions { RegistrantPolicy = "registry-registrant", ReaderPolicy = "registry-reader" })
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapKaleido().MapCentralRegistry();
        await app.StartAsync(Cancel);
        return app;
    }

    private sealed class NoCallsTokenExchange : ITokenExchange
    {
        public Task<string> ExchangeAsync(string callerAuthorization, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class GuideServiceTokenHandler : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Authorization = TestJwtAuthentication.Bearer(TestTokenIssuer.App("orders-app", ["registrant", "registry-reader", "Kaleido.Registrant.orders"]));
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class RegistrationHandler(TaskCompletionSource registered) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/registry/instances" && response.IsSuccessStatusCode)
            {
                registered.TrySetResult();
            }

            return response;
        }
    }
}
