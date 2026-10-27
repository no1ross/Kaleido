using Kaleido.Exceptions;
using Kaleido.Http.Client;
using Kaleido.Http.Mcp.Observability;
using Kaleido.Http.Mcp.Tools;
using Kaleido.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Kaleido.Http.Mcp.UnitTests;

public sealed class KaleidoMcpServiceCollectionExtensionsTests
    : Kaleido.UnitTests.SutFixture
{
    private static Mock<IKaleidoBuilder> Builder(ServiceCollection services, IReadOnlyDictionary<string, string?>? settings = null)
    {
        var builder = new Mock<IKaleidoBuilder>();
        builder.Setup(x => x.Services).Returns(services);
        builder.Setup(x => x.Configuration).Returns(new ConfigurationBuilder().AddInMemoryCollection(settings ?? new Dictionary<string, string?>()).Build());
        return builder;
    }

    private static ServiceCollection HostServices()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IKaleidoRegistryService>());
        // AddHttp() registers the unfiltered registry the tool names come from (#435).
        services.AddScoped(_ => Mock.Of<IKaleidoUnfilteredRegistry>());
        // What AddHttpClients() registers with a client configured, and AddKaleido() the inbound caller.
        services.AddSingleton(new KaleidoClientOptions { Clients = new Dictionary<string, KaleidoClientEntry> { ["Clinic"] = new() } });
        services.AddSingleton(Mock.Of<IKaleidoCallCredentialProvider>());
        services.AddScoped(_ => Mock.Of<IKaleidoInboundCallerAccessor>());
        services.AddScoped(_ => Mock.Of<IKaleidoCorrelationContextInitializer>());
        return services;
    }

    private static KaleidoMcpOptions RegisteredOptions(ServiceCollection services) =>
        Assert.IsType<KaleidoMcpOptions>(Assert.Single(services, x => x.ServiceType == typeof(KaleidoMcpOptions)).ImplementationInstance);

    [Theory]
    [InlineData("0", 0)]
    [InlineData("3", 3)]
    public void AddMcp_BindsMaxElicitationRounds_FromConfiguration(string configured, int expected)
    {
        var services = HostServices();

        Builder(services, new Dictionary<string, string?> { ["Kaleido:Mcp:MaxElicitationRounds"] = configured }).Object.AddMcp();

        Assert.Equal(expected, RegisteredOptions(services).MaxElicitationRounds);
    }

    [Fact]
    public void AddMcp_BindsTheFrontDoor_FromConfiguration()
    {
        var services = HostServices();

        Builder(services, new Dictionary<string, string?>
        {
            ["Kaleido:Mcp:ToolListing"] = "FrontDoor",
            ["Kaleido:Mcp:ListedTools:0"] = "history_prior_auth_record_list",
        }).Object.AddMcp();

        var options = RegisteredOptions(services);
        Assert.Equal(McpToolListing.FrontDoor, options.ToolListing);
        Assert.Equal(["history_prior_auth_record_list"], options.ListedTools);
    }

    [Fact]
    public void AddMcp_NegativeMaxElicitationRoundsInConfiguration_FailsWithItsCode()
    {
        var builder = Builder(HostServices(), new Dictionary<string, string?> { ["Kaleido:Mcp:MaxElicitationRounds"] = "-1" });

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.Object.AddMcp());

        Assert.Equal(McpErrorCodes.InvalidOptions, exception.Code);
    }

    [Fact]
    public void AddMcp_NegativeMaxElicitationRoundsInConfigure_FailsWithItsCode()
    {
        var builder = Builder(HostServices());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.Object.AddMcp(new() { MaxElicitationRounds = -1 }));

        Assert.Equal(McpErrorCodes.InvalidOptions, exception.Code);
    }

    [Fact]
    public void AddMcp_WithoutAddHttp_FailsWithItsCode()
    {
        var builder = Builder([]);

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.Object.AddMcp());

        Assert.Equal(McpErrorCodes.MissingHttp, exception.Code);
    }

    [Fact]
    public void AddMcp_WithoutKaleidoClients_FailsWithItsCode()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => Mock.Of<IKaleidoRegistryService>());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder(services).Object.AddMcp());

        Assert.Equal(McpErrorCodes.MissingClients, exception.Code);
    }

    [Fact]
    public void AddMcp_OnAKaleidoHttpHostWithClients_RegistersTheAdapter_AndAppliesTheConfiguration()
    {
        var services = HostServices();

        Builder(services).Object.AddMcp(new() { ServerName = "priorauth" });

        var options = Assert.Single(services, x => x.ServiceType == typeof(KaleidoMcpOptions)).ImplementationInstance;
        Assert.Equal("priorauth", Assert.IsType<KaleidoMcpOptions>(options).ServerName);
    }

    // ADR 0022: fail closed - the host states how tool calls are authorized for the agent's user.
    [Fact]
    public void AddMcp_WithoutCallCredentials_FailsWithItsCode_NamingBothChoices()
    {
        var services = HostServices();
        services.RemoveAll<IKaleidoCallCredentialProvider>();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => Builder(services).Object.AddMcp());

        Assert.Equal(McpErrorCodes.MissingCallCredentials, exception.Code);
        Assert.Contains("ForwardCallerToken()", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddCallCredentials<TProvider>()", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddMcp_WithoutABuilder_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => KaleidoMcpServiceCollectionExtensions.AddMcp(null!));
    }

    [Fact]
    public void AddMcp_RegistersTheAdaptersServices_WithTheirLifetimes()
    {
        var services = HostServices();

        Builder(services).Object.AddMcp();

        Assert.Equal(ServiceLifetime.Singleton, Lifetime<IToolNameBuilder>(services));
        Assert.Equal(ServiceLifetime.Singleton, Lifetime<IToolSchemaBuilder>(services));
        Assert.Equal(ServiceLifetime.Singleton, Lifetime<IToolCatalogBuilder>(services));
        Assert.Equal(ServiceLifetime.Singleton, Lifetime<IInformationRequestElicitor>(services));
        Assert.Equal(ServiceLifetime.Scoped, Lifetime<IMcpObservability>(services));
        Assert.Equal(ServiceLifetime.Scoped, Lifetime<IToolCatalogProvider>(services));
        Assert.Equal(ServiceLifetime.Scoped, Lifetime<IKaleidoToolInvoker>(services));
        Assert.Equal(ServiceLifetime.Scoped, Lifetime<IKaleidoMcpToolHandler>(services));
        Assert.Equal(ServiceLifetime.Scoped, Lifetime<IMcpLookupStateProtector>(services));
        Assert.Equal(ServiceLifetime.Singleton, Lifetime<IToolCatalogCache>(services));
        Assert.Contains(services, x => x.ServiceType == typeof(Microsoft.AspNetCore.DataProtection.IDataProtectionProvider));
    }

    private static ServiceLifetime Lifetime<TService>(ServiceCollection services) =>
        Assert.Single(services, x => x.ServiceType == typeof(TService)).Lifetime;

    [Fact]
    public void AddMcp_ConfiguresTheServer_WithTheOptionsNameVersionAndInstructions()
    {
        var services = HostServices();

        Builder(services).Object.AddMcp(new()
        {
            ServerName = "priorauth",
            ServerVersion = "2.1.0",
            Instructions = "Be careful.",
        });

        var server = ServerOptions(services);

        Assert.Equal("priorauth", server.ServerInfo?.Name);
        Assert.Equal("2.1.0", server.ServerInfo?.Version);
        Assert.Equal("Be careful.", server.ServerInstructions);
        var resolved = Assert.Single(services, x => x.ServiceType == typeof(KaleidoMcpServerInstructions)).ImplementationInstance;
        Assert.Equal("Be careful.", Assert.IsType<KaleidoMcpServerInstructions>(resolved).Text);
        Assert.Equal("Be careful.", RegisteredOptions(services).Instructions);
    }

    // #463: without a ServerVersion the server reports the host application's version, resolved once.
    [Fact]
    public void AddMcp_WithoutAServerVersion_ReportsTheHostApplicationsVersion()
    {
        var services = HostServices();

        Builder(services).Object.AddMcp();

        var expected = KaleidoMcpServerVersion.Resolve(new KaleidoMcpOptions(), System.Reflection.Assembly.GetEntryAssembly()).Value;
        Assert.Equal(expected, ServerOptions(services).ServerInfo?.Version);
        var registered = Assert.Single(services, x => x.ServiceType == typeof(KaleidoMcpServerVersion)).ImplementationInstance;
        Assert.Equal(expected, Assert.IsType<KaleidoMcpServerVersion>(registered).Value);
        Assert.Null(RegisteredOptions(services).ServerVersion);
    }

    // ADR 0030: the Kaleido:Mcp section overrides the options from code, setting by setting; a blank
    // value is an error, never replaced.
    [Fact]
    public void AddMcp_LaysTheKaleidoMcpSectionOverTheOptionsFromCode()
    {
        var services = HostServices();
        var code = new KaleidoMcpOptions { ServerName = "priorauth", MaxElicitationRounds = 2 };

        Builder(services, new Dictionary<string, string?> { ["Kaleido:Mcp:MaxElicitationRounds"] = "4" }).Object.AddMcp(code);

        Assert.Equal(4, RegisteredOptions(services).MaxElicitationRounds);
        Assert.Equal("priorauth", RegisteredOptions(services).ServerName);
        Assert.Equal(2, code.MaxElicitationRounds);
    }

    [Fact]
    public void AddMcp_ABlankServerNameInConfiguration_FailsWithItsCode()
    {
        var builder = Builder(HostServices(), new Dictionary<string, string?> { ["Kaleido:Mcp:ServerName"] = " " });

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.Object.AddMcp());

        Assert.Equal(McpErrorCodes.InvalidOptions, exception.Code);
        Assert.StartsWith("Kaleido:Mcp:ServerName ", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToolsList_IsHandled_ByTheHandlerOfTheHttpRequestsScope()
    {
        var handler = new Mock<IKaleidoMcpToolHandler>();
        var expected = new ListToolsResult { Tools = [new Tool { Name = "clinic_patient" }] };
        handler.Setup(x => x.ListToolsAsync(It.IsAny<RequestContext<ListToolsRequestParams>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var messageScope = new Mock<IServiceProvider>();
        messageScope.Setup(x => x.GetService(typeof(IHttpContextAccessor))).Returns(HttpRequestWith(handler.Object));
        var services = HostServices();
        Builder(services).Object.AddMcp();

        var listTools = ServerOptions(services).Handlers.ListToolsHandler ?? throw new Xunit.Sdk.XunitException("No tools/list handler.");
        var request = new RequestContext<ListToolsRequestParams>(new Mock<McpServer>().Object, new JsonRpcRequest { Method = RequestMethods.ToolsList }, new ListToolsRequestParams())
        {
            Services = messageScope.Object
        };

        var result = await listTools(request, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        handler.Verify(x => x.ListToolsAsync(request, TestContext.Current.CancellationToken), Times.Once);
        messageScope.Verify(x => x.GetService(typeof(IKaleidoMcpToolHandler)), Times.Never);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ToolsCall_OutsideAnHttpRequest_IsHandled_ByTheHandlerOfTheMessagesScope(bool withAccessor)
    {
        var handler = new Mock<IKaleidoMcpToolHandler>();
        var expected = new CallToolResult { Content = [] };
        handler.Setup(x => x.CallToolAsync(It.IsAny<RequestContext<CallToolRequestParams>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var messageScope = new Mock<IServiceProvider>();
        messageScope.Setup(x => x.GetService(typeof(IKaleidoMcpToolHandler))).Returns(handler.Object);

        if (withAccessor)
        {
            messageScope.Setup(x => x.GetService(typeof(IHttpContextAccessor))).Returns(Mock.Of<IHttpContextAccessor>());
        }

        var services = HostServices();
        Builder(services).Object.AddMcp();

        var callTool = ServerOptions(services).Handlers.CallToolHandler ?? throw new Xunit.Sdk.XunitException("No tools/call handler.");
        var request = new RequestContext<CallToolRequestParams>(new Mock<McpServer>().Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, new CallToolRequestParams { Name = "clinic_patient" })
        {
            Services = messageScope.Object
        };

        var result = await callTool(request, TestContext.Current.CancellationToken);

        Assert.Same(expected, result);
        handler.Verify(x => x.CallToolAsync(request, TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task ToolsCall_WithoutAnyServiceProvider_IsAFrameworkError()
    {
        var services = HostServices();
        Builder(services).Object.AddMcp();

        var callTool = ServerOptions(services).Handlers.CallToolHandler ?? throw new Xunit.Sdk.XunitException("No tools/call handler.");
        var request = new RequestContext<CallToolRequestParams>(new Mock<McpServer>().Object, new JsonRpcRequest { Method = RequestMethods.ToolsCall }, new CallToolRequestParams { Name = "clinic_patient" })
        {
            Services = null
        };

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() => callTool(request, TestContext.Current.CancellationToken).AsTask());

        Assert.Equal(FrameworkErrorCodes.MissingRegistration, exception.Code);
    }

    private static IHttpContextAccessor HttpRequestWith(IKaleidoMcpToolHandler handler)
    {
        var requestServices = new Mock<IServiceProvider>();
        requestServices.Setup(x => x.GetService(typeof(IKaleidoMcpToolHandler))).Returns(handler);
        var accessor = new Mock<IHttpContextAccessor>();
        accessor.Setup(x => x.HttpContext).Returns(new DefaultHttpContext { RequestServices = requestServices.Object });
        return accessor.Object;
    }

    private static McpServerOptions ServerOptions(ServiceCollection services)
    {
        services.AddLogging();
        services.AddSingleton(Mock.Of<IHostApplicationLifetime>());
        services.AddSingleton(Mock.Of<IHostEnvironment>());
        services.AddSingleton(Mock.Of<IHttpContextAccessor>());
        services.AddSingleton(Mock.Of<IKaleidoCorrelationContextAccessor>());
        services.AddSingleton(Mock.Of<IKaleidoProcessorClientFactory>());
        services.AddSingleton(Mock.Of<IKaleidoQueryableClientFactory>());
        services.AddSingleton(Mock.Of<IKaleidoProcessForwarding>());

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        return provider.GetRequiredService<IOptions<McpServerOptions>>().Value;
    }
}
