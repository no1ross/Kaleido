using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using Xunit;

namespace Kaleido.UnitTests;

public sealed class KaleidoBuilderTests
{
    private static IConfiguration EmptyConfig() =>
        new ConfigurationBuilder().Build();

    private static KaleidoServiceOptions DefaultServiceOptions() =>
        new() { ServiceName = "test" };

    [Fact]
    public void Constructor_Should_Store_ServiceCollection()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = new KaleidoBuilder(services, EmptyConfig(), DefaultServiceOptions());

        // Assert
        Assert.Same(
            services,
            builder.Services);
    }

    [Fact]
    public void Constructor_Should_Initialize_Assembly_Collection_From_Defaults()
    {
        // Arrange
        var services = new ServiceCollection();

        // Act
        var builder = new KaleidoBuilder(services, EmptyConfig(), DefaultServiceOptions());

        // Assert
        // Constructor now defaults to calling and entry assemblies
        Assert.NotEmpty(
            builder.Assemblies);
    }
}
