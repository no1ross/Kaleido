using Kaleido.Exceptions;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoHttpServiceCollectionExtensionsTests
    : SutFixture
{
    private static KaleidoServiceOptions ConfigureKaleido(string serviceName) =>
        new()
        {
            ServiceName = serviceName,
            Assemblies = [typeof(KaleidoServiceOptions).Assembly],
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

        builder.AddHttp(new() { IncludeFrameworkMessages = true });

        var options = Assert.Single(services, d => d.ServiceType == typeof(KaleidoHttpOptions)).ImplementationInstance;
        Assert.True(Assert.IsType<KaleidoHttpOptions>(options).IncludeFrameworkMessages);
    }

    // ADR 0030: the Kaleido:Http section overrides the options from code, setting by setting.
    [Fact]
    public void AddHttp_LaysTheKaleidoHttpSectionOverTheOptionsFromCode()
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:Http:IdempotencyLease"] = "00:10:00" })
            .Build();
        var services = new ServiceCollection();
        var code = new KaleidoHttpOptions { IncludeFrameworkMessages = true, IdempotencyLease = TimeSpan.FromMinutes(1) };

        services.AddKaleido(configuration, ConfigureKaleido("test")).AddHttp(code);

        var options = Assert.IsType<KaleidoHttpOptions>(Assert.Single(services, d => d.ServiceType == typeof(KaleidoHttpOptions)).ImplementationInstance);
        Assert.Equal(TimeSpan.FromMinutes(10), options.IdempotencyLease);
        Assert.True(options.IncludeFrameworkMessages);
        Assert.Equal(TimeSpan.FromMinutes(1), code.IdempotencyLease);
    }

    [Theory]
    [InlineData("Kaleido:Http:IdempotencyLease", "00:00:00")]
    [InlineData("Kaleido:Http:IdempotencyRetention", "-00:00:01")]
    [InlineData("Kaleido:Http:IdempotencyLease", "soon")]
    public void AddHttp_AnInvalidSetting_IsAConfigurationError(string key, string value)
    {
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();
        var builder = new ServiceCollection().AddKaleido(configuration, ConfigureKaleido("test"));

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddHttp());

        Assert.Equal(ConfigurationErrorCodes.InvalidHttpOptions, exception.Code);
        Assert.Contains(key, exception.Message, StringComparison.Ordinal);
    }

    // #301: a second call would silently replace the first one's options.
    [Fact]
    public void AddHttp_CalledTwice_IsAConfigurationError()
    {
        var services = new ServiceCollection();
        var builder = services.AddKaleido(new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build(), ConfigureKaleido("test"));
        builder.AddHttp(new() { IncludeFrameworkMessages = true });

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddHttp());

        Assert.Equal(ConfigurationErrorCodes.DuplicateRegistration, exception.Code);
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
