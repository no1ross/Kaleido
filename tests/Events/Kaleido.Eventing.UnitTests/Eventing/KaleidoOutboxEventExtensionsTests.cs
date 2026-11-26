using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Storage;
using Kaleido.UnitTests;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoOutboxEventExtensionsTests
    : SutFixture
{
    private static ProcessEventContext Context() =>
        new() { RequestId = "req-1", ServiceName = "svc", ProcessId = Guid.NewGuid(), StepName = "step-a" };

    private static KaleidoEventEnvelope<SampleEvent, ProcessEventContext> SampleStepEnvelope() =>
        new()
        {
            EventType = "eventing.step-sample.v1",
            Context = Context(),
            Event = new SampleEvent
            {
                OccurredOn = DateTimeOffset.UtcNow.AddSeconds(-5),
                StepName = "step-a",
                Request = new { MemberId = "M-1" },
            },
        };

    [Fact]
    public void ToOutboxEvent_CarriesTheEventsIdentityAndIsDueAtOnce()
    {
        var envelope = SampleStepEnvelope();

        var entry = envelope.ToOutboxEvent();

        Assert.Equal(envelope.Event.EventId, entry.EventId);
        Assert.Equal("eventing.step-sample.v1", entry.EventType);
        Assert.Equal(envelope.Event.OccurredOn, entry.OccurredUtc);
        Assert.Equal(envelope.Event.OccurredOn, entry.NextAttemptUtc);
        Assert.Equal(0, entry.Attempts);
        Assert.False(entry.IsParked);
    }

    [Fact]
    public async Task PublishAsync_HandsTheRestoredEnvelopeToThePublisher()
    {
        var envelope = SampleStepEnvelope();
        KaleidoEventEnvelope<SampleEvent, ProcessEventContext>? published = null;
        var publisher = new Mock<IEventPublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<KaleidoEventEnvelope<SampleEvent, ProcessEventContext>>(), It.IsAny<CancellationToken>()))
            .Callback<KaleidoEventEnvelope<SampleEvent, ProcessEventContext>, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        await publisher.Object.PublishOutboxEventAsync<SampleEvent, ProcessEventContext>(envelope.ToOutboxEvent(), CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal(envelope.Event.EventId, published.Event.EventId);
        Assert.Equal(envelope.Event.OccurredOn, published.Event.OccurredOn);
        Assert.Equal(envelope.Context, published.Context);
        Assert.Equal("completed", published.Event.Status);

        // object-typed members come back as JSON.
        var request = Assert.IsType<JsonElement>(published.Event.Request);
        Assert.Equal("M-1", request.GetProperty("memberId").GetString());
    }

    public static TheoryData<string> EventTypes() =>
    [
        "eventing.sample-created.v1",
        "eventing.sample-planned.v1",
        "eventing.step-sample.v1",
        "eventing.sample-completed.v1",
        "eventing.query-sample.v1",
    ];

    // A typed envelope for each discriminator, serialized the way the outbox stores it.
    private static KaleidoOutboxEvent EntryOf(string eventType)
    {
        var now = DateTimeOffset.UtcNow;
        if (eventType == "eventing.step-sample.v1")
        {
            return SampleStepEnvelope().ToOutboxEvent();
        }

        if (eventType == "eventing.query-sample.v1")
        {
            return (new KaleidoEventEnvelope<SampleEvent, QueryableEventContext>
            {
                EventType = eventType,
                Context = new QueryableEventContext { RequestId = "req-1", ServiceName = "svc", QuerySourceName = "Members" },
                Event = new SampleEvent { OccurredOn = now, QuerySourceName = "Members", TotalCount = 2 },
            }).ToOutboxEvent();
        }

        return (new KaleidoEventEnvelope<SampleEvent, ProcessEventContext>
        {
            EventType = eventType,
            Context = Context(),
            Event = new SampleEvent { OccurredOn = now },
        }).ToOutboxEvent();
    }

    [Theory]
    [MemberData(nameof(EventTypes))]
    public async Task PublishAsync_RoutesRegisteredDiscriminatorsToDispatcher(string eventType)
    {
        var publisher = new Mock<IEventPublisher>();
        var dispatcher = new Mock<IOutboxEventDispatcher>();
        dispatcher.Setup(x => x.CanHandle(eventType)).Returns(true);
        var entry = EntryOf(eventType);

        await publisher.Object.PublishOutboxEventAsync(entry, [dispatcher.Object], CancellationToken.None);

        dispatcher.Verify(x => x.PublishAsync(publisher.Object, entry, CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_ForAQueryEvent_UsesTheQueryableContext()
    {
        KaleidoEventEnvelope<SampleEvent, QueryableEventContext>? published = null;
        var publisher = new Mock<IEventPublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<KaleidoEventEnvelope<SampleEvent, QueryableEventContext>>(), It.IsAny<CancellationToken>()))
            .Callback<KaleidoEventEnvelope<SampleEvent, QueryableEventContext>, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        await publisher.Object.PublishOutboxEventAsync<SampleEvent, QueryableEventContext>(EntryOf("eventing.query-sample.v1"), CancellationToken.None);

        Assert.Equal("Members", published!.Context.QuerySourceName);
        Assert.Equal(2, published.Event.TotalCount);
    }
    [Fact]
    public async Task PublishAsync_ForAnUnknownEventType_Throws()
    {
        var entry = new KaleidoOutboxEvent { EventId = Guid.NewGuid(), EventType = "something.else.v1", Payload = "{}", OccurredUtc = DateTimeOffset.UtcNow };

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            Mock.Of<IEventPublisher>().PublishOutboxEventAsync(entry, [], CancellationToken.None));

        Assert.Contains("something.else.v1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishAsync_ForAnEmptyPayload_Throws()
    {
        var entry = new KaleidoOutboxEvent { EventId = Guid.NewGuid(), EventType = "eventing.sample-created.v1", Payload = "null", OccurredUtc = DateTimeOffset.UtcNow };

        await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            Mock.Of<IEventPublisher>().PublishOutboxEventAsync<SampleEvent, ProcessEventContext>(entry, CancellationToken.None));
    }
}
