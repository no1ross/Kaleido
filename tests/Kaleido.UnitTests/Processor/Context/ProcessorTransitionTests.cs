using Kaleido.Eventing;
using Kaleido.Exceptions;
using Kaleido.Storage;
using Kaleido.UnitTests;

namespace Kaleido.Processor.Context.UnitTests;

public sealed class ProcessorTransitionTests
    : SutFixture
{
    private static readonly KaleidoOutboxEvent StepCompleted =
        new() { EventId = Guid.NewGuid(), EventType = KaleidoEventTypes.ProcessStepCompleted, Payload = "{}", OccurredUtc = DateTimeOffset.UtcNow };

    private static readonly ProcessorContext Context = new() { ProcessId = Guid.NewGuid(), ProcessorName = "radiology", Version = 3 };

    // Records every call of the transition's collaborators, in order.
    private sealed class Recorder
    {
        public List<string> Calls { get; } = [];

        public Mock<IKaleidoTransactions> Transactions { get; } = new();

        public Mock<IKaleidoTransaction> Transaction { get; } = new();

        public Mock<IKaleidoContextStore> Store { get; } = new();

        public Mock<IKaleidoOutbox> Outbox { get; } = new();

        public Mock<IKaleidoEventWriter> Writer { get; } = new();

        public Recorder()
        {
            Transactions.Setup(x => x.BeginAsync(It.IsAny<CancellationToken>())).Callback(() => Calls.Add("begin")).ReturnsAsync(Transaction.Object);
            Transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => Calls.Add("commit")).Returns(Task.CompletedTask);
            Transaction.Setup(x => x.DisposeAsync()).Callback(() => Calls.Add("dispose")).Returns(ValueTask.CompletedTask);
            Store.Setup(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<CancellationToken>()))
                .Callback(() => Calls.Add("save"))
                .ReturnsAsync((ProcessorContext context, CancellationToken _) => context with { Version = context.Version + 1 });
            Outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).Callback(() => Calls.Add("add")).Returns(Task.CompletedTask);
            Writer.Setup(x => x.CommittedAsync(It.IsAny<CancellationToken>())).Callback(() => Calls.Add("committed")).Returns(Task.CompletedTask);
            Writer.Setup(x => x.RolledBack()).Callback(() => Calls.Add("rolled back"));
        }
    }

    private static ProcessorTransition CreateSut(Recorder recorder) =>
        new(recorder.Transactions.Object, recorder.Store.Object, recorder.Outbox.Object, recorder.Writer.Object);

    [Fact]
    public async Task SaveAsync_WithAnEvent_SavesTheStateAndAddsTheEventInOneTransaction_ThenSignalsCommitted()
    {
        var recorder = new Recorder();

        var saved = await CreateSut(recorder).SaveAsync(Context, StepCompleted, CancellationToken.None);

        Assert.Equal(["begin", "save", "add", "commit", "dispose", "committed"], recorder.Calls);
        Assert.Equal(4, saved.Version);
        recorder.Outbox.Verify(x => x.AddAsync(It.Is<IReadOnlyCollection<KaleidoOutboxEvent>>(e => e.Single() == StepCompleted), It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task SaveAsync_WithoutAnEvent_SavesTheStateInATransaction_AndAddsNothing()
    {
        var recorder = new Recorder();

        await CreateSut(recorder).SaveAsync(Context, transitionEvent: null, CancellationToken.None);

        Assert.Equal(["begin", "save", "commit", "dispose"], recorder.Calls);
    }

    // A conflict leaves nothing behind: no event, no commit, the transaction rolled back.
    [Fact]
    public async Task SaveAsync_WhenTheStateConflicts_AddsNoEvent_AndRollsBack()
    {
        var recorder = new Recorder();
        recorder.Store.Setup(x => x.SaveAsync(It.IsAny<ProcessorContext>(), It.IsAny<CancellationToken>())).ThrowsAsync(new KaleidoConcurrencyException(Context.ProcessId));

        await Assert.ThrowsAsync<KaleidoConcurrencyException>(() => CreateSut(recorder).SaveAsync(Context, StepCompleted, CancellationToken.None));

        Assert.Equal(["begin", "dispose", "rolled back"], recorder.Calls);
    }

    // #451: the commit itself fails - the outbox must forget the event it was given, so a later save in
    // this scope that does commit (the failure save) never delivers it.
    [Fact]
    public async Task SaveAsync_WhenTheCommitFails_TellsTheOutboxTheEventRolledBack()
    {
        var recorder = new Recorder();
        var failure = new IOException("database is locked");
        recorder.Transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).Callback(() => recorder.Calls.Add("commit")).ThrowsAsync(failure);

        var thrown = await Assert.ThrowsAsync<IOException>(() => CreateSut(recorder).SaveAsync(Context, StepCompleted, CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(["begin", "save", "add", "commit", "dispose", "rolled back"], recorder.Calls);
    }

    // Without an event there is nothing to forget.
    [Fact]
    public async Task SaveAsync_WithoutAnEvent_WhenTheCommitFails_SignalsNothing()
    {
        var recorder = new Recorder();
        recorder.Transaction.Setup(x => x.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new IOException("database is locked"));

        await Assert.ThrowsAsync<IOException>(() => CreateSut(recorder).SaveAsync(Context, transitionEvent: null, CancellationToken.None));

        recorder.Writer.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SaveAsync_WhenTheOutboxFails_DoesNotCommit_AndRollsBack()
    {
        var recorder = new Recorder();
        var failure = new InvalidOperationException("disk full");
        recorder.Outbox.Setup(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), It.IsAny<CancellationToken>())).ThrowsAsync(failure);

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateSut(recorder).SaveAsync(Context, StepCompleted, CancellationToken.None));

        Assert.Same(failure, thrown);
        Assert.Equal(["begin", "save", "dispose", "rolled back"], recorder.Calls);
    }

    // The request's token covers the transaction; signalling after the commit is not cancellable.
    [Fact]
    public async Task SaveAsync_PassesTheTokenToTheTransaction_ButNotToTheCommittedSignal()
    {
        var recorder = new Recorder();
        using var cancellation = new CancellationTokenSource();

        await CreateSut(recorder).SaveAsync(Context, StepCompleted, cancellation.Token);

        recorder.Transactions.Verify(x => x.BeginAsync(cancellation.Token));
        recorder.Store.Verify(x => x.SaveAsync(Context, cancellation.Token));
        recorder.Outbox.Verify(x => x.AddAsync(It.IsAny<IReadOnlyCollection<KaleidoOutboxEvent>>(), cancellation.Token));
        recorder.Transaction.Verify(x => x.CommitAsync(cancellation.Token));
        recorder.Writer.Verify(x => x.CommittedAsync(CancellationToken.None));
    }
}
