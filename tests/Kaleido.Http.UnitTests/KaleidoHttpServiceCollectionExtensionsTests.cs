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
    public void AddHttp_RegistersKaleidoStartupFilter()
    {
        var webBuilder = WebApplication.CreateBuilder();
        webBuilder.WebHost.UseTestServer();

        var kaleidoBuilder = webBuilder.Services.AddKaleido(webBuilder.Configuration, ConfigureKaleido("test-service"));
        kaleidoBuilder.AddHttp();

        var serviceProvider = webBuilder.Services.BuildServiceProvider();
        var startupFilter = serviceProvider.GetService<IStartupFilter>();

        Assert.NotNull(startupFilter);
        Assert.IsType<KaleidoStartupFilter>(startupFilter);
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
