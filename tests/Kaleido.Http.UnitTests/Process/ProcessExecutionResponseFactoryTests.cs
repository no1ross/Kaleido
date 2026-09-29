using Kaleido.Http.Process;

namespace Kaleido.Http.UnitTests.Process;

public sealed class ProcessExecutionResponseFactoryTests
    : Kaleido.UnitTests.SutFixture
{
    private static ProcessExecutionResponseFactory CreateSut() =>
        new(Mock.Of<IProcessResponseFactory>());

    [Fact]
    public void Constructor_CreatesInstance()
    {
        Assert.NotNull(CreateSut());
    }
}
