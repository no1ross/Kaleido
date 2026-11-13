using Kaleido.Exceptions;
using Kaleido.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.UnitTests;

public sealed class KaleidoServiceCollectionExtensionsTests
    : SutFixture
{
    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    // Provides valid bootstrap options so unrelated DI tests pass startup validation.
    private static KaleidoServiceOptions DefaultServiceOptions() =>
        new()
        {
            ServiceName = "test-service",
            ServiceVersion = "1",
            Assemblies = [typeof(KaleidoServiceCollectionExtensionsTests).Assembly],
        };

    private static ServiceDescriptor Single<TService>(IServiceCollection services) =>
        Assert.Single(services, x => x.ServiceType == typeof(TService));

    [Fact]
    public void AddKaleido_RegistersConcreteMetadataHelpersAsSingletons()
    {
        var services = new ServiceCollection();
        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        Assert.Equal(ServiceLifetime.Singleton, Single<TypeDescriber>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Singleton, Single<ConstraintMapper>(services).Lifetime);
        using var provider = services.BuildServiceProvider();
        Assert.Same(provider.GetRequiredService<TypeDescriber>(), provider.GetRequiredService<TypeDescriber>());
        Assert.Same(provider.GetRequiredService<ConstraintMapper>(), provider.GetRequiredService<ConstraintMapper>());
    }

    [Fact]
    public void AddKaleido_MissingVersion_ThrowsBeforeChangingServices()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            services.AddKaleido(EmptyConfig(), DefaultServiceOptions() with { ServiceVersion = null! }));

        Assert.Equal(ConfigurationErrorCodes.InvalidServiceVersion, exception.Code);
        Assert.Empty(services);
    }

    [Fact]
    public void AddKaleido_DoesNotBindInternalInstanceIdFromConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:InstanceId"] = Guid.Empty.ToString() })
            .Build();

        var options = new ServiceCollection().AddKaleido(configuration, DefaultServiceOptions()).ServiceOptions;

        Assert.NotEqual(Guid.Empty, options.InstanceId);
    }

    // ADR 0030: configuration overrides code setting by setting; what configuration does not name keeps
    // the code value, and the host's own instance is never changed.
    [Fact]
    public void AddKaleido_LaysConfigurationOverTheOptionsFromCode_WithoutChangingThem()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:DisplayName"] = "From configuration",
                ["Kaleido:ServiceName"] = "configured-service",
                ["Kaleido:ServiceVersion"] = "2",
            })
            .Build();
        var code = DefaultServiceOptions() with
        {
            DisplayName = "From code",
            Description = "Kept",
        };

        var registered = new ServiceCollection().AddKaleido(config, code).ServiceOptions;

        Assert.Equal("From configuration", registered.DisplayName);
        Assert.Equal("configured-service", registered.ServiceName);
        Assert.Equal("2", registered.ServiceVersion);
        Assert.Equal("Kept", registered.Description);
        Assert.Equal("From code", code.DisplayName);
        Assert.Equal("test-service", code.ServiceName);
        Assert.Equal("1", code.ServiceVersion);
    }

    // The registered options hold their own copy: changing the caller's array afterwards
    // changes nothing.
    [Fact]
    public void AddKaleido_RegistersACopyOfTheAssemblies()
    {
        var assemblies = new[] { typeof(KaleidoServiceOptions).Assembly };

        var registered = new ServiceCollection()
            .AddKaleido(EmptyConfig(), DefaultServiceOptions() with { Assemblies = assemblies })
            .ServiceOptions;
        assemblies[0] = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        Assert.Equal([typeof(KaleidoServiceOptions).Assembly], registered.Assemblies);
        Assert.Throws<NotSupportedException>(() => ((IList<System.Reflection.Assembly>)registered.Assemblies!)[0] = assemblies[0]);
    }

    // #301: one Kaleido service per host; a second call would silently replace the first one's options.
    [Fact]
    public void AddKaleido_CalledTwice_IsAConfigurationError()
    {
        var services = new ServiceCollection();
        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        var exception = Assert.Throws<KaleidoConfigurationException>(() => services.AddKaleido(EmptyConfig(), DefaultServiceOptions()));

        Assert.Equal(ConfigurationErrorCodes.DuplicateRegistration, exception.Code);
    }

    [Fact]
    public void AddKaleido_ShouldThrow_WhenServicesIsNull()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(
            () => services!.AddKaleido(EmptyConfig()));
    }

    [Fact]
    public void AddKaleido_ShouldThrow_WhenConfigurationIsNull()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(
            () => services.AddKaleido(null!));
    }

    [Fact]
    public void AddKaleido_ShouldReturn_KaleidoBuilder()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        Assert.NotNull(builder);

        var concreteBuilder =
            Assert.IsType<KaleidoBuilder>(builder);

        Assert.Same(
            services,
            concreteBuilder.Services);
    }

    [Fact]
    public void AddKaleido_WithAssembliesInOptions_RegistersAssemblies()
    {
        var services = new ServiceCollection();
        var assembly = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", ServiceVersion = "1", Assemblies = new[] { assembly } });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Same(assembly, Assert.Single(concreteBuilder.Assemblies));
    }

    [Fact]
    public void AddKaleido_WithMultipleAssembliesInOptions_RegistersAllAssemblies()
    {
        var services = new ServiceCollection();
        var assembly1 = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;
        var assembly2 = typeof(IServiceCollection).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", ServiceVersion = "1", Assemblies = new[] { assembly1, assembly2 } });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Contains(assembly1, concreteBuilder.Assemblies);
        Assert.Contains(assembly2, concreteBuilder.Assemblies);
    }

    [Fact]
    public void AddKaleido_WithDuplicateAssembliesInOptions_DeduplicatesAssemblies()
    {
        var services = new ServiceCollection();
        var assembly = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", ServiceVersion = "1", Assemblies = new[] { assembly, assembly } });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Equal(1, concreteBuilder.Assemblies.Count(a => a == assembly));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AddKaleido_WhenAssembliesAreMissingOrEmpty_FailsBeforeRegistration(bool empty)
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", ServiceVersion = "1", Assemblies = empty ? [] : null }));

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, exception.Code);
        Assert.Empty(services);
    }

    [Fact]
    public void AddKaleido_ShouldNotOverride_PreregisteredCorrelationContextAccessor()
    {
        var services = new ServiceCollection();
        var custom = new CustomCorrelationContextAccessor();
        services.AddScoped<IKaleidoCorrelationContextAccessor>(_ => custom);

        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextAccessor>();
        Assert.Same(custom, resolved);
    }

    [Fact]
    public void AddKaleido_ShouldNotOverride_PreregisteredCorrelationContextInitializer()
    {
        var services = new ServiceCollection();
        var custom = new CustomCorrelationContextInitializer();
        services.AddScoped<IKaleidoCorrelationContextInitializer>(_ => custom);

        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextInitializer>();
        Assert.Same(custom, resolved);
    }

    [Fact]
    public void AddKaleido_ShouldRegisterDefaultAccessor_WhenNonePreregistered()
    {
        var services = new ServiceCollection();
        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextAccessor>();
        Assert.NotNull(resolved);
    }

    [Fact]
    public void AddKaleido_ShouldRegisterDefaultInitializer_WhenNonePreregistered()
    {
        var services = new ServiceCollection();
        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextInitializer>();
        Assert.NotNull(resolved);
    }

    [Fact]
    public void AddKaleido_WithoutConfigureCallback_UsesBoundConfigurationAndValidatesIt()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:ServiceName"] = "bound-service",
                ["Kaleido:ServiceVersion"] = "1",
            })
            .Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() => services.AddKaleido(config));

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, exception.Code);
    }

    // ---------------------------------------------------------------------------
    // Test doubles

    private sealed class CustomCorrelationContextAccessor : IKaleidoCorrelationContextAccessor
    {
        public KaleidoCorrelationContext Current => new();
    }

    private sealed class CustomCorrelationContextInitializer : IKaleidoCorrelationContextInitializer
    {
        public void Initialize(KaleidoCorrelationContext context) { }
    }

}
