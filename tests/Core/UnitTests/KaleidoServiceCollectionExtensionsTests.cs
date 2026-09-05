using Kaleido.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.UnitTests;

public sealed class KaleidoServiceCollectionExtensionsTests
{
    [Fact]
    public void AddKaleido_ShouldThrow_WhenServicesIsNull()
    {
        IServiceCollection? services = null;

        Assert.Throws<ArgumentNullException>(
            () => services!.AddKaleido());
    }

    [Fact]
    public void AddKaleido_ShouldReturn_KaleidoBuilder()
    {
        var services = new ServiceCollection();

        var builder =
            services.AddKaleido();

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
                .AddKaleido();

        Assert.Throws<ArgumentNullException>(
            () => builder.AddAssembly(null!));
    }

    [Fact]
    public void AddAssembly_ShouldReturn_SameBuilder()
    {
        var builder =
            new ServiceCollection()
                .AddKaleido();

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
                .AddKaleido();

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
                .AddKaleido();

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
                .AddKaleido();

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

        services.AddKaleido();

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

        services.AddKaleido();

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextInitializer>();
        Assert.Same(custom, resolved);
    }

    [Fact]
    public void AddKaleido_ShouldRegisterDefaultAccessor_WhenNonePreregistered()
    {
        var services = new ServiceCollection();
        services.AddKaleido();

        using var provider = services.BuildServiceProvider(validateScopes: false);
        using var scope = provider.CreateScope();

        var resolved = scope.ServiceProvider.GetRequiredService<IKaleidoCorrelationContextAccessor>();
        Assert.NotNull(resolved);
    }

    [Fact]
    public void AddKaleido_ShouldRegisterDefaultInitializer_WhenNonePreregistered()
    {
        var services = new ServiceCollection();
        services.AddKaleido();

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