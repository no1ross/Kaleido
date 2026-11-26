namespace Kaleido.Http.Client.Calls.UnitTests;

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
            Assert.Equal(id, KaleidoProcessForwarding.CurrentProcessId);
        }

        Assert.Null(sut.ProcessId);
        Assert.Null(KaleidoProcessForwarding.CurrentProcessId);
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
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var other = Task.Run(async () =>
        {
            await gate.Task;
            return sut.ProcessId;
        }, TestContext.Current.CancellationToken);

        using (sut.ForwardProcess(id))
        {
            await Task.Yield();
            gate.SetResult(true);
            Assert.Equal(id, sut.ProcessId);
        }

        Assert.Null(await other);
    }
}
