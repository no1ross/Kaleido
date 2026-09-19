using Kaleido.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.UnitTests;

public sealed class KaleidoServiceCollectionExtensionsTests
{
    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    // Provides a valid ServiceName so tests not concerned with service identity
    // still pass the startup validation check.
    private static Action<KaleidoServiceOptions> DefaultServiceName() =>
        o => o.ServiceName = "test-service";

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
            services.AddKaleido(EmptyConfig(), DefaultServiceName());

        Assert.NotNull(builder);

        var concreteBuilder =
            Assert.IsType<KaleidoBuilder>(builder);

        Assert.Same(
            services,
            concreteBuilder.Services);
    }

    [Fact]
    public void AddAssembly_ShouldThrow_WhenBuilderIsNull()
    {
        IKaleidoBuilder? builder = null;

        Assert.Throws<ArgumentNullException>(
            () => builder!.AddAssembly(
                typeof(KaleidoServiceCollectionExtensionsTests).Assembly));
    }

    [Fact]
    public void AddAssembly_ShouldThrow_WhenAssemblyIsNull()
    {
        var builder =
            new ServiceCollection()
                .AddKaleido(EmptyConfig(), DefaultServiceName());

        Assert.Throws<ArgumentNullException>(
            () => builder.AddAssembly(null!));
    }

    [Fact]
    public void AddAssembly_ShouldReturn_SameBuilder()
    {
        var builder =
            new ServiceCollection()
                .AddKaleido(EmptyConfig(), DefaultServiceName());

        var result =
            builder.AddAssembly(
                typeof(KaleidoServiceCollectionExtensionsTests).Assembly);

        Assert.Same(
            builder,
            result);
    }

    [Fact]
    public void AddAssembly_ShouldAdd_Assembly()
    {
        var builder =
            new ServiceCollection()
                .AddKaleido(EmptyConfig(), DefaultServiceName());

        var assembly =
            typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        builder.AddAssembly(assembly);

        var concreteBuilder =
            Assert.IsType<KaleidoBuilder>(builder);

        var registeredAssembly =
            Assert.Single(concreteBuilder.Assemblies);

        Assert.Same(
            assembly,
            registeredAssembly);
    }

    [Fact]
    public void AddAssembly_ShouldNotAdd_DuplicateAssembly()
    {
        var builder =
            new ServiceCollection()
                .AddKaleido(EmptyConfig(), DefaultServiceName());

        var assembly =
            typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        builder.AddAssembly(assembly);

        builder.AddAssembly(assembly);

        var concreteBuilder =
            Assert.IsType<KaleidoBuilder>(builder);

        Assert.Single(
            concreteBuilder.Assemblies);
    }

    [Fact]
    public void AddAssembly_ShouldAdd_MultipleAssemblies()
    {
        var builder =
            new ServiceCollection()
                .AddKaleido(EmptyConfig(), DefaultServiceName());

        builder.AddAssembly(
            typeof(KaleidoServiceCollectionExtensionsTests).Assembly);

        builder.AddAssembly(
            typeof(IServiceCollection).Assembly);

        var concreteBuilder =
            Assert.IsType<KaleidoBuilder>(builder);

        Assert.Equal(
            2,
            concreteBuilder.Assemblies.Count);
    }

    [Fact]
    public void AddKaleido_ShouldNotOverride_PreregisteredCorrelationContextAccessor()
    {
        var services = new ServiceCollection();
        var custom = new CustomCorrelationContextAccessor();
        services.AddScoped<IKaleidoCorrelationContextAccessor>(_ => custom);

        services.AddKaleido(EmptyConfig(), DefaultServiceName());

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

        services.AddKaleido(EmptyConfig(), DefaultServiceName());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextInitializer>();
        Assert.Same(custom, resolved);
    }

    [Fact]
    public void AddKaleido_ShouldRegisterDefaultAccessor_WhenNonePreregistered()
    {
        var services = new ServiceCollection();
        services.AddKaleido(EmptyConfig(), DefaultServiceName());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextAccessor>();
        Assert.NotNull(resolved);
    }

    [Fact]
    public void AddKaleido_ShouldRegisterDefaultInitializer_WhenNonePreregistered()
    {
        var services = new ServiceCollection();
        services.AddKaleido(EmptyConfig(), DefaultServiceName());

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextInitializer>();
        Assert.NotNull(resolved);
    }

    // ---------------------------------------------------------------------------
    // Test doubles
    // ---------------------------------------------------------------------------

    private sealed class CustomCorrelationContextAccessor : IKaleidoCorrelationContextAccessor
    {
        public KaleidoCorrelationContext Current => new();
    }

    private sealed class CustomCorrelationContextInitializer : IKaleidoCorrelationContextInitializer
    {
        public void Initialize(KaleidoCorrelationContext context) { }
    }
}
