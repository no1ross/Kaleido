using Kaleido.Http.Processor;

namespace Kaleido.Http.UnitTests.Process;

public sealed class ProcessExecutionResponseFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private static ProcessExecutionResponseFactory CreateSut() =>
        new(Mock.Of<IProcessorResponseFactory>());

    [Fact]
    public void Constructor_CreatesInstance()
    {
        Assert.NotNull(CreateSut());
    }
}
