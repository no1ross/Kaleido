namespace Kaleido.Http.Client.UnitTests;

public sealed class KaleidoProcessForwardingTests
    : Kaleido.UnitTests.SutFixture
{
    private static KaleidoProcessForwarding CreateSut() => new();

    [Fact]
    public void OutsideAScope_NoProcessIdIsForwarded()
    {
        var sut = CreateSut();

        Assert.Null(sut.ProcessId);
    }

    [Fact]
    public void InsideAScope_TheProcessIdIsForwarded_UntilDisposed()
    {
        var sut = CreateSut();
        var id = Guid.NewGuid();

        using (sut.ForwardProcess(id))
        {
            Assert.Equal(id, sut.ProcessId);
        }

        Assert.Null(sut.ProcessId);
    }

    [Fact]
    public void NestedScopes_RestoreTheOuterProcessId()
    {
        var sut = CreateSut();
        var outer = Guid.NewGuid();

        using (sut.ForwardProcess(outer))
        {
            using (sut.ForwardProcess(null))
            {
                Assert.Null(sut.ProcessId);
            }

            Assert.Equal(outer, sut.ProcessId);
        }
    }

    [Fact]
    public async Task TheScope_FlowsAcrossAwaits_AndDoesNotLeakIntoOtherFlows()
    {
        var sut = CreateSut();
        var id = Guid.NewGuid();
        var other = Task.Run(async () =>
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
            return sut.ProcessId;
        }, TestContext.Current.CancellationToken);

        using (sut.ForwardProcess(id))
        {
            await Task.Yield();

            Assert.Equal(id, sut.ProcessId);
        }

        Assert.Null(await other);
    }
}
