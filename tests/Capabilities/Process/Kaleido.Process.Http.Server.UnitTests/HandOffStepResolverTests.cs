using Kaleido.Platform.Queryable;
using Kaleido.Platform.Registry;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Server.Processor.UnitTests;

// #198: after a hand-off the runtime answers with the target's next steps by name; the resolver
// finds them in the target's processor in the full registry (its current version when versions are known).
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

    private readonly Mock<IKaleidoRegistrySource> source = new(MockBehavior.Strict);

    private readonly Mock<ILogger<HandOffStepResolver>> logger = new();

    public HandOffStepResolverTests() =>
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    private HandOffStepResolver CreateSut() => CreateSut(source.Object, logger.Object);

    private static HandOffStepResolver CreateSut(IKaleidoRegistrySource registries, ILogger<HandOffStepResolver> log) => new(registries, log);

    private static KaleidoRegistry Contents(IReadOnlyCollection<ProcessorRegistryResponse> processes, IReadOnlyCollection<QueryableSourceResponse> queryables, IReadOnlyCollection<RegistryReadError> errors) =>
        new() { Processes = processes, Queryables = queryables, Errors = errors };

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

    private void Registry(KaleidoRegistry contents, KaleidoRegistryAvailability? status = null)
    {
        source.Setup(x => x.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(contents);
        source.Setup(x => x.GetAvailabilityAsync(It.IsAny<CancellationToken>())).ReturnsAsync(status ?? new KaleidoRegistryAvailability());
    }

    private void Publishes(params ProcessStepResponse[] steps) =>
        Registry(Contents([new ProcessorRegistryResponse { Name = Target, ServiceName = Target, Steps = steps }], [], []));

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
        source.VerifyNoOtherCalls();
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

    // A target the full registry does not have (never registered, or its registry could not be read)
    // leaves every step out, with pro_not_permitted and a Warning; another service's steps never stand in.
    [Fact]
    public async Task ATargetTheRegistryDoesNotHave_LeavesEveryStepOut_WithTheNotice()
    {
        Registry(Contents([new ProcessorRegistryResponse { Name = "intake", ServiceName = "intake", Steps = [CaptureMri, Validate] }], [], []));

        var continuation = await CreateSut().ResolveAsync(HandedOff(required: CaptureMri.Name, available: [Validate.Name], notice: TargetNotice), Cancel);

        Assert.Null(continuation.RequiredStep);
        Assert.Empty(continuation.AvailableSteps);
        Assert.Equal(ProcessContinuationFilter.NotPermitted, continuation.Notice);
        VerifyWarning(Times.Once());
    }

    // #553: with the target's versions known, its steps are its current version's.
    [Fact]
    public async Task WhenTheRegistryKnowsVersions_TheTargetsCurrentVersion_Answers()
    {
        Registry(Contents(
            [
                new ProcessorRegistryResponse { Name = Target, ServiceName = Target, ServiceVersion = "1", Steps = [Validate] },
                new ProcessorRegistryResponse { Name = Target, ServiceName = Target, ServiceVersion = "2", Steps = [Validate, CaptureMri] },
            ],
            [],
            []),
            new KaleidoRegistryAvailability
            {
                Services =
                [
                    new RegistryServiceAvailability { ServiceName = Target, ServiceVersion = "1", IsCurrent = true },
                    new RegistryServiceAvailability { ServiceName = Target, ServiceVersion = "2" },
                ],
            });

        var continuation = await CreateSut().ResolveAsync(HandedOff(available: [Validate.Name, CaptureMri.Name]), Cancel);

        Assert.Equal([Validate.Name], continuation.AvailableSteps.Select(x => x.Name));
        Assert.Equal(ProcessContinuationFilter.NotPermitted, continuation.Notice);
    }
}
