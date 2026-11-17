using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Registry.IntegrationTests;

public sealed class RegistryPublicationIntegrationTests
{
    [Fact]
    public async Task QueryableHost_PublishesItsActualMappedHttpBinding_ThenDeregisters()
    {
        await using var authority = await StartAuthorityAsync();
        var registered = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "http://localhost/",
        });
        var outbox = new Mock<IKaleidoOutboxStore>();
        outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        builder.Services.AddSingleton(outbox.Object);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(RegistryPublicationIntegrationTests).Assembly],
            TypeFilter = type => type == typeof(CatalogSource),
        })
            .AddRegistryHttpParticipation()
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddQueryableHttpBindings();
        builder.Services.Configure<Microsoft.Extensions.Http.HttpClientFactoryOptions>("kaleido-registry", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new ObserveRegistrationHandler(registered)
            {
                InnerHandler = authority.GetTestServer().CreateHandler(),
            }));
        await using var publisher = builder.Build();
        publisher.MapQueryable();

        await publisher.StartAsync(TestContext.Current.CancellationToken);
        await registered.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        using var response = await authority.GetTestClient().GetAsync("/registry/capabilities", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var registry = await response.Content.ReadFromJsonAsync<KaleidoRegistry>(KaleidoJsonOptions.Options, TestContext.Current.CancellationToken);
        var source = Assert.Single(Assert.IsType<KaleidoRegistry>(registry).Queryables);
        Assert.Equal("CatalogSource", source.Name);
        Assert.Equal("/catalog/1/queryable/catalogsource/query", Assert.Single(source.Bindings.Http).Path);

        using var mapped = await publisher.GetTestClient().GetAsync("/catalog/1/queryable/catalogsource/query", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, mapped.StatusCode);
        await publisher.StopAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<WebApplication> StartAuthorityAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "registry",
            ServiceVersion = "1",
            Assemblies = [typeof(RegistryPublicationIntegrationTests).Assembly],
            TypeFilter = _ => false,
        })
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddControlRegistry(new KaleidoRegistryOptions { Cache = new KaleidoRegistryCacheOptions { ViewTtl = TimeSpan.FromHours(1) } })
            .AddInMemoryRegistryStore()
            .AddRegistryHttpServer();
        var app = builder.Build();
        app.MapKaleidoControlRegistry();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    [QuerySource(DisplayName = "Catalog", Description = "Catalog entries")]
    public sealed class CatalogSource : IQuerySource<CatalogRecord>
    {
        public IQueryable<CatalogRecord> CreateQuery(QueryExecutionContext executionContext) =>
            new[] { new CatalogRecord { Name = "one" } }.AsQueryable();
    }

    public sealed class CatalogRecord : IQueryContext
    {
        public string Name { get; init; } = string.Empty;
    }

    private sealed class ObserveRegistrationHandler(TaskCompletionSource<bool> observed) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (request.Method == HttpMethod.Post
                && string.Equals(request.RequestUri?.AbsolutePath, "/registry/instances", StringComparison.Ordinal)
                && response.IsSuccessStatusCode)
            {
                observed.TrySetResult(true);
            }

            return response;
        }
    }
}
