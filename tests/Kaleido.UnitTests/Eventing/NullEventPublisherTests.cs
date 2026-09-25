using Kaleido.Eventing;
using Kaleido.Process.Eventing;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.UnitTests.Eventing;

public sealed class NullEventPublisherTests
{
    private static NullEventPublisher CreateSut() =>
        new(NullLogger<NullEventPublisher>.Instance);

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
