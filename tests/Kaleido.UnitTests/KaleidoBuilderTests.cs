using Kaleido.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.UnitTests;

public sealed class KaleidoBuilderTests
    : SutFixture
{
    private static KaleidoBuilder CreateSut(params System.Reflection.Assembly[]? assemblies) =>
        CreateSut(new ServiceCollection(), new ConfigurationBuilder().Build(), new KaleidoServiceOptions { ServiceName = "test", Assemblies = assemblies });

    private static KaleidoBuilder CreateSut(IServiceCollection services, IConfiguration configuration, KaleidoServiceOptions options) =>
        new(services, configuration, options);

    [Fact]
    public void TheConfiguredAssemblies_AreTheScanSet_EachOnce()
    {
        var sut = CreateSut(typeof(KaleidoBuilderTests).Assembly, typeof(KaleidoBuilder).Assembly, typeof(KaleidoBuilderTests).Assembly);

        Assert.Equal(2, sut.Assemblies.Count);
        Assert.Contains(typeof(KaleidoBuilderTests).Assembly, sut.Assemblies);
        Assert.Contains(typeof(KaleidoBuilder).Assembly, sut.Assemblies);
    }

    [Fact]
    public void AddAssembly_AnAssemblyAlreadyInTheSet_IsNotAddedAgain()
    {
        var sut = CreateSut(typeof(KaleidoBuilderTests).Assembly);

        Assert.False(sut.AddAssembly(typeof(KaleidoBuilderTests).Assembly));
        Assert.True(sut.AddAssembly(typeof(KaleidoBuilder).Assembly));
        Assert.Equal(2, sut.Assemblies.Count);
    }

    [Fact]
    public void WithoutAssemblies_IsAConfigurationError()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => CreateSut(null));

        Assert.Equal(ConfigurationErrorCodes.MissingAssembly, exception.Code);
    }

    [Fact]
    public void ExposesTheServicesConfigurationAndOptionsItWasGiven()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();
        var options = new KaleidoServiceOptions { ServiceName = "test", Assemblies = [typeof(KaleidoBuilderTests).Assembly] };

        var sut = CreateSut(services, configuration, options);

        Assert.Same(services, sut.Services);
        Assert.Same(configuration, sut.Configuration);
        Assert.Same(options, sut.ServiceOptions);
    }
}
