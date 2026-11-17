using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Queryable.Eventing;
using Kaleido.Queryable.Registry;
using Kaleido.Registry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Queryable.UnitTests;

public sealed class QueryableServiceCollectionExtensionsTests : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void AddQueryableEventDispatch_RegistersWithoutQueryableRuntime()
    {
        var services = new ServiceCollection();
        var builder = new Mock<IKaleidoBuilder>();
        builder.SetupGet(x => x.Services).Returns(services);

        builder.Object.AddQueryableEventDispatch();
        builder.Object.AddQueryableEventDispatch();

        Assert.Single(services, x => x.ServiceType == typeof(IOutboxEventDispatcher) && x.ImplementationType == typeof(QueryableOutboxEventDispatcher));
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IQueryableRegistry));
    }

    [Fact]
    public void AddQueryable_WhenBuilderIsNull_Throws()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(() => builder!.AddQueryable());
    }

    [Fact]
    public void AddQueryable_WithoutAssemblies_ThrowsMissingAssembly()
    {
        var builder = new Mock<IKaleidoBuilder>();
        builder.SetupGet(x => x.Assemblies).Returns([]);
        builder.SetupGet(x => x.Configuration).Returns(new ConfigurationBuilder().Build());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => builder.Object.AddQueryable());

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, exception.Code);
    }

    [Fact]
    public void AddQueryable_WithoutCapabilities_RegistersNothing()
    {
        var services = new ServiceCollection();
        var builder = new Mock<IKaleidoBuilder>();
        builder.SetupGet(x => x.Assemblies).Returns([typeof(QueryableServiceCollectionExtensionsTests).Assembly]);
        builder.SetupGet(x => x.Configuration).Returns(new ConfigurationBuilder().Build());
        builder.SetupGet(x => x.Services).Returns(services);
        builder.SetupGet(x => x.ServiceOptions).Returns(new KaleidoServiceOptions { ServiceName = "svc", ServiceVersion = "1", TypeFilter = _ => false });

        var result = builder.Object.AddQueryable();

        Assert.Same(builder.Object, result);
        Assert.Empty(services);
    }

    [Fact]
    public void AddQueryable_RegistersRegistriesBuiltFromTheScannedCapabilities()
    {
        var sourceType = typeof(Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeSource);
        var viewType = typeof(Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeLabelView);
        var services = new ServiceCollection();
        services.AddKaleido(
            new ConfigurationBuilder().Build(),
            new()
            {
                ServiceName = "test-service",
                ServiceVersion = "1",
                Assemblies = [typeof(QueryableServiceCollectionExtensionsTests).Assembly],
                TypeFilter = type => type == sourceType || type == viewType,
            }).AddQueryable();

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });

        var sources = provider.GetRequiredService<IQuerySourceRegistry>();
        var views = provider.GetRequiredService<IQueryViewRegistry>();

        Assert.Equal(sourceType, sources.GetRegistration(sourceType).SourceType);
        Assert.Equal(viewType, views.Find(viewType)?.QueryViewType);
        Assert.Same(
            provider.GetRequiredService<QuerySourceRegistry>(),
            sources);
        Assert.Same(
            provider.GetRequiredService<QueryViewRegistry>(),
            views);
        var local = Assert.Single(provider.GetServices<IKaleidoLocalCapabilities>());
        Assert.Same(provider.GetRequiredService<IQueryableRegistry>(), local);
        Assert.Equal("test-service", Assert.Single(local.Capabilities().Queryables).ServiceName);
    }

    [Fact]
    public void AddQueryable_WithoutDefault_UsesUnspecifiedAuthorization()
    {
        var services = NewSourceBuilder().AddQueryable().Services;
        using var provider = services.BuildServiceProvider();

        var registration = provider.GetRequiredService<IQuerySourceRegistry>()
            .GetRegistration(typeof(Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeSource));

        Assert.Same(AuthorizationMetadata.Unspecified, registration.Metadata.Authorization);
        Assert.Null(provider.GetRequiredService<KaleidoQueryableOptions>().DefaultAuthorization);
    }

    [Fact]
    public void AddQueryable_ConfigOverridesCodeDefaultWithoutMutatingIt_AndPreservesAnExplicitSourceRule()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:Queryable:DefaultAuthorization:Policy"] = "configured-policy",
                ["Kaleido:Queryable:DefaultAuthorization:Roles:0"] = "radiology",
            })
            .Build();
        var roles = new List<string> { "code-role" };
        var code = new KaleidoQueryableOptions { DefaultAuthorization = new AuthorizationMetadata("code-policy", roles) };
        var services = NewSourceBuilder(configuration).AddQueryable(code).Services;
        roles.Add("later");
        using var provider = services.BuildServiceProvider();

        var effective = provider.GetRequiredService<KaleidoQueryableOptions>();
        var sources = provider.GetRequiredService<IQuerySourceRegistry>();
        Assert.Equal("configured-policy", effective.DefaultAuthorization?.Policy);
        Assert.Equal(["radiology"], effective.DefaultAuthorization?.Roles);
        Assert.Equal("code-policy", code.DefaultAuthorization.Policy);
        Assert.Equal(["code-role", "later"], code.DefaultAuthorization.Roles);
        Assert.Equal("configured-policy", sources.GetRegistration(typeof(Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeSource)).Metadata.Authorization.Policy);
        Assert.Equal("explicit-policy", sources.GetRegistration(typeof(SecuredSource)).Metadata.Authorization.Policy);
    }

    [Fact]
    public void AddQueryable_CodeDefault_SnapshotsMutableRoles()
    {
        var roles = new List<string> { "reader" };
        var code = new KaleidoQueryableOptions { DefaultAuthorization = new AuthorizationMetadata("staff", roles) };
        var services = NewSourceBuilder().AddQueryable(code).Services;
        roles.Add("later");
        using var provider = services.BuildServiceProvider();

        var effective = provider.GetRequiredService<KaleidoQueryableOptions>();
        var registration = provider.GetRequiredService<IQuerySourceRegistry>()
            .GetRegistration(typeof(Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeSource));
        Assert.Equal(["reader"], effective.DefaultAuthorization?.Roles);
        Assert.Equal(["reader"], registration.Metadata.Authorization.Roles);
        Assert.Equal(["reader", "later"], code.DefaultAuthorization?.Roles);
    }

    [Fact]
    public void AddQueryable_BindsConfiguredDefaultWithoutCodeOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:Queryable:DefaultAuthorization:Policy"] = "configured-policy",
                ["Kaleido:Queryable:DefaultAuthorization:Roles:0"] = "reader",
            })
            .Build();
        var services = NewSourceBuilder(configuration).AddQueryable().Services;
        using var provider = services.BuildServiceProvider();

        Assert.Equal("configured-policy", provider.GetRequiredService<KaleidoQueryableOptions>().DefaultAuthorization?.Policy);
        Assert.Equal(["reader"], provider.GetRequiredService<KaleidoQueryableOptions>().DefaultAuthorization?.Roles);
    }

    [Fact]
    public void AddQueryable_ConfiguredPolicyWithoutRoles_UsesEmptyRoles()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:Queryable:DefaultAuthorization:Policy"] = "configured-policy",
            })
            .Build();
        var services = NewSourceBuilder(configuration).AddQueryable().Services;
        using var provider = services.BuildServiceProvider();

        var rule = provider.GetRequiredService<KaleidoQueryableOptions>().DefaultAuthorization;
        Assert.Equal("configured-policy", rule?.Policy);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<string>>(rule?.Roles));
    }

    [Fact]
    public void AddQueryable_RejectsConflictingConfiguredDefault()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:Queryable:DefaultAuthorization:AllowAnonymous"] = "true",
                ["Kaleido:Queryable:DefaultAuthorization:Roles:0"] = "reader",
            })
            .Build();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => NewSourceBuilder(configuration).AddQueryable());

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }

    private static IKaleidoBuilder NewSourceBuilder(IConfiguration? configuration = null)
    {
        var source = typeof(Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeSource);
        return new ServiceCollection().AddKaleido(configuration ?? new ConfigurationBuilder().Build(), new()
        {
            ServiceName = "test-service",
            ServiceVersion = "1",
            Assemblies = [typeof(QueryableServiceCollectionExtensionsTests).Assembly],
            TypeFilter = type => type == source || type == typeof(SecuredSource),
        });
    }

    [QuerySource(DisplayName = "Secured", Description = "Source with an explicit authorization rule.")]
    [KaleidoAuthorization(Policy = "explicit-policy")]
    public sealed class SecuredSource : IQuerySource<Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeContext>
    {
        public IQueryable<Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeContext> CreateQuery(QueryExecutionContext executionContext) =>
            Array.Empty<Kaleido.Queryable.Query.UnitTests.QueryContextEngineTests.FakeContext>().AsQueryable();
    }
}
