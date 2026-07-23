using Kaleido.Queryable.Metadata;
using Kaleido.Queryable.Registry;
using Kaleido.Queryable.Validation;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.UnitTests;

public sealed class QueryableServiceCollectionExtensionsTests
{
    [Fact]
    public void AddKaleido_Should_Throw_When_Services_Is_Null()
    {
        // Arrange
        IServiceCollection? services = null;

        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => services!.AddKaleido().AddQueryable(_ => { }));
    }

    [Fact]
    public void AddKaleido_Should_Throw_When_Configure_Is_Null()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act / Assert
        Assert.Throws<ArgumentNullException>(
            () => services.AddKaleido().AddQueryable(null!));
    }

    [Fact]
    public void AddKaleido_Should_Throw_When_No_Assemblies_Are_Configured()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddKaleido().AddQueryable(_ => { }));

        // Assert
        Assert.Equal(
            "At least one assembly must be registered before AddQueryable().",
            exception.Message);
    }

    [Fact]
    public void AddKaleido_Should_Register_Framework_Services()
    {
        // Arrange
        var provider = BuildProvider();

        // Act
        using var scope = provider.CreateScope();

        // Assert
        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<IRecordMetadataCatalog>());

        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<IRecordDescriptorFactory>());

        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<IRecordQueryValidator>());

        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<IRecordQueryCompiler>());

        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<IRecordRegistry>());

        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<IRecordDispatcher>());

        Assert.NotNull(
            scope.ServiceProvider.GetRequiredService<IQueryableCatalog>());
    }

    [Fact]
    public void MetadataCatalog_Should_Be_Singleton()
    {
        // Arrange
        var provider = BuildProvider();

        // Act
        var first =
            provider.GetRequiredService<IRecordMetadataCatalog>();

        var second =
            provider.GetRequiredService<IRecordMetadataCatalog>();

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void DescriptorFactory_Should_Be_Singleton()
    {
        // Arrange
        var provider = BuildProvider();

        // Act
        var first =
            provider.GetRequiredService<IRecordDescriptorFactory>();

        var second =
            provider.GetRequiredService<IRecordDescriptorFactory>();

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void QueryValidator_Should_Be_Singleton()
    {
        // Arrange
        var provider = BuildProvider();

        // Act
        var first =
            provider.GetRequiredService<IRecordQueryValidator>();

        var second =
            provider.GetRequiredService<IRecordQueryValidator>();

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void QueryCompiler_Should_Be_Singleton()
    {
        // Arrange
        var provider = BuildProvider();

        // Act
        var first =
            provider.GetRequiredService<IRecordQueryCompiler>();

        var second =
            provider.GetRequiredService<IRecordQueryCompiler>();

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void RecordRegistry_Should_Be_Singleton()
    {
        // Arrange
        var provider = BuildProvider();

        // Act
        var first =
            provider.GetRequiredService<IRecordRegistry>();

        var second =
            provider.GetRequiredService<IRecordRegistry>();

        // Assert
        Assert.Same(first, second);
    }

    [Fact]
    public void Dispatcher_Should_Be_Scoped()
    {
        // Arrange
        var provider = BuildProvider();

        // Act
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var first =
            scope1.ServiceProvider.GetRequiredService<IRecordDispatcher>();

        var second =
            scope2.ServiceProvider.GetRequiredService<IRecordDispatcher>();

        // Assert
        Assert.NotSame(first, second);
    }

    [Fact]
    public void KaleidoCatalog_Should_Be_Scoped()
    {
        // Arrange
        var provider = BuildProvider();

        // Act
        using var scope1 = provider.CreateScope();
        using var scope2 = provider.CreateScope();

        var first =
            scope1.ServiceProvider.GetRequiredService<IQueryableCatalog>();

        var second =
            scope2.ServiceProvider.GetRequiredService<IQueryableCatalog>();

        // Assert
        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddKaleido_Should_Register_RecordRegistry_With_Discovered_Record()
    {
        // Arrange
        var provider = BuildProvider();

        // Act
        var registry =
            provider.GetRequiredService<IRecordRegistry>();

        var registration =
            registry.GetRegistration("TestRecord");

        // Assert
        Assert.NotNull(registration);
        Assert.Equal(typeof(TestRecord), registration.RecordType);
        Assert.Equal("TestRecord", registration.Name);
    }

    [Fact]
    public void AddKaleido_Should_Register_RecordQueryEngine()
    {
        // Arrange
        var provider = BuildProvider();

        // Act
        using var scope = provider.CreateScope();

        var engine =
            scope.ServiceProvider.GetRequiredService<
                IRecordQueryEngine<TestRecord>>();

        // Assert
        Assert.NotNull(engine);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddKaleido()
            .AddAssembly(typeof(TestRecord).Assembly)   
            .AddQueryable(options =>
        {
            options.ValidateRegistrations = false;
        });

        return services.BuildServiceProvider();
    }
}