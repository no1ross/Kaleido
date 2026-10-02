using Kaleido.Http.Processor;
using Kaleido.Processor.Registry;

namespace Kaleido.Http.UnitTests.Process;

public sealed class ProcessorResponseFactoryTests
    : Kaleido.UnitTests.SutFixture<ProcessorResponseFactory>
{
    protected override ProcessorResponseFactory CreateSut() => new();

    [Fact]
    public void CreateRegistryResponse_WithEmptyRegistration_ReturnsResponse()
    {
        var sut = CreateSut();
        var registration = new ProcessorRegistryItem();
        var options = new KaleidoServiceOptions { ServiceName = "test-svc" };

        var result = sut.CreateRegistryResponse(registration, options);

        Assert.Equal("test-svc", result.ServiceName);
        Assert.Empty(result.Steps ?? []);
    }

}
