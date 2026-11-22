using System.Net;
using System.Net.Http.Json;
using Kaleido.Eventing;
using Kaleido.Http;
using Kaleido.Http.Server;
using Kaleido.Provider.InMemory;
using Kaleido.Queryable.Http.Server.FunctionalTests.Contract;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.Http.Server.FunctionalTests.Publisher;

public sealed class PublisherFailureFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AQuery_ReturnsItsResults_WhenTheOutboxPublishThrowsOrFaults(bool throwOnCall)
    {
        var publisher = new FailingPublisher(throwOnCall);
        await using var app = await StartAsync(publisher);
        using var client = app.GetTestClient();

        using var response = await client.PostAsJsonAsync("/catalog/1/queryable/contractsource/query",
            new { query = new { } }, KaleidoJsonOptions.Options, Cancel);
        await publisher.Started.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(KaleidoJsonOptions.Options, Cancel);
        Assert.Equal(6, result.GetProperty("totalCount").GetInt32());
        Assert.Equal(3, result.GetProperty("results").GetArrayLength());
    }

    private static async Task<WebApplication> StartAsync(FailingPublisher publisher)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ContractRecords>();
        builder.Services.AddSingleton(publisher);
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "catalog",
            ServiceVersion = "1",
            Assemblies = [typeof(PublisherFailureFunctionalTests).Assembly],
            TypeFilter = type => type == typeof(ContractSource),
        })
            .AddEventing()
            .AddQueryable()
            .AddServer(new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddHttp()
            .AddInMemoryOutboxProvider()
            .AddOutboxRelay<FailingPublisher>();
        var app = builder.Build();
        app.MapQueryable();
        await app.StartAsync(Cancel);
        return app;
    }

    private sealed class FailingPublisher(bool throwOnCall) : IEventPublisher
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task PublishAsync<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope, CancellationToken cancellationToken)
            where TEvent : IKaleidoEvent
        {
            Started.TrySetResult();
            return throwOnCall
                ? throw new InvalidOperationException("publisher failed on call")
                : Task.FromException(new InvalidOperationException("publisher task faulted"));
        }
    }
}
