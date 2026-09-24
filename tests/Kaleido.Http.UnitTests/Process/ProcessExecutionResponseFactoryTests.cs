using Kaleido.Http.Process.Services;

namespace Kaleido.Http.UnitTests.Process;

public sealed class ProcessExecutionResponseFactoryTests
{
    private static ProcessExecutionResponseFactory CreateSut() =>
        new(Mock.Of<IProcessResponseFactory>());

    [Fact]
    public void Constructor_CreatesInstance()
    {
        Assert.NotNull(CreateSut());
    }
}
