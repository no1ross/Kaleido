using Kaleido.Http.Authorization;
using Kaleido.Observability;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Kaleido.UnitTests;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessStateServiceTests
    : SutFixture
{
    private static ProcessStateService CreateSut(
        IKaleidoContextStore contextStore,
        IProcessorStepRegistry registry,
        IProcessorResponseFactory responseFactory) =>
        new(
            contextStore,
            registry,
            new KaleidoServiceOptions { ServiceName = "test-processor" },
            responseFactory,
            NullLogger<ProcessStateService>.Instance);

    [Fact]
    public async Task GetCurrentState_WhenContextDoesNotExist_ReturnsNull()
    {
        var contextStore =
            new Mock<IKaleidoContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessorContext?)null);

        var service =
            CreateSut(
                contextStore.Object,
                CreateRegistry(),
                new Mock<IProcessorResponseFactory>().Object);

        var result =
            await service.GetCurrentState(
                Guid.NewGuid(),
                CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentState_WhenContextExists_MapsView()
    {
        var processId =
            Guid.NewGuid();

        var context =
            new ProcessorContext
            {
                ProcessId = processId,
                ProcessorName = "test-processor",
                State = ProcessExecutionState.AwaitingStepSelection,
                RequiredStep = "Step-B",
                RequiredInformationRequest = PendingRequest,
                AvailableSteps = ["Step-A"],
                CreatedUtc = DateTimeOffset.UtcNow.AddMinutes(-5),
                UpdatedUtc = DateTimeOffset.UtcNow,
                Steps =
                [
                    new StepContext
                    {
                        StepName = "Step-B",
                        Version = "1.0.0",
                        Status = StepExecutionStatus.Pending
                    },
                    new StepContext
                    {
                        StepName = "Step-A",
                        Version = "1.0.0",
                        Status = StepExecutionStatus.Completed,
                        LastExecuted = DateTimeOffset.UtcNow.AddMinutes(-1)
                    }
                ]
            };

        var contextStore =
            new Mock<IKaleidoContextStore>();

        contextStore
            .Setup(x =>
                x.LoadAsync(
                    processId,
                    It.IsAny<CancellationToken>()))
            .ReturnsAsync(context);

        var expectedSummary = new ProcessStepSummary
        {
            Name = "Step-A"
        };

        var responseFactory = new Mock<IProcessorResponseFactory>();
        responseFactory
            .Setup(x => x.CreateStepSummary(
                It.IsAny<ProcessorStepSummary>(),
                "test-processor"))
            .Returns(expectedSummary);

        var expectedRequired = new ProcessRequiredStep
        {
            Name = "Step-B",
            InformationRequest = PendingRequest
        };

        responseFactory
            .Setup(x => x.CreateRequiredStep(
                It.Is<ProcessorStepSummary>(s => s.Name == "Step-B"),
                PendingRequest,
                "test-processor"))
            .Returns(expectedRequired);

        var service =
            CreateSut(
                contextStore.Object,
                CreateRegistry("Step-A", "Step-B"),
                responseFactory.Object);

        var result =
            await service.GetCurrentState(
                processId,
                CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(processId, result.ProcessId);
        Assert.Equal(ProcessExecutionState.AwaitingStepSelection, result.State);
        Assert.Same(expectedRequired, result.RequiredStep);
        Assert.Null(result.TargetProcessorName);
        Assert.Equal("Step-A", Assert.Single(result.AvailableSteps).Name);

        Assert.Collection(
            result.Steps,
            step =>
            {
                Assert.Equal("Step-A", step.StepName);
                Assert.Equal(StepExecutionStatus.Completed, step.Status);
            },
            step =>
            {
                Assert.Equal("Step-B", step.StepName);
                Assert.Equal(StepExecutionStatus.Pending, step.Status);
            });
    }

    private static readonly InformationRequest PendingRequest =
        new()
        {
            InformationRequestId = "out-of-network",
            Items = [new InformationItem { Id = "q1", Text = "Proceed?", Type = InformationItemType.Boolean }]
        };

    private static IProcessorStepRegistry CreateRegistry(
        params string[] stepNames)
    {
        var mock = new Mock<IProcessorStepRegistry>();

        foreach (var name in stepNames)
        {
            var registration = new ProcessStepRegistration(
                typeof(object),
                null,
                typeof(object),
                [],
                [],
                [],
                new RepeatableOptions { Enabled = false },
                new ProcessStepMetadata { Name = name, Description = name, Version = "1.0.0", DisplayName = name });

            mock.Setup(x => x.Find(name))
                .Returns(registration);
        }

        return mock.Object;
    }
}
