using Kaleido.Exceptions;
using Kaleido.Process.Execution;
using Kaleido.Queryable.Query;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Hosting.UnitTests;

public sealed class KaleidoHostingServiceCollectionExtensionsTests : SutFixture
{
    private static IKaleidoBuilder NewBuilder(Func<Type, bool> filter, IConfiguration? configuration = null) =>
        new ServiceCollection().AddKaleido(configuration ?? new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "console-test",
            ServiceVersion = "1",
            Assemblies = [typeof(KaleidoHostingServiceCollectionExtensionsTests).Assembly],
            TypeFilter = filter,
        });

    [Fact]
    public void AddHosting_WithNoCapabilities_NeedsNoEventingOrProvider()
    {
        var builder = NewBuilder(_ => false).AddHosting();

        Assert.DoesNotContain(builder.Services, x => x.ServiceType == typeof(KaleidoEventOptions));
        Assert.DoesNotContain(builder.Services, x => x.ServiceType == typeof(IProcessorRuntime));
        Assert.DoesNotContain(builder.Services, x => x.ServiceType == typeof(IQueryableService));
        Assert.Single(builder.Services, x => x.ServiceType == typeof(IHostedService) && x.ImplementationInstance is KaleidoProviderRegistrationCheck);
    }

    [Fact]
    public void AddHosting_WithStepsAndSources_ComposesBothRuntimesAndEventing()
    {
        var builder = NewBuilder(type => type == typeof(TestStep) || type == typeof(TestSource)).AddHosting();

        Assert.Single(builder.Services, x => x.ServiceType == typeof(IProcessorRuntime));
        Assert.Single(builder.Services, x => x.ServiceType == typeof(IQueryableService));
        Assert.Single(builder.Services, x => x.ServiceType == typeof(KaleidoEventOptions));
    }

    [Fact]
    public void AddHosting_ConfiguredServiceRule_IsSnapshotForBothCapabilities()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:DefaultAuthorization:Policy"] = "configured" })
            .Build();
        var roles = new List<string> { "operator" };
        var options = new KaleidoServerOptions { DefaultAuthorization = new AuthorizationMetadata("code", roles) };
        var builder = NewBuilder(type => type == typeof(TestStep) || type == typeof(TestSource), configuration)
            .AddHosting(serverOptions: options);
        roles.Add("later");
        var process = Assert.IsType<KaleidoProcessOptions>(Assert.Single(builder.Services,
            x => x.ServiceType == typeof(KaleidoProcessOptions)).ImplementationInstance).DefaultAuthorization;
        var queryable = Assert.IsType<KaleidoQueryableOptions>(Assert.Single(builder.Services,
            x => x.ServiceType == typeof(KaleidoQueryableOptions)).ImplementationInstance).DefaultAuthorization;
        Assert.Equal("configured", process?.Policy);
        Assert.Equal("configured", queryable?.Policy);
        Assert.Equal(["operator"], process?.Roles);
        Assert.Equal(["operator"], queryable?.Roles);
        Assert.Equal(["operator", "later"], options.DefaultAuthorization.Roles);
    }

    [Fact]
    public void AddHosting_CapabilityRulesOverrideTheServiceDefault()
    {
        var builder = NewBuilder(type => type == typeof(TestStep) || type == typeof(TestSource))
            .AddHosting(
                serverOptions: new() { DefaultAuthorization = new AuthorizationMetadata("service", []) },
                processOptions: new() { DefaultAuthorization = new AuthorizationMetadata("process", []) },
                queryableOptions: new() { DefaultAuthorization = new AuthorizationMetadata("queryable", []) });

        var process = Assert.IsType<KaleidoProcessOptions>(Assert.Single(builder.Services,
            x => x.ServiceType == typeof(KaleidoProcessOptions)).ImplementationInstance);
        var queryable = Assert.IsType<KaleidoQueryableOptions>(Assert.Single(builder.Services,
            x => x.ServiceType == typeof(KaleidoQueryableOptions)).ImplementationInstance);
        Assert.Equal("process", process.DefaultAuthorization?.Policy);
        Assert.Equal("queryable", queryable.DefaultAuthorization?.Policy);
    }

    [Fact]
    public void AddHosting_Twice_RejectsDuplicateRegistration()
    {
        var builder = NewBuilder(_ => false).AddHosting();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.AddHosting());

        Assert.Equal(ConfigurationErrorCodes.DuplicateRegistration, exception.Code);
    }

    [ProcessStep(DisplayName = "Start", Description = "Starts the test Process instance.")]
    public sealed record TestStep : IProcessStep;

    public sealed class TestStepHandler : IProcessStepHandler<TestStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(TestStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    public sealed record TestQueryContext : IQueryContext
    {
        public int Id { get; init; }
    }

    [QuerySource(DisplayName = "Test source", Description = "Queries sample data in this host.")]
    public sealed class TestSource : IQuerySource<TestQueryContext>
    {
        public IQueryable<TestQueryContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<TestQueryContext>().AsQueryable();
    }
}
