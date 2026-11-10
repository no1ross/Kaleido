using Kaleido.Processor.Context;
using Kaleido.UnitTests;

namespace Kaleido.Processor.Recovery.UnitTests;

public sealed class ProcessorRecoveryStoreTests
    : SutFixture
{
    private static readonly DateTimeOffset Now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private static ProcessorRecoveryStore CreateSut() => new();

    private static PendingOutcome Outcome(Guid? processId = null, DateTimeOffset? recorded = null, DateTimeOffset? next = null) =>
        new()
        {
            Context = new ProcessorContext { ProcessId = processId ?? Guid.NewGuid(), ProcessorName = "intake" },
            StepName = "Close",
            RecordedUtc = recorded ?? Now,
            NextAttemptUtc = next ?? Now,
        };

    [Fact]
    public void TryRecord_KeepsTheOutcome_AndThenItsProcessIsPending()
    {
        var sut = CreateSut();
        var outcome = Outcome();

        Assert.True(sut.TryRecord(outcome));

        Assert.True(sut.IsPending(outcome.Context.ProcessId));
        Assert.False(sut.IsPending(Guid.NewGuid()));
    }

    // One outcome per process: a second one cannot be applied after the first anyway.
    [Fact]
    public void TryRecord_ASecondOutcomeForTheSameProcess_IsNotKept()
    {
        var sut = CreateSut();
        var processId = Guid.NewGuid();
        var first = Outcome(processId);

        sut.TryRecord(first);

        Assert.False(sut.TryRecord(Outcome(processId)));
        Assert.Same(first, Assert.Single(sut.Due(Now)));
    }

    [Fact]
    public void TryRecord_WhenFull_IsNotKept()
    {
        var sut = CreateSut();

        for (var i = 0; i < ProcessorRecoveryStore.Capacity; i++)
        {
            Assert.True(sut.TryRecord(Outcome()));
        }

        Assert.False(sut.TryRecord(Outcome()));
    }

    [Fact]
    public void Due_ReturnsTheOutcomesWhoseAttemptIsDue_OldestFirst()
    {
        var sut = CreateSut();
        var later = Outcome(recorded: Now.AddSeconds(-1));
        var older = Outcome(recorded: Now.AddSeconds(-5));
        var notYet = Outcome(next: Now.AddSeconds(1));
        sut.TryRecord(later);
        sut.TryRecord(notYet);
        sut.TryRecord(older);

        Assert.Equal([older, later], sut.Due(Now));
    }

    [Fact]
    public void Reschedule_ReplacesTheOutcome_AndRemove_ForgetsIt()
    {
        var sut = CreateSut();
        var outcome = Outcome();
        sut.TryRecord(outcome);

        sut.Reschedule(outcome with { Attempts = 1, NextAttemptUtc = Now.AddSeconds(2) });

        Assert.Empty(sut.Due(Now));
        Assert.Equal(1, Assert.Single(sut.Due(Now.AddSeconds(2))).Attempts);

        sut.Remove(outcome.Context.ProcessId);

        Assert.False(sut.IsPending(outcome.Context.ProcessId));
        Assert.Empty(sut.Due(Now.AddHours(1)));
    }

    [Fact]
    public void TryRecord_AndReschedule_RejectANullOutcome()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().TryRecord(null!));
        Assert.Throws<ArgumentNullException>(() => CreateSut().Reschedule(null!));
    }
}
