using Kaleido.Exceptions;
using Kaleido.Observability;
using Kaleido.Registry;
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
            Assemblies = [typeof(KaleidoServiceCollectionExtensionsTests).Assembly],
        };

    private static ServiceDescriptor Single<TService>(IServiceCollection services) =>
        Assert.Single(services, x => x.ServiceType == typeof(TService));

    // ADR 0030: configuration overrides code setting by setting; what configuration does not name keeps
    // the code value, and the host's own instance is never changed.
    [Fact]
    public void AddKaleido_LaysConfigurationOverTheOptionsFromCode_WithoutChangingThem()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:DisplayName"] = "From configuration",
                ["Kaleido:DefaultAuthorization:Roles:0"] = "radiology",
            })
            .Build();
        var code = DefaultServiceOptions() with
        {
            DisplayName = "From code",
            Description = "Kept",
            DefaultAuthorization = new AuthorizationMetadata("staff", ["admin"]),
        };

        var registered = new ServiceCollection().AddKaleido(config, code).ServiceOptions;

        Assert.Equal("From configuration", registered.DisplayName);
        Assert.Equal("Kept", registered.Description);
        Assert.Equal("staff", registered.DefaultAuthorization.Policy);
        Assert.Equal(["radiology"], registered.DefaultAuthorization.Roles); // replaced, not appended
        Assert.Equal("From code", code.DisplayName);
        Assert.Equal(["admin"], code.DefaultAuthorization.Roles);
    }

    // The registered options hold their own copies: changing the caller's array or list afterwards
    // changes nothing.
    [Fact]
    public void AddKaleido_RegistersCopiesOfTheAssembliesAndRoles()
    {
        var assemblies = new[] { typeof(KaleidoServiceOptions).Assembly };
        var roles = new List<string> { "admin" };

        var registered = new ServiceCollection()
            .AddKaleido(EmptyConfig(), DefaultServiceOptions() with { Assemblies = assemblies, DefaultAuthorization = new AuthorizationMetadata(null, roles) })
            .ServiceOptions;
        assemblies[0] = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;
        roles.Add("other");

        Assert.Equal([typeof(KaleidoServiceOptions).Assembly], registered.Assemblies);
        Assert.Equal(["admin"], registered.DefaultAuthorization.Roles);
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

    // #288: a host that does not choose gets Authenticated; leaving capabilities open is an explicit None.
    [Fact]
    public void AddKaleido_WithoutAnAuthorizationMode_EnforcesAuthenticatedCallers()
    {
        var services = new ServiceCollection();

        services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        var options = Assert.IsType<KaleidoServiceOptions>(Single<KaleidoServiceOptions>(services).ImplementationInstance);
        Assert.Equal(KaleidoAuthorizationMode.Authenticated, options.AuthorizationMode);
    }

    [Fact]
    public void AddKaleido_WithAuthorizationModeNoneInConfiguration_LeavesCapabilitiesOpen()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:AuthorizationMode"] = "None" })
            .Build();

        services.AddKaleido(configuration, DefaultServiceOptions());

        var options = Assert.IsType<KaleidoServiceOptions>(Single<KaleidoServiceOptions>(services).ImplementationInstance);
        Assert.Equal(KaleidoAuthorizationMode.None, options.AuthorizationMode);
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

        var builder = services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", Assemblies = new[] { assembly } });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Same(assembly, Assert.Single(concreteBuilder.Assemblies));
    }

    [Fact]
    public void AddKaleido_WithMultipleAssembliesInOptions_RegistersAllAssemblies()
    {
        var services = new ServiceCollection();
        var assembly1 = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;
        var assembly2 = typeof(IServiceCollection).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", Assemblies = new[] { assembly1, assembly2 } });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Contains(assembly1, concreteBuilder.Assemblies);
        Assert.Contains(assembly2, concreteBuilder.Assemblies);
    }

    [Fact]
    public void AddKaleido_WithDuplicateAssembliesInOptions_DeduplicatesAssemblies()
    {
        var services = new ServiceCollection();
        var assembly = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", Assemblies = new[] { assembly, assembly } });

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
            services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", Assemblies = empty ? [] : null }));

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, exception.Code);
        Assert.Empty(services);
    }

    [Fact]
    public void AddKaleido_NoServiceAuthorizationRule_UsesUnspecified()
    {
        var services = new ServiceCollection();

        var builder = services.AddKaleido(EmptyConfig(), DefaultServiceOptions());

        Assert.False(builder.ServiceOptions.DefaultAuthorization.IsExplicit());
        Assert.Empty(builder.ServiceOptions.DefaultAuthorization.Roles);
    }

    [Fact]
    public void AddKaleido_BindsServiceAuthorizationRuleFromConfiguration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kaleido:DefaultAuthorization:Policy"] = "staff",
                ["Kaleido:DefaultAuthorization:Roles:0"] = "radiology"
            })
            .Build();
        var services = new ServiceCollection();

        var builder = services.AddKaleido(config, DefaultServiceOptions());

        Assert.Equal("staff", builder.ServiceOptions.DefaultAuthorization.Policy);
        Assert.Equal("radiology", Assert.Single(builder.ServiceOptions.DefaultAuthorization.Roles));
        Assert.False(AuthorizationMetadata.Unspecified.IsExplicit());
    }

    [Fact]
    public void AddKaleido_RejectsConflictingServiceAuthorizationRule()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            services.AddKaleido(EmptyConfig(), new()
            {
                ServiceName = "test-service",
                DefaultAuthorization = new AuthorizationMetadata("staff", []) { AllowAnonymous = true },
            }));

        Assert.Equal(ConfigurationErrorCodes.ConflictingAuthorization, exception.Code);
    }

    [Fact]
    public void AddKaleido_RejectsUnknownAuthorizationMode()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<KaleidoConfigurationException>(() =>
            services.AddKaleido(EmptyConfig(), new() { ServiceName = "test-service", AuthorizationMode = (KaleidoAuthorizationMode)123 }));

        Assert.Equal(ConfigurationErrorCodes.InvalidAuthorizationMode, exception.Code);
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
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Kaleido:ServiceName"] = "bound-service" })
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
