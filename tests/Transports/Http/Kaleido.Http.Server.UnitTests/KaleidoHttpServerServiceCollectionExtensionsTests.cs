using Kaleido.Exceptions;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.Server.UnitTests;

public sealed class KaleidoHttpServerServiceCollectionExtensionsTests : SutFixture
{
    private static KaleidoServiceOptions ConfigureKaleido() =>
        new()
        {
            ServiceName = "test-service",
            ServiceVersion = "1",
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
        var builder = services.AddKaleido(new ConfigurationBuilder().Build(), ConfigureKaleido());

        Assert.Same(builder, builder.AddHttp());
    }

    [Fact]
    public void AddHttp_RegistersGenericServicesAndOptions()
    {
        var services = new ServiceCollection();
        var builder = services.AddKaleido(new ConfigurationBuilder().Build(), ConfigureKaleido());

        builder.AddHttp();

        Assert.IsType<KaleidoHttpServerOptions>(Assert.Single(services, d => d.ServiceType == typeof(KaleidoHttpServerOptions)).ImplementationInstance);
        Assert.Contains(services, d => d.ServiceType == typeof(IKaleidoAuthorizer));
        Assert.Contains(services, d => d.ServiceType == typeof(IKaleidoExceptionHandler));
        Assert.Contains(services, d => d.ServiceType == typeof(IHttpContextAccessor));
    }

    [Fact]
    public void AddHttp_PreservesCodeProvidedCorrelationTrust()
    {
        var services = new ServiceCollection();
        var builder = services.AddKaleido(new ConfigurationBuilder().Build(), ConfigureKaleido());
        Func<HttpContext, bool> trust = context => context.User.Identity?.IsAuthenticated == true;
        var options = new KaleidoHttpServerOptions { TrustCorrelationIdentity = trust };

        builder.AddHttp(options);

        var registered = Assert.IsType<KaleidoHttpServerOptions>(Assert.Single(services, d => d.ServiceType == typeof(KaleidoHttpServerOptions)).ImplementationInstance);
        Assert.Same(trust, registered.TrustCorrelationIdentity);
    }

    [Fact]
    public void AddHttp_CalledTwice_IsAConfigurationError()
    {
        var services = new ServiceCollection();
        var builder = services.AddKaleido(new ConfigurationBuilder().Build(), ConfigureKaleido());
        builder.AddHttp();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddHttp());

        Assert.Equal(ConfigurationErrorCodes.DuplicateRegistration, exception.Code);
    }

    [Fact]
    public void AddHttp_AddsNothingToTheHostsPipeline()
    {
        var webBuilder = WebApplication.CreateBuilder();
        webBuilder.WebHost.UseTestServer();
        var before = webBuilder.Services.Count(x => x.ServiceType == typeof(IStartupFilter));

        webBuilder.Services.AddKaleido(webBuilder.Configuration, ConfigureKaleido()).AddHttp();

        Assert.Equal(before, webBuilder.Services.Count(x => x.ServiceType == typeof(IStartupFilter)));
    }
}
