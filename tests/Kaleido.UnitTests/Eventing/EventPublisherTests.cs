using Kaleido.Eventing;
using Kaleido.Processor.Eventing;
using Microsoft.Extensions.Logging.Abstractions;

using Kaleido.UnitTests;

namespace Kaleido.Eventing.UnitTests;

public sealed class EventPublisherTests
    : SutFixture
{
    private static EventPublisher CreateSut() =>
        new(NullLogger<EventPublisher>.Instance);

    [Fact]
    public async Task PublishAsync_CompletesWithoutThrowing()
    {
        var sut = CreateSut();
        var envelope = new KaleidoEventEnvelope<ProcessCreated, Kaleido.Eventing.ProcessEventContext>
        {
            EventType = "process.created.v1",
            Context = new Kaleido.Eventing.ProcessEventContext
            {
                RequestId = "req-1",
                ServiceName = "svc",
                ProcessId = Guid.NewGuid(),
                StepName = "step-a"
            },
            Event = new ProcessCreated
            {
                OccurredOn = DateTimeOffset.UtcNow,
                State = ProcessExecutionState.Active,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow,
                SubmittedStepCount = 0
            }
        };

        await sut.PublishAsync(envelope);
    }
}
