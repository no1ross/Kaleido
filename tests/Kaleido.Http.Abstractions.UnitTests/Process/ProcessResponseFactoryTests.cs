using Kaleido.Http.Process.Services;
using Kaleido.Process.Registry;

namespace Kaleido.Http.Abstractions.UnitTests.Process;

public sealed class ProcessResponseFactoryTests
{
    private static ProcessResponseFactory CreateSut() => new();

    [Fact]
    public void CreateRegistryResponse_WithEmptyRegistration_ReturnsResponse()
    {
        var sut = CreateSut();
        var registration = new ProcessorRegistryItem();
        var options = new KaleidoServiceOptions { ServiceName = "test-svc" };

        var result = sut.CreateRegistryResponse(registration, options);

        Assert.Equal("test-svc", result.ServiceName);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void CreateCatalogResponse_WithEmptyRegistration_ReturnsResponse()
    {
        var sut = CreateSut();
        var registration = new ProcessorRegistryItem();
        var options = new KaleidoServiceOptions { ServiceName = "test-svc" };

        var result = sut.CreateCatalogResponse(registration, options);

        Assert.Equal("test-svc", result.ServiceName);
    }
}
