using Kaleido.Storage;
using Kaleido.UnitTests;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoStoreOutboxTests
    : SutFixture
{
    private static KaleidoStoreOutbox CreateSut(IKaleidoOutboxStore store, IKaleidoEventSignal signal) => new(store, signal);

    [Fact]
    public async Task AddAsync_AddsTheEventsToTheOutboxStore()
    {
        KaleidoOutboxEvent[] events = [new() { EventId = Guid.NewGuid(), EventType = KaleidoEventTypes.ProcessCreated, Payload = "{}", OccurredUtc = DateTimeOffset.UtcNow }];
        var store = new Mock<IKaleidoOutboxStore>();
        var signal = new Mock<IKaleidoEventSignal>();
        using var cancellation = new CancellationTokenSource();

        await CreateSut(store.Object, signal.Object).AddAsync(events, cancellation.Token);

        store.Verify(x => x.AddAsync(events, cancellation.Token), Times.Once);
        signal.VerifyNoOtherCalls();
    }

    // Committed: the relay may claim them now instead of at its next poll.
    [Fact]
    public async Task CommittedAsync_WakesTheRelay()
    {
        var store = new Mock<IKaleidoOutboxStore>();
        var signal = new Mock<IKaleidoEventSignal>();

        await CreateSut(store.Object, signal.Object).CommittedAsync(TestContext.Current.CancellationToken);

        signal.Verify(x => x.Notify(), Times.Once);
        store.VerifyNoOtherCalls();
    }
}
