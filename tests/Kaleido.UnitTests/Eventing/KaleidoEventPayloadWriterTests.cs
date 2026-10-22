using System.Text.Json;
using Kaleido.Exceptions;
using Kaleido.Processor.Eventing;
using Kaleido.Queryable.Eventing;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using Microsoft.Extensions.Logging;

namespace Kaleido.Eventing.UnitTests;

public sealed class KaleidoEventPayloadWriterTests
    : SutFixture
{
    private static readonly DataClassification Phi = new("test", "phi");
    private static readonly DataClassification Pii = new("test", "pii");

    private static KaleidoEventPayloadWriter CreateSut(
        IKaleidoEventRedactorProvider? redactorProvider = null,
        IEnumerable<IKaleidoEventRedactor>? redactors = null,
        ILogger<KaleidoEventPayloadWriter>? logger = null) =>
        new(
            redactors ?? [],
            redactorProvider ?? NoEventPolicy(),
            logger ?? Mock.Of<ILogger<KaleidoEventPayloadWriter>>());

    // No AddEventRedaction: nothing is configured, every classified value is erased.
    private static IKaleidoEventRedactorProvider NoEventPolicy() => Mock.Of<IKaleidoEventRedactorProvider>();

    private static Member AMember() =>
        new()
        {
            Name = "Jane Doe",
            BirthDate = new DateOnly(1980, 1, 2),
            Ssn4 = 1234,
            Plan = "Gold",
            Notes = "allergic to penicillin",
            Address = new Address("Main Street 1"),
        };

    private static KaleidoEventEnvelope<QueryExecuted, QueryableEventContext> QueryEnvelope(params object?[] records) =>
        new()
        {
            EventType = KaleidoEventTypes.QueryExecuted,
            Context = new QueryableEventContext { RequestId = "req-1", ServiceName = "member", QuerySourceName = "Members" },
            Event = new QueryExecuted
            {
                OccurredOn = DateTimeOffset.UtcNow,
                QuerySourceName = "Members",
                IsDirectQuery = true,
                TotalCount = records.Length,
                ReturnedCount = records.Length,
                SortCount = 0,
                FilterProvided = false,
                Records = records,
            },
        };

    private static KaleidoEventEnvelope<StepCompleted, ProcessEventContext> StepEnvelope(object? request, object? response) =>
        new()
        {
            EventType = KaleidoEventTypes.ProcessStepCompleted,
            Context = new ProcessEventContext { RequestId = "req-1", ServiceName = "intake", ProcessId = Guid.NewGuid(), StepName = "CaptureMember" },
            Event = new StepCompleted
            {
                OccurredOn = DateTimeOffset.UtcNow,
                StepName = "CaptureMember",
                StepVersion = "1.0",
                Request = request,
                Response = response,
                DecisionType = ExecutionDecisionType.Continue,
                ExecutionStatus = StepExecutionStatus.Completed,
                Outcome = StepExecutionOutcome.Completed,
                ProcessState = ProcessExecutionState.Active,
            },
        };

    private static JsonElement FirstRecord(KaleidoOutboxEvent entry) =>
        JsonDocument.Parse(entry.Payload).RootElement.GetProperty("event").GetProperty("records")[0];

    [Fact]
    public void Write_WithoutAnEventPolicy_ErasesClassifiedValuesAndKeepsTheRest()
    {
        var record = FirstRecord(CreateSut().Write(QueryEnvelope(AMember())));

        Assert.Equal(string.Empty, record.GetProperty("name").GetString());
        Assert.Equal(string.Empty, record.GetProperty("birthDate").GetString());
        Assert.Equal(string.Empty, record.GetProperty("ssn4").GetString());
        Assert.Equal(string.Empty, record.GetProperty("address").GetString());
        Assert.Equal("Gold", record.GetProperty("plan").GetString());
    }

    [Fact]
    public void Write_WithoutAnEventPolicy_WarnsOnce()
    {
        var logger = new Mock<ILogger<KaleidoEventPayloadWriter>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var sut = CreateSut(logger: logger.Object);

        sut.Write(QueryEnvelope(AMember()));
        sut.Write(QueryEnvelope(AMember(), AMember()));

        logger.Verify(
            x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    // A configured policy that erases (no redactor, no fallback) is a choice, not a mistake: no warning.
    [Fact]
    public void Write_WithAnEventPolicyThatErases_ErasesWithoutWarning()
    {
        var logger = new Mock<ILogger<KaleidoEventPayloadWriter>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        var policy = Mock.Of<IKaleidoEventRedactorProvider>(x => x.IsConfigured);

        var record = FirstRecord(CreateSut(policy, logger: logger.Object).Write(QueryEnvelope(AMember())));

        Assert.Equal(string.Empty, record.GetProperty("name").GetString());
        Assert.DoesNotContain(logger.Invocations, x => x.Method.Name == nameof(ILogger.Log));
    }

    [Fact]
    public void Write_WithARedactorProvider_RedactsEachValueAsText()
    {
        var record = FirstRecord(CreateSut(new EchoRedactorProvider()).Write(QueryEnvelope(AMember())));

        Assert.Equal("redacted(phi|Jane Doe)", record.GetProperty("name").GetString());
        Assert.Equal("redacted(phi|01/02/1980)", record.GetProperty("birthDate").GetString());
        Assert.Equal("redacted(pii|1234)", record.GetProperty("ssn4").GetString());
        Assert.Equal("""redacted(phi|{"street":"Main Street 1"})""", record.GetProperty("address").GetString());
    }

    [Fact]
    public void Write_ForAPropertyWithSeveralClassifications_AsksForTheirUnion()
    {
        var provider = new EchoRedactorProvider();

        CreateSut(provider).Write(QueryEnvelope(AMember()));

        Assert.Contains(provider.Requested, set => set.Equals(new DataClassificationSet(Phi, Pii)));
    }

    [Fact]
    public void Write_KeepsANullClassifiedValueNull()
    {
        var record = FirstRecord(CreateSut(new EchoRedactorProvider()).Write(QueryEnvelope(AMember() with { Address = null })));

        Assert.Equal(JsonValueKind.Null, record.GetProperty("address").ValueKind);
    }

    [Fact]
    public void Write_LeavesAPropertyMarkedAsNotClassifiedUntouched()
    {
        var record = FirstRecord(CreateSut(new EchoRedactorProvider()).Write(QueryEnvelope(AMember())));

        Assert.Equal("member-7", record.GetProperty("memberId").GetString());
    }

    [Fact]
    public void Write_RedactsClassifiedValuesInAStepsRequestAndResponse()
    {
        var entry = CreateSut().Write(StepEnvelope(AMember(), new { Member = AMember(), Accepted = true }));

        var @event = JsonDocument.Parse(entry.Payload).RootElement.GetProperty("event");
        Assert.Equal(string.Empty, @event.GetProperty("request").GetProperty("name").GetString());
        Assert.Equal(string.Empty, @event.GetProperty("response").GetProperty("member").GetProperty("name").GetString());
        Assert.True(@event.GetProperty("response").GetProperty("accepted").GetBoolean());
    }

    [Fact]
    public void Write_KeepsTheEventsOwnFields()
    {
        var envelope = QueryEnvelope(AMember());

        var entry = CreateSut().Write(envelope);

        var root = JsonDocument.Parse(entry.Payload).RootElement;
        Assert.Equal(envelope.Event.EventId, entry.EventId);
        Assert.Equal(KaleidoEventTypes.QueryExecuted, root.GetProperty("eventType").GetString());
        Assert.Equal("Members", root.GetProperty("context").GetProperty("querySourceName").GetString());
        Assert.Equal(1, root.GetProperty("event").GetProperty("totalCount").GetInt32());
    }

    [Fact]
    public void Write_RunsTheEventRedactorsInOrderBeforeClassification()
    {
        var calls = new List<string>();
        var first = new RecordingRedactor("first", calls, envelope =>
            envelope with { Event = envelope.Event with { Records = [AMember() with { Plan = "Silver" }] } });
        var second = new RecordingRedactor("second", calls, envelope => envelope);

        var record = FirstRecord(CreateSut(redactors: [first, second]).Write(QueryEnvelope(AMember())));

        Assert.Equal(["first", "second"], calls);
        Assert.Equal("Silver", record.GetProperty("plan").GetString());
        Assert.Equal(string.Empty, record.GetProperty("name").GetString());
    }

    [Fact]
    public void Write_WhenAnEventRedactorReturnsNothing_Throws()
    {
        var redactor = new RecordingRedactor("broken", [], _ => null!);

        Assert.Throws<KaleidoFrameworkException>(() => CreateSut(redactors: [redactor]).Write(QueryEnvelope(AMember())));
    }

    private sealed class PhiAttribute() : DataClassificationAttribute(Phi);

    private sealed class PiiAttribute() : DataClassificationAttribute(Pii);

    private sealed record Address(string Street);

    private sealed record Member
    {
        [NoDataClassification]
        public string MemberId { get; init; } = "member-7";

        [Phi]
        public string Name { get; init; } = string.Empty;

        [Phi]
        public DateOnly BirthDate { get; init; }

        [Pii]
        public int Ssn4 { get; init; }

        public string Plan { get; init; } = string.Empty;

        [Phi]
        [Pii]
        public string Notes { get; init; } = string.Empty;

        [Phi]
        public Address? Address { get; init; }
    }

    // Shows the classification and the text it was given: redacted({phi|pii|several}|{value}).
    private sealed class EchoRedactorProvider : IKaleidoEventRedactorProvider
    {
        public List<DataClassificationSet> Requested { get; } = [];

        public bool IsConfigured => true;

        public Redactor GetRedactor(DataClassificationSet classifications)
        {
            Requested.Add(classifications);
            return new EchoRedactor(classifications.Equals(new DataClassificationSet(Phi)) ? "phi" : classifications.Equals(new DataClassificationSet(Pii)) ? "pii" : "several");
        }
    }

    private sealed class EchoRedactor(string classifications) : Redactor
    {
        private string Prefix => $"redacted({classifications}|";

        public override int GetRedactedLength(ReadOnlySpan<char> input) => Prefix.Length + input.Length + 1;

        public override int Redact(ReadOnlySpan<char> source, Span<char> destination)
        {
            var text = $"{Prefix}{source})";
            text.AsSpan().CopyTo(destination);
            return text.Length;
        }
    }

    private sealed class RecordingRedactor(string name, List<string> calls, Func<KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>, KaleidoEventEnvelope<QueryExecuted, QueryableEventContext>> redact)
        : IKaleidoEventRedactor
    {
        public KaleidoEventEnvelope<TEvent, TContext> Redact<TEvent, TContext>(KaleidoEventEnvelope<TEvent, TContext> envelope)
            where TEvent : IKaleidoEvent
        {
            calls.Add(name);
            return envelope is KaleidoEventEnvelope<QueryExecuted, QueryableEventContext> query
                ? (KaleidoEventEnvelope<TEvent, TContext>)(object)redact(query)
                : envelope;
        }
    }
}
