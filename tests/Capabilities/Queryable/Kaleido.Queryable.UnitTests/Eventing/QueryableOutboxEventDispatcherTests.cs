using Kaleido.Eventing;
using Kaleido.UnitTests;

namespace Kaleido.Queryable.Eventing.UnitTests;

public sealed class QueryableOutboxEventDispatcherTests : SutFixture
{
    private static QueryableOutboxEventDispatcher CreateSut() => new();

    [Fact]
    public void CanHandle_QueryEventType_IsTrue() =>
        Assert.True(CreateSut().CanHandle(QueryableEventTypes.QueryExecuted));

    [Fact]
    public void CanHandle_ProcessEventType_IsFalse() =>
        Assert.False(CreateSut().CanHandle("process.created.v1"));

    [Fact]
    public async Task PublishAsync_RestoresQueryableEvent()
    {
        var envelope = new KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>
        {
            EventType = QueryableEventTypes.QueryExecuted,
            Context = new QueryableEventContext { RequestId = "request", ServiceName = "service", QuerySourceName = "source" },
            Event = new QueryExecuted { OccurredOn = DateTimeOffset.UtcNow, QuerySourceName = "source", IsDirectQuery = true, TotalCount = 1, ReturnedCount = 1, SortCount = 0, FilterProvided = false },
        };
        var publisher = new Mock<IEventPublisher>();

        await CreateSut().PublishAsync(publisher.Object, envelope.ToOutboxEvent(), CancellationToken.None);

        publisher.Verify(x => x.PublishAsync(It.Is<KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>>(e => e.Event.EventId == envelope.Event.EventId), CancellationToken.None), Times.Once);
    }
}
