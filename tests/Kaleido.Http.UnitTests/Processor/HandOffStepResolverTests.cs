using Kaleido.Http.Registry;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Processor.UnitTests;

// #198: after a hand-off the runtime answers with the target's next steps by name; the resolver
// finds them in the target's published registry (its client's cached copy).
public sealed class HandOffStepResolverTests
    : SutFixture
{
    private const string Target = "radiology";

    private static readonly AuthorizationMetadata RadiologyRole = new(Policy: null, Roles: ["radiology"]);

    private static readonly ProcessStepResponse Validate =
        new() { Name = "ValidateMemberStep", DisplayName = "Validate member", Description = "Checks the member.", Destructive = true, Authorization = RadiologyRole, ExecuteUrl = "/radiology/processes/steps/validatememberstep" };

    private static readonly ProcessStepResponse CaptureMri =
        new() { Name = "CaptureMriInfoStep", DisplayName = "MRI questions", IsInformationStep = true, Authorization = RadiologyRole, ExecuteUrl = "/radiology/processes/steps/capturemriinfostep" };

    private static readonly InformationRequest Questions = new() { InformationRequestId = "mri-intake", ExchangeId = "exchange-1" };

    private static readonly KaleidoNotice TargetNotice = new("target_notice", "The target left something out.");

    private readonly Mock<IKaleidoDownstreamRegistries> downstream = new(MockBehavior.Strict);

    private readonly Mock<ILogger<HandOffStepResolver>> logger = new();

    public HandOffStepResolverTests() =>
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    private HandOffStepResolver CreateSut() => CreateSut(downstream.Object, logger.Object);

    private static HandOffStepResolver CreateSut(IKaleidoDownstreamRegistries registries, ILogger<HandOffStepResolver> log) => new(registries, log);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static ProcessResult HandedOff(string? required = null, InformationRequest? questions = null, IReadOnlyCollection<string>? available = null, KaleidoNotice? notice = null) =>
        new()
        {
            ProcessId = Guid.NewGuid(),
            State = ProcessExecutionState.HandOff,
            TargetProcessorName = Target,
            RequiredStep = required,
            RequiredInformationRequest = questions,
            AvailableSteps = available ?? [],
            Notice = notice,
            Steps = [],
        };

    private void Publishes(params ProcessStepResponse[] steps) =>
        downstream.Setup(x => x.GetServiceAsync(Target, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AggregatedRegistryResponse { Processes = [new ProcessorRegistryResponse { Name = Target, ServiceName = Target, Steps = steps }] });

    private void VerifyWarning(Times times) =>
        logger.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception?, string>>()), times);

    // Each name becomes the target's own published step: its description, flags, authorization and
    // execute URL; the questions go on the required step.
    [Fact]
    public async Task TheTargetsNames_BecomeItsPublishedSteps()
    {
        Publishes(Validate, CaptureMri);

        var required = await CreateSut().ResolveAsync(HandedOff(required: CaptureMri.Name, questions: Questions, notice: TargetNotice), Cancel);
        var available = await CreateSut().ResolveAsync(HandedOff(available: [Validate.Name.ToUpperInvariant()]), Cancel);

        Assert.Equal(
            new ProcessRequiredStep { Name = CaptureMri.Name, DisplayName = CaptureMri.DisplayName, IsInformationStep = true, Authorization = RadiologyRole, ExecuteUrl = CaptureMri.ExecuteUrl, InformationRequest = Questions },
            required.RequiredStep);
        Assert.Same(TargetNotice, required.Notice);
        var step = Assert.Single(available.AvailableSteps);
        Assert.Equal(
            new ProcessStepSummary { Name = Validate.Name, DisplayName = Validate.DisplayName, Description = Validate.Description, Destructive = true, Authorization = RadiologyRole, ExecuteUrl = Validate.ExecuteUrl },
            step);
        Assert.Null(available.Notice);
        VerifyWarning(Times.Never());
    }

    // A later request on a process handed off earlier carries no answer: nothing is read.
    [Fact]
    public async Task WithoutNextSteps_NothingIsRead()
    {
        var continuation = await CreateSut().ResolveAsync(HandedOff(notice: TargetNotice), Cancel);

        Assert.Null(continuation.RequiredStep);
        Assert.Empty(continuation.AvailableSteps);
        Assert.Same(TargetNotice, continuation.Notice);
        downstream.VerifyNoOtherCalls();
    }

    // A step the target's registry (as this host's client sees it) does not publish is left out with
    // pro_not_permitted, and a Warning says which; the others are kept.
    [Fact]
    public async Task AStepTheTargetDoesNotPublish_IsLeftOut_WithTheNotice()
    {
        Publishes(Validate);

        var continuation = await CreateSut().ResolveAsync(HandedOff(required: CaptureMri.Name, available: [Validate.Name, "UnknownStep"]), Cancel);

        Assert.Null(continuation.RequiredStep);
        Assert.Equal([Validate.Name], continuation.AvailableSteps.Select(x => x.Name));
        Assert.Equal(ProcessContinuationFilter.NotPermitted, continuation.Notice);
        VerifyWarning(Times.Once());
    }

    // A registry that cannot be read leaves every step out, with pro_not_permitted and a Warning.
    [Fact]
    public async Task AnUnreadableTargetRegistry_LeavesEveryStepOut_WithTheNotice()
    {
        downstream.Setup(x => x.GetServiceAsync(Target, It.IsAny<CancellationToken>())).ReturnsAsync((AggregatedRegistryResponse?)null);

        var continuation = await CreateSut().ResolveAsync(HandedOff(required: CaptureMri.Name, available: [Validate.Name], notice: TargetNotice), Cancel);

        Assert.Null(continuation.RequiredStep);
        Assert.Empty(continuation.AvailableSteps);
        Assert.Equal(ProcessContinuationFilter.NotPermitted, continuation.Notice);
        VerifyWarning(Times.Once());
    }
}
