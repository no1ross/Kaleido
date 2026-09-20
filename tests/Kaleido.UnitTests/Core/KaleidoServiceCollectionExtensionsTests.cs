using Kaleido.Observability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

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
    public void AddKaleido_WithAssembliesInOptions_RegistersAssemblies()
    {
        var services = new ServiceCollection();
        var assembly = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), o =>
        {
            o.ServiceName = "test-service";
            o.Assemblies = new[] { assembly };
        });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Contains(assembly, concreteBuilder.Assemblies);
    }

    [Fact]
    public void AddKaleido_WithMultipleAssembliesInOptions_RegistersAllAssemblies()
    {
        var services = new ServiceCollection();
        var assembly1 = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;
        var assembly2 = typeof(IServiceCollection).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), o =>
        {
            o.ServiceName = "test-service";
            o.Assemblies = new[] { assembly1, assembly2 };
        });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Contains(assembly1, concreteBuilder.Assemblies);
        Assert.Contains(assembly2, concreteBuilder.Assemblies);
    }

    [Fact]
    public void AddKaleido_WithDuplicateAssembliesInOptions_DeduplicatesAssemblies()
    {
        var services = new ServiceCollection();
        var assembly = typeof(KaleidoServiceCollectionExtensionsTests).Assembly;

        var builder = services.AddKaleido(EmptyConfig(), o =>
        {
            o.ServiceName = "test-service";
            o.Assemblies = new[] { assembly, assembly };
        });

        var concreteBuilder = Assert.IsType<KaleidoBuilder>(builder);
        Assert.Equal(1, concreteBuilder.Assemblies.Count(a => a == assembly));
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
