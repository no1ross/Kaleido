using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Client.Registration.UnitTests;

public sealed class KaleidoHttpClientPipelineServiceCollectionExtensionsTests : Kaleido.UnitTests.SutFixture
{
    private sealed class HostCredentialHandler : DelegatingHandler;

    private static IKaleidoBuilder Builder(IServiceCollection services, string? gateway = "https://gateway.test/", bool obsoleteClients = false, string? onBehalfOfCalls = null)
    {
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        var settings = new Dictionary<string, string?> { ["Kaleido:BaseUrl"] = gateway };
        if (obsoleteClients)
        {
            settings["Kaleido:Clients:clinic:BaseUrl"] = "https://clinic.test/";
        }

        if (onBehalfOfCalls is not null)
        {
            settings["Kaleido:OnBehalfOfCalls"] = onBehalfOfCalls;
        }

        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        return builder.Object;
    }

    [Fact]
    public void AddPipelineAndNamedClient_UseTheOneGatewayAndSharedHandlerPolicy()
    {
        var services = new ServiceCollection();
        var builder = Builder(services);
        builder.AddKaleidoClientHandler<HostCredentialHandler>();
        builder.AddKaleidoHttpClientPipeline().AddKaleidoHttpClientPipeline();
        builder.AddKaleidoNamedClient("kaleido-registry");

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("kaleido-registry");
        Assert.Equal(new Uri("https://gateway.test/"), client.BaseAddress);
        Assert.Single(services, x => x.ImplementationInstance is KaleidoClientHandlerRegistration);
        Assert.Single(services, x => x.ImplementationInstance is KaleidoHttpClientName);
        Assert.Single(services, x => x.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
            && x.ImplementationType == typeof(KaleidoClientTransportCheck));
    }

    [Fact]
    public async Task AddNamedClient_LogsANonSuccessForItsRemoteServiceWithoutQueryOrCredentials()
    {
        var services = new ServiceCollection();
        var logger = new RecordingLogger();
        services.AddSingleton<ILogger<KaleidoClientCallLoggingHandler>>(logger);
        var builder = Builder(services);
        builder.AddKaleidoHttpClientPipeline();
        builder.AddKaleidoNamedClient("kaleido-process");
        services.Configure<HttpClientFactoryOptions>("kaleido-process", options =>
            options.HttpMessageHandlerBuilderActions.Add(handlers => handlers.PrimaryHandler = new BadRequestHandler()));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("kaleido-process");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/catalog/1/processes/state?credential=do-not-log");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer do-not-log");
        request.Options.Set(KaleidoClientCallLoggingHandler.TargetService, "catalog");
        KaleidoCallContext.MarkServiceOwn(request);

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var warning = Assert.Single(logger.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("catalog", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-log", warning.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddPipeline_RejectsObsoletePerServiceClientAddresses()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder(new ServiceCollection(), obsoleteClients: true).AddKaleidoHttpClientPipeline());

        Assert.Equal(HttpClientErrorCodes.InvalidClientOptions, exception.Code);
        Assert.Contains("Kaleido:Clients", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddPipeline_MissingGateway_UsesThePublishedErrorCode()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder(new ServiceCollection(), gateway: null).AddKaleidoHttpClientPipeline());

        Assert.Equal(HttpClientErrorCodes.MissingBaseUrl, exception.Code);
    }

    [Theory]
    [InlineData(null, KaleidoOnBehalfOfCalls.Forward)]
    [InlineData("Forward", KaleidoOnBehalfOfCalls.Forward)]
    [InlineData("Exchange", KaleidoOnBehalfOfCalls.Exchange)]
    public void AddPipeline_UsesTheConfiguredCallerTokenMode(string? configured, KaleidoOnBehalfOfCalls expected)
    {
        var services = new ServiceCollection();
        Builder(services, onBehalfOfCalls: configured).AddKaleidoHttpClientPipeline();

        Assert.Equal(expected, Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<KaleidoClientOptions>()).OnBehalfOfCalls);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(42)]
    [InlineData("")]
    [InlineData(" ")]
    public void RequireServiceName_RejectsKeysWithoutAServiceName(object? key)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(key.RequireServiceName);

        Assert.Equal(HttpClientErrorCodes.UnknownClient, exception.Code);
    }

    [Fact]
    public void RequireServiceName_KeepsTheNamedService()
    {
        Assert.Equal("radiology", ((object)"radiology").RequireServiceName());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-gateway")]
    [InlineData("ftp://gateway.test/")]
    [InlineData("http://")]
    [InlineData("http://user:secret@gateway.test/")]
    [InlineData("https://name:password@gateway.test/")]
    public void AddPipeline_RejectsAnInvalidGateway(string? gateway)
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder(new ServiceCollection(), gateway).AddKaleidoHttpClientPipeline());
        Assert.Contains("BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    private sealed class BadRequestHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest));
    }

    private sealed class RecordingLogger : ILogger<KaleidoClientCallLoggingHandler>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((level, formatter(state, exception)));
    }
}
