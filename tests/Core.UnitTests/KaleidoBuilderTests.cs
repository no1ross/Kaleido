using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace Kaleido.Core.Tests;

public sealed class KaleidoBuilderTests
{
    [Fact]
    public void Constructor_Should_Store_ServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = new KaleidoBuilder(services);

        // Assert
        Assert.Same(
            services,
            builder.Services);
    }

    [Fact]
    public void Constructor_Should_Initialize_Empty_Assembly_Collection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = new KaleidoBuilder(services);

        // Assert
        Assert.Empty(
            builder.Assemblies);
    }

    [Fact]
    public void AddAssembly_Should_Return_True_When_Assembly_Is_New()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new KaleidoBuilder(services);

        var assembly = typeof(KaleidoBuilderTests)
            .Assembly;

        // Act
        var result = builder.AddAssembly(assembly);

        // Assert
        Assert.True(result);

        Assert.Single(
            builder.Assemblies);

        Assert.Contains(
            assembly,
            builder.Assemblies);
    }

    [Fact]
    public void AddAssembly_Should_Return_False_When_Assembly_Already_Exists()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new KaleidoBuilder(services);

        var assembly = typeof(KaleidoBuilderTests)
            .Assembly;

        builder.AddAssembly(assembly);

        // Act
        var result = builder.AddAssembly(assembly);

        // Assert
        Assert.False(result);

        Assert.Single(
            builder.Assemblies);
    }

    [Fact]
    public void Assemblies_Should_Contain_All_Unique_Assemblies()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new KaleidoBuilder(services);

        var assembly1 = typeof(KaleidoBuilderTests)
            .Assembly;

        var assembly2 = typeof(ServiceCollection)
            .Assembly;

        // Act
        builder.AddAssembly(assembly1);
        builder.AddAssembly(assembly2);

        // Assert
        Assert.Equal(
            2,
            builder.Assemblies.Count);

        Assert.Contains(
            assembly1,
            builder.Assemblies);

        Assert.Contains(
            assembly2,
            builder.Assemblies);
    }

    [Fact]
    public void Assemblies_Should_Be_ReadOnly_From_Consumer_Perspective()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new KaleidoBuilder(services);

        var assembly = typeof(KaleidoBuilderTests)
            .Assembly;

        builder.AddAssembly(assembly);

        // Act
        var assemblies = builder.Assemblies;

        // Assert
        Assert.IsAssignableFrom<IReadOnlyCollection<Assembly>>(
            assemblies);

        Assert.Single(assemblies);
    }

    [Fact]
    public void AddAssembly_Should_Allow_Multiple_Unique_Assemblies()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new KaleidoBuilder(services);

        var assemblies =
            AppDomain.CurrentDomain.GetAssemblies()
                .Take(3)
                .ToArray();

        // Act
        foreach (var assembly in assemblies)
        {
            builder.AddAssembly(assembly);
        }

        // Assert
        Assert.Equal(
            assemblies.Length,
            builder.Assemblies.Count);
    }

    [Fact]
    public void AddAssembly_Should_Not_Add_Same_Assembly_Twice()
    {
        // Arrange
        var services = new ServiceCollection();
        var builder = new KaleidoBuilder(services);

        var assembly = typeof(KaleidoBuilderTests).Assembly;

        // Act
        var first = builder.AddAssembly(assembly);
        var second = builder.AddAssembly(assembly);

        // Assert
        Assert.True(first);
        Assert.False(second);

        Assert.Single(builder.Assemblies);
    }
}