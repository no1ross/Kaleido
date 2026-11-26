using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kaleido.Hosting.Http.UnitTests;

public sealed class KaleidoEndpointRouteBuilderExtensionsTests : SutFixture
{
    [Fact]
    public void MapKaleido_WhenEndpointsIsNull_Throws()
    {
        var method = Assert.IsAssignableFrom<MethodInfo>(typeof(KaleidoEndpointRouteBuilderExtensions)
            .GetMethod(nameof(KaleidoEndpointRouteBuilderExtensions.MapKaleido), [typeof(IEndpointRouteBuilder)]));

        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [null]));

        Assert.IsType<ArgumentNullException>(error.InnerException);
    }

    [Fact]
    public void MapKaleido_WithNoRegistryOrCapability_MapsNothingAndReturnsConventionBuilder()
    {
        using var app = WebApplication.CreateBuilder().Build();

        var builder = app.MapKaleido();

        Assert.IsAssignableFrom<IEndpointConventionBuilder>(builder);
        Assert.Same(app.Services, builder.Endpoints.ServiceProvider);
        Assert.Empty(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints));
    }

    [Fact]
    public void MapProcess_WithoutProcessRegistration_FailsBeforeMapping()
    {
        using var app = WebApplication.CreateBuilder().Build();

        var error = Assert.Throws<KaleidoConfigurationException>(app.MapKaleido().MapProcess);

        Assert.Equal(ProcessorErrorCodes.InvalidRegistration, error.Code);
        Assert.Empty(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints));
    }

    [Fact]
    public void MapQueryable_WithoutQueryableRegistration_FailsBeforeMapping()
    {
        using var app = WebApplication.CreateBuilder().Build();

        var error = Assert.Throws<KaleidoConfigurationException>(app.MapKaleido().MapQueryable);

        Assert.Equal(QueryableErrorCodes.InvalidRegistration, error.Code);
        Assert.Empty(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints));
    }

    [Fact]
    public void MapCentralRegistry_WithoutAuthorityRegistration_FailsBeforeMapping()
    {
        using var app = WebApplication.CreateBuilder().Build();

        var error = Assert.Throws<KaleidoConfigurationException>(app.MapKaleido().MapCentralRegistry);

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, error.Code);
        Assert.Empty(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints));
    }

    [Theory]
    [InlineData(nameof(KaleidoEndpointRouteBuilderExtensions.MapProcess))]
    [InlineData(nameof(KaleidoEndpointRouteBuilderExtensions.MapQueryable))]
    [InlineData(nameof(KaleidoEndpointRouteBuilderExtensions.MapCentralRegistry))]
    public void ExplicitMap_WhenBuilderIsNull_Throws(string methodName)
    {
        var method = Assert.IsAssignableFrom<MethodInfo>(typeof(KaleidoEndpointRouteBuilderExtensions)
            .GetMethod(methodName, [typeof(IKaleidoHttpEndpointBuilder)]));

        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [null]));

        Assert.IsType<ArgumentNullException>(error.InnerException);
    }

    [Fact]
    public void MapQueryable_WithoutGenericHttpRegistration_ReportsTheMissingRegistrationBeforeMapping()
    {
        var services = WebApplication.CreateBuilder();
        services.Services.AddSingleton(Mock.Of<IQueryableService>());
        services.Services.AddSingleton(new KaleidoServerOptions { AuthorizationMode = KaleidoAuthorizationMode.None });
        using var app = services.Build();

        var error = Assert.Throws<KaleidoConfigurationException>(app.MapKaleido().MapQueryable);

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, error.Code);
        Assert.Contains("AddHttp()", error.Message, StringComparison.Ordinal);
        Assert.Empty(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints));
    }

    [Fact]
    public void MapProcess_WithoutProcessHttpAdapter_ReportsTheMissingRegistrationBeforeMapping()
    {
        var services = WebApplication.CreateBuilder();
        services.Services.AddSingleton(Mock.Of<IProcessorRuntime>());
        services.Services.AddSingleton(new KaleidoHttpServerOptions());
        services.Services.AddSingleton(new KaleidoServerOptions { AuthorizationMode = KaleidoAuthorizationMode.None });
        using var app = services.Build();

        var error = Assert.Throws<KaleidoConfigurationException>(app.MapKaleido().MapProcess);

        Assert.Equal(ConfigurationErrorCodes.InvalidOptions, error.Code);
        Assert.Contains("AddProcessHttpServer()", error.Message, StringComparison.Ordinal);
        Assert.Empty(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(KaleidoAuthorizationMode.Authenticated)]
    [InlineData(KaleidoAuthorizationMode.ZeroTrust)]
    public void MapProcess_WithoutAnAuthenticationScheme_ThrowsBeforeMapping(KaleidoAuthorizationMode? mode)
    {
        var services = WebApplication.CreateBuilder();
        services.Services.AddSingleton(Mock.Of<IProcessorRuntime>());
        services.Services.AddSingleton(new KaleidoHttpServerOptions());
        services.Services.AddSingleton(new KaleidoProcessHttpOptions());
        services.Services.AddSingleton(mode is { } selected ? new KaleidoServerOptions { AuthorizationMode = selected } : new KaleidoServerOptions());
        using var app = services.Build();

        var error = Assert.Throws<KaleidoConfigurationException>(app.MapKaleido().MapProcess);

        Assert.Equal(ConfigurationErrorCodes.AuthenticationNotConfigured, error.Code);
        Assert.Contains("Configure AddAuthentication", error.Message, StringComparison.Ordinal);
        Assert.Empty(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints));
    }

    [Fact]
    public void MapQueryable_WithoutAnAuthenticationScheme_ThrowsBeforeMapping()
    {
        var services = WebApplication.CreateBuilder();
        services.Services.AddSingleton(Mock.Of<IQueryableService>());
        services.Services.AddSingleton(new KaleidoHttpServerOptions());
        services.Services.AddSingleton(new KaleidoServerOptions());
        using var app = services.Build();

        var error = Assert.Throws<KaleidoConfigurationException>(app.MapKaleido().MapQueryable);

        Assert.Equal(ConfigurationErrorCodes.AuthenticationNotConfigured, error.Code);
        Assert.Empty(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints));
    }

    [Fact]
    public void MapProcess_WhenAuthorizationIsDisabled_WarnsBeforeDelegatingToProcess()
    {
        var services = WebApplication.CreateBuilder();
        var logger = new Mock<ILogger>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var loggerFactory = new Mock<ILoggerFactory>();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(logger.Object);
        services.Services.AddSingleton(loggerFactory.Object);
        services.Services.AddSingleton(Mock.Of<IProcessorRuntime>());
        services.Services.AddSingleton(new KaleidoHttpServerOptions());
        services.Services.AddSingleton(new KaleidoProcessHttpOptions());
        services.Services.AddSingleton(new KaleidoServerOptions { AuthorizationMode = KaleidoAuthorizationMode.None });
        using var app = services.Build();

        Assert.Throws<KaleidoConfigurationException>(app.MapKaleido().MapProcess);

        logger.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.Is<It.IsAnyType>((_, _) => true),
            It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public void MapKaleido_ConventionsDoNotApplyToAnUnrelatedHostEndpoint()
    {
        using var app = WebApplication.CreateBuilder().Build();
        app.MapGet("/outside", () => Results.Ok());
        app.MapKaleido().Add(endpoint => endpoint.Metadata.Add(new Marker()));

        var route = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(x => x.Endpoints).OfType<RouteEndpoint>());
        Assert.Empty(route.Metadata.OfType<Marker>());
    }

    private sealed record Marker;
}
