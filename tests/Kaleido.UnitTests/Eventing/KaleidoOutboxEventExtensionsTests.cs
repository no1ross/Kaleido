using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Processor.Eventing;
using Kaleido.Queryable.Eventing;
using Kaleido.Storage;
using Kaleido.UnitTests;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoOutboxEventExtensionsTests
    : SutFixture
{
    private static ProcessEventContext Context() =>
        new() { RequestId = "req-1", ServiceName = "svc", ProcessId = Guid.NewGuid(), StepName = "step-a" };

    private static KaleidoEventEnvelope<StepCompleted, ProcessEventContext> StepCompletedEnvelope() =>
        new()
        {
            EventType = KaleidoEventTypes.ProcessStepCompleted,
            Context = Context(),
            Event = new StepCompleted
            {
                OccurredOn = DateTimeOffset.UtcNow.AddSeconds(-5),
                StepName = "step-a",
                Request = new { MemberId = "M-1" },
                DecisionType = ExecutionDecisionType.Continue,
                Status = StepExecutionStatus.Completed,
                ProcessState = ProcessExecutionState.Active,
            },
        };

    [Fact]
    public void ToOutboxEvent_CarriesTheEventsIdentityAndIsDueAtOnce()
    {
        var envelope = StepCompletedEnvelope();

        var entry = envelope.ToOutboxEvent();

        Assert.Equal(envelope.Event.EventId, entry.EventId);
        Assert.Equal(KaleidoEventTypes.ProcessStepCompleted, entry.EventType);
        Assert.Equal(envelope.Event.OccurredOn, entry.OccurredUtc);
        Assert.Equal(envelope.Event.OccurredOn, entry.NextAttemptUtc);
        Assert.Equal(0, entry.Attempts);
        Assert.False(entry.IsParked);
    }

    [Fact]
    public async Task PublishAsync_HandsTheRestoredEnvelopeToThePublisher()
    {
        var envelope = StepCompletedEnvelope();
        KaleidoEventEnvelope<StepCompleted, ProcessEventContext>? published = null;
        var publisher = new Mock<IEventPublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<KaleidoEventEnvelope<StepCompleted, ProcessEventContext>>(), It.IsAny<CancellationToken>()))
            .Callback<KaleidoEventEnvelope<StepCompleted, ProcessEventContext>, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        await publisher.Object.PublishOutboxEventAsync(envelope.ToOutboxEvent(), CancellationToken.None);

        Assert.NotNull(published);
        Assert.Equal(envelope.Event.EventId, published.Event.EventId);
        Assert.Equal(envelope.Event.OccurredOn, published.Event.OccurredOn);
        Assert.Equal(envelope.Context, published.Context);
        Assert.Equal(StepExecutionStatus.Completed, published.Event.Status);

        // object-typed members come back as JSON.
        var request = Assert.IsType<JsonElement>(published.Event.Request);
        Assert.Equal("M-1", request.GetProperty("memberId").GetString());
    }

    public static TheoryData<string> EventTypes() =>
    [
        KaleidoEventTypes.ProcessCreated,
        KaleidoEventTypes.ProcessPlanBuilt,
        KaleidoEventTypes.ProcessStepCompleted,
        KaleidoEventTypes.ProcessExecutionCompleted,
        KaleidoEventTypes.QueryExecuted,
    ];

    // A real envelope of each type, serialized the way the outbox stores it.
    private static KaleidoOutboxEvent EntryOf(string eventType)
    {
        var now = DateTimeOffset.UtcNow;

        return eventType switch
        {
            KaleidoEventTypes.ProcessCreated => (new KaleidoEventEnvelope<ProcessCreated, ProcessEventContext>
            {
                EventType = eventType,
                Context = Context(),
                Event = new ProcessCreated { OccurredOn = now, State = ProcessExecutionState.Active, CreatedUtc = now, UpdatedUtc = now, SubmittedStepCount = 1 },
            }).ToOutboxEvent(),
            KaleidoEventTypes.ProcessPlanBuilt => (new KaleidoEventEnvelope<PlanBuilt, ProcessEventContext>
            {
                EventType = eventType,
                Context = Context(),
                Event = new PlanBuilt { OccurredOn = now, State = ProcessExecutionState.Active, SubmittedStepCount = 1, CandidateCount = 1, ExecutableCount = 1 },
            }).ToOutboxEvent(),
            KaleidoEventTypes.ProcessStepCompleted => StepCompletedEnvelope().ToOutboxEvent(),
            KaleidoEventTypes.ProcessExecutionCompleted => (new KaleidoEventEnvelope<ExecutionCompleted, ProcessEventContext>
            {
                EventType = eventType,
                Context = Context(),
                Event = new ExecutionCompleted { OccurredOn = now, State = ProcessExecutionState.Complete },
            }).ToOutboxEvent(),
            _ => (new KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>
            {
                EventType = eventType,
                Context = new QueryableEventContext { RequestId = "req-1", ServiceName = "svc", QuerySourceName = "Members" },
                Event = new QueryExecuted { OccurredOn = now, QuerySourceName = "Members", IsDirectQuery = true, TotalCount = 2, ReturnedCount = 2, SortCount = 0, FilterProvided = false },
            }).ToOutboxEvent(),
        };
    }

    [Theory]
    [MemberData(nameof(EventTypes))]
    public async Task PublishAsync_KnowsEveryEventType(string eventType)
    {
        var publisher = new Mock<IEventPublisher>();
        var entry = EntryOf(eventType);

        await publisher.Object.PublishOutboxEventAsync(entry, CancellationToken.None);

        Assert.Single(publisher.Invocations);
    }

    [Fact]
    public async Task PublishAsync_ForAQueryEvent_UsesTheQueryableContext()
    {
        KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>? published = null;
        var publisher = new Mock<IEventPublisher>();
        publisher
            .Setup(x => x.PublishAsync(It.IsAny<KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>>(), It.IsAny<CancellationToken>()))
            .Callback<KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        await publisher.Object.PublishOutboxEventAsync(EntryOf(KaleidoEventTypes.QueryExecuted), CancellationToken.None);

        Assert.Equal("Members", published!.Context.QuerySourceName);
        Assert.Equal(2, published.Event.TotalCount);
    }
    [Fact]
    public async Task PublishAsync_ForAnUnknownEventType_Throws()
    {
        var entry = new KaleidoOutboxEvent { EventId = Guid.NewGuid(), EventType = "something.else.v1", Payload = "{}", OccurredUtc = DateTimeOffset.UtcNow };

        var exception = await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            Mock.Of<IEventPublisher>().PublishOutboxEventAsync(entry, CancellationToken.None));

        Assert.Contains("something.else.v1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PublishAsync_ForAnEmptyPayload_Throws()
    {
        var entry = new KaleidoOutboxEvent { EventId = Guid.NewGuid(), EventType = KaleidoEventTypes.ProcessCreated, Payload = "null", OccurredUtc = DateTimeOffset.UtcNow };

        await Assert.ThrowsAsync<KaleidoFrameworkException>(() =>
            Mock.Of<IEventPublisher>().PublishOutboxEventAsync(entry, CancellationToken.None));
    }
}
