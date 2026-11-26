using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Time.Testing;

namespace Kaleido.Process.Http.Client.FunctionalTests;

public sealed class SharedGatewayResilienceFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ATransientRead_UsesTheSharedNamedClientPipeline()
    {
        var (provider, handler, time) = Build(enabled: true, maxRetries: 1);
        using (provider)
        {
            Assert.Equal(2, await GetAttemptsAsync(provider, handler, time, "kaleido-registry", "/registry/capabilities"));
        }
    }

    [Fact]
    public async Task DisablingResilience_MakesOnlyOneAttempt()
    {
        var (provider, handler, time) = Build(enabled: false, maxRetries: 1);
        using (provider)
        {
            Assert.Equal(1, await GetAttemptsAsync(provider, handler, time, "kaleido-registry", "/registry/capabilities"));
        }
    }

    [Fact]
    public async Task TheOneSharedSetting_AppliesToRegistryAndProcessNamedClients()
    {
        var (provider, handler, time) = Build(enabled: true, maxRetries: 3);
        using (provider)
        {
            Assert.Equal(4, await GetAttemptsAsync(provider, handler, time, "kaleido-registry", "/registry/capabilities"));
            Assert.Equal(4, await GetAttemptsAsync(provider, handler, time, "kaleido-process", "/catalog/1/processes/state"));
        }
    }

    private static (ServiceProvider Provider, CountingHandler Handler, FakeTimeProvider Time) Build(bool enabled, int maxRetries)
    {
        var time = new FakeTimeProvider();
        var handler = new CountingHandler();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(time);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:BaseUrl"] = "https://gateway.test/",
            ["Kaleido:Resilience:Enabled"] = enabled.ToString(),
            ["Kaleido:Resilience:MaxRetryAttempts"] = maxRetries.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Kaleido:Resilience:RetryDelay"] = "00:00:01",
            ["Kaleido:Resilience:TotalTimeout"] = "00:02:00",
            ["Kaleido:Resilience:AttemptTimeout"] = "00:01:00",
        }).Build();
        services.AddKaleido(configuration, new()
        {
            ServiceName = "reader",
            ServiceVersion = "1",
            Assemblies = [typeof(SharedGatewayResilienceFunctionalTests).Assembly],
            TypeFilter = _ => false,
        }).AddRegistryHttpParticipation().AddProcessHttpClient();
        foreach (var name in new[] { "kaleido-registry", "kaleido-process" })
        {
            services.Configure<HttpClientFactoryOptions>(name, options =>
                options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler));
        }

        return (services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }), handler, time);
    }

    private static async Task<int> GetAttemptsAsync(ServiceProvider provider, CountingHandler handler, FakeTimeProvider time, string name, string path)
    {
        handler.ClearNotifications();
        var before = handler.Attempts;
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(name);
        var pending = client.GetAsync(path, Cancel);
        await handler.NextAttemptAsync();
        for (var tick = 0; tick < 12 && !pending.IsCompleted; tick++)
        {
            await Task.Yield();
            time.Advance(TimeSpan.FromSeconds(2));
        }

        Assert.True(pending.IsCompleted);
        using var response = await pending.WaitAsync(TimeSpan.FromSeconds(5), Cancel);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        return handler.Attempts - before;
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        private readonly System.Threading.Channels.Channel<int> attempts = System.Threading.Channels.Channel.CreateUnbounded<int>();
        private int count;
        public int Attempts => Volatile.Read(ref count);
        public Task<int> NextAttemptAsync() => attempts.Reader.ReadAsync(Cancel).AsTask();
        public void ClearNotifications()
        {
            while (attempts.Reader.TryRead(out _))
            {
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            attempts.Writer.TryWrite(Interlocked.Increment(ref count));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { RequestMessage = request });
        }
    }
}
