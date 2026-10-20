using Kaleido.UnitTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoHttpServiceCollectionExtensionsTests
    : SutFixture
{
    private static Action<KaleidoServiceOptionsBuilder> ConfigureKaleido(string serviceName) =>
        o =>
        {
            o.ServiceName = serviceName;
            o.Assemblies = [typeof(KaleidoServiceOptions).Assembly];
        };

    [Fact]
    public void AddHttp_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddHttp());
    }

    [Fact]
    public void AddHttp_ReturnsSameBuilder()
    {
        var services = new ServiceCollection();
        var builder = services.AddKaleido(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), ConfigureKaleido("test"));

        var result = builder.AddHttp();

        Assert.Same(builder, result);
    }

    [Fact]
    public void AddHttp_AppliesTheConfiguredOptions()
    {
        var services = new ServiceCollection();
        var builder = services.AddKaleido(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), ConfigureKaleido("test"));

        builder.AddHttp(o => o.IncludeFrameworkMessages = true);

        var options = Assert.Single(services, d => d.ServiceType == typeof(KaleidoHttpOptions)).ImplementationInstance;
        Assert.True(Assert.IsType<KaleidoHttpOptions>(options).IncludeFrameworkMessages);
    }

    [Fact]
    public void AddHttp_WithoutConfiguration_RegistersDefaultOptions()
    {
        var services = new ServiceCollection();
        var builder = services.AddKaleido(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), ConfigureKaleido("test"));

        builder.AddHttp();

        var options = Assert.Single(services, d => d.ServiceType == typeof(KaleidoHttpOptions)).ImplementationInstance;
        Assert.False(Assert.IsType<KaleidoHttpOptions>(options).IncludeFrameworkMessages);
    }

    [Fact]
    // Kaleido's HTTP behavior lives on its own endpoints; nothing is added to the host's pipeline (#295).
    public void AddHttp_AddsNothingToTheHostsPipeline()
    {
        var webBuilder = WebApplication.CreateBuilder();
        webBuilder.WebHost.UseTestServer();
        var before = webBuilder.Services.Count(x => x.ServiceType == typeof(IStartupFilter));

        var kaleidoBuilder = webBuilder.Services.AddKaleido(webBuilder.Configuration, ConfigureKaleido("test-service"));
        kaleidoBuilder.AddHttp();

        Assert.Equal(before, webBuilder.Services.Count(x => x.ServiceType == typeof(IStartupFilter)));
    }

    [Fact]
    public void AddHttp_WithProcessRuntime_RegistersExecutionAndStateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IProcessorRegistry>());
        services.AddSingleton(Mock.Of<IProcessorRuntime>());

        var builder = services.AddKaleido(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), ConfigureKaleido("test"));
        builder.AddHttp();

        Assert.Contains(services, d => d.ServiceType == typeof(IProcessExecutionService));
        Assert.Contains(services, d => d.ServiceType == typeof(IProcessStateService));
    }

    [Fact]
    public void AddHttp_WithoutProcessRuntime_DoesNotRegisterExecutionServices()
    {
        var services = new ServiceCollection();
        var builder = services.AddKaleido(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), ConfigureKaleido("test"));
        builder.AddHttp();

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IProcessExecutionService));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IProcessStateService));
    }
}
