using System.Net;
using System.Text;
using Kaleido.Eventing;
using Kaleido.Http.FunctionalTests.Queryable.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Http.FunctionalTests.Queryable.Execution;

public sealed class EventPublisherFailureTests
{
    [Fact]
    public async Task Query_WhenPublisherThrowsOnCall_StillSucceeds() =>
        await AssertQuerySucceedsAsync<ThrowingPublisher>();

    [Fact]
    public async Task Query_WhenPublisherTaskFaults_StillSucceeds() =>
        await AssertQuerySucceedsAsync<FaultingPublisher>();

    private static async Task AssertQuerySucceedsAsync<TPublisher>()
        where TPublisher : class, IEventPublisher
    {
        using var host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder.UseTestServer();
                webBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton<FunctionalRecordData>();
                    services.AddKaleido(new ConfigurationBuilder().Build(), o =>
                        {
                            o.ServiceName = "kaleido";
                            o.Assemblies = [typeof(FunctionalRecordContext).Assembly];
                        })
                        .AddEventPublisher<TPublisher>()
                        .AddHttp();
                });
                webBuilder.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);

        using var content = new StringContent("""{ "query": {} }""", Encoding.UTF8, "application/json");
        using var response = await host.GetTestClient().PostAsync(
            "/kaleido/queryable/functional-records/query",
            content,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class ThrowingPublisher : IEventPublisher
    {
        public Task PublishAsync<TEvent, TContext>(
            KaleidoEventEnvelope<TEvent, TContext> envelope,
            CancellationToken cancellationToken = default)
            where TEvent : IKaleidoEvent =>
            throw new InvalidOperationException("publisher failed on call");
    }

    private sealed class FaultingPublisher : IEventPublisher
    {
        public Task PublishAsync<TEvent, TContext>(
            KaleidoEventEnvelope<TEvent, TContext> envelope,
            CancellationToken cancellationToken = default)
            where TEvent : IKaleidoEvent =>
            Task.FromException(new InvalidOperationException("publisher task faulted"));
    }
}
