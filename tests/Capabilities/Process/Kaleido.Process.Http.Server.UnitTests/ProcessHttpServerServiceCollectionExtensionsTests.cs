using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Process.Http.Server.UnitTests;

public sealed class ProcessHttpServerServiceCollectionExtensionsTests : Kaleido.UnitTests.SutFixture
{
    private static (IKaleidoBuilder Builder, IServiceCollection Services) CreateSut(IConfiguration? configuration = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IProcessorRuntime>());
        services.AddSingleton(new KaleidoServerOptions());
        services.AddSingleton(new KaleidoHttpServerOptions());
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        builder.Setup(x => x.Configuration).Returns(configuration ?? new ConfigurationBuilder().Build());
        return (builder.Object, services);
    }

    [Fact]
    public void AddProcessHttpServer_BindsConfigurationOverACopyOfCodeOptions()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Kaleido:Process:Http:IdempotencyLease"] = "00:00:06",
        }).Build();
        var (builder, services) = CreateSut(configuration);
        var supplied = new KaleidoProcessHttpOptions { IdempotencyLease = TimeSpan.FromMinutes(10), IdempotencyRetention = TimeSpan.FromHours(3) };

        builder.AddProcessHttpServer(supplied);

        var effective = Assert.IsType<KaleidoProcessHttpOptions>(Assert.Single(services, x => x.ServiceType == typeof(KaleidoProcessHttpOptions)).ImplementationInstance);
        Assert.NotSame(supplied, effective);
        Assert.Equal(TimeSpan.FromSeconds(6), effective.IdempotencyLease);
        Assert.Equal(TimeSpan.FromHours(3), effective.IdempotencyRetention);
        Assert.Equal(TimeSpan.FromMinutes(10), supplied.IdempotencyLease);
        Assert.Single(services, x => x.ServiceType == typeof(IProcessExecutionService));
        Assert.Single(services, x => x.ServiceType == typeof(IProcessStateService));
    }

    [Fact]
    public void AddProcessHttpServer_RejectsDuplicateRegistration()
    {
        var (builder, _) = CreateSut();
        builder.AddProcessHttpServer();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddProcessHttpServer());

        Assert.Equal(ConfigurationErrorCodes.DuplicateRegistration, exception.Code);
    }

    [Fact]
    public void AddProcessHttpServer_RequiresGenericProcessAndBothServerOwners()
    {
        var services = new ServiceCollection();
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().Build());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.Object.AddProcessHttpServer());

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, exception.Code);
        Assert.Empty(services);
    }
}
