using Kaleido.Http.Authorization;
using Kaleido.Registry;
using Kaleido.UnitTests;
using Microsoft.Extensions.Logging;

namespace Kaleido.Http.Processor.UnitTests;

// #524: a caller is never described a step they may not run.
public sealed class ProcessContinuationFilterTests
    : SutFixture
{
    private static readonly Guid ProcessId = Guid.Parse("6b1f0a3c-2d4e-4f58-9a7b-0c1d2e3f4a5b");

    private static readonly AuthorizationMetadata Clerks = new(null, ["clerk"]);
    private static readonly AuthorizationMetadata Admins = new(null, ["admin"]);

    private readonly Mock<IKaleidoAuthorizer> authorizer = new();
    private readonly Mock<ILogger<ProcessContinuationFilter>> logger = new();

    public ProcessContinuationFilterTests()
    {
        // The caller is a clerk.
        authorizer.Setup(x => x.IsEnforced).Returns(true);
        authorizer.Setup(x => x.CanAccessAsync(It.IsAny<AuthorizationMetadata?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthorizationMetadata? authorization, CancellationToken _) => authorization == Clerks);
        authorizer.Setup(x => x.FilterAsync(It.IsAny<IEnumerable<ProcessStepSummary>>(), It.IsAny<Func<ProcessStepSummary, AuthorizationMetadata?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<ProcessStepSummary> items, Func<ProcessStepSummary, AuthorizationMetadata?> rule, CancellationToken _) =>
                (IReadOnlyList<ProcessStepSummary>)items.Where(x => rule(x) == Clerks).ToList());
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
    }

    private ProcessContinuationFilter CreateSut() => new(authorizer.Object, logger.Object);

    private static ProcessStepSummary Step(string name, AuthorizationMetadata rule) =>
        new() { Name = name, Authorization = rule, ExecuteUrl = $"/svc/processes/steps/{name.ToLowerInvariant()}" };

    private static ProcessRequiredStep Required(string name, AuthorizationMetadata rule) =>
        new() { Name = name, Authorization = rule, ExecuteUrl = $"/svc/processes/steps/{name.ToLowerInvariant()}" };

    private string[] Warnings() =>
        [.. logger.Invocations
            .Where(x => x.Method.Name == nameof(ILogger.Log) && (LogLevel)x.Arguments[0] == LogLevel.Warning)
            .Select(x => x.Arguments[2].ToString()!)];

    [Fact]
    public async Task ARequiredStepTheCallerMayRun_IsKept_WithoutANotice()
    {
        var required = Required("Review", Clerks);

        var continuation = await CreateSut().FilterAsync(ProcessId, required, [], TestContext.Current.CancellationToken);

        Assert.Same(required, continuation.RequiredStep);
        Assert.Null(continuation.Notice);
        Assert.Empty(Warnings());
    }

    // The required step is left out and not named; the notice says only that the caller may not go on.
    [Fact]
    public async Task ARequiredStepTheCallerMayNotRun_IsLeftOut_WithTheNotice_AndAWarningNamingIt()
    {
        var continuation = await CreateSut().FilterAsync(ProcessId, Required("Approve", Admins), [], TestContext.Current.CancellationToken);

        Assert.Null(continuation.RequiredStep);
        Assert.Equal(ProcessorErrorCodes.NotPermitted, continuation.Notice?.Code);
        Assert.DoesNotContain("Approve", continuation.Notice?.Message, StringComparison.Ordinal);
        Assert.Contains(Warnings(), x => x.Contains("Approve", StringComparison.Ordinal) && x.Contains(ProcessId.ToString(), StringComparison.Ordinal));
    }

    // Some available steps the caller may run: only those are listed, and nothing is wrong.
    [Fact]
    public async Task AvailableStepsTheCallerMayNotRun_AreLeftOut_WithoutANotice_WhileOthersRemain()
    {
        var continuation = await CreateSut().FilterAsync(ProcessId, null, [Step("Note", Clerks), Step("Approve", Admins)], TestContext.Current.CancellationToken);

        Assert.Equal(["Note"], continuation.AvailableSteps.Select(x => x.Name));
        Assert.Null(continuation.Notice);
        Assert.Empty(Warnings());
    }

    [Fact]
    public async Task NoAvailableStepTheCallerMayRun_LeavesAnEmptyList_WithTheNotice_AndAWarningNamingThem()
    {
        var continuation = await CreateSut().FilterAsync(ProcessId, null, [Step("Approve", Admins), Step("Reject", Admins)], TestContext.Current.CancellationToken);

        Assert.Empty(continuation.AvailableSteps);
        Assert.Equal(ProcessContinuationFilter.NotPermitted, continuation.Notice);
        Assert.Contains(Warnings(), x => x.Contains("Approve, Reject", StringComparison.Ordinal));
    }

    // Nothing to continue with (a hand-off, an ended process): nothing to tell.
    [Fact]
    public async Task NothingToContinueWith_HasNoNotice()
    {
        var continuation = await CreateSut().FilterAsync(ProcessId, null, [], TestContext.Current.CancellationToken);

        Assert.Null(continuation.RequiredStep);
        Assert.Empty(continuation.AvailableSteps);
        Assert.Null(continuation.Notice);
    }

    [Fact]
    public async Task WhenAuthorizationIsNotEnforced_EverythingIsKept()
    {
        authorizer.Setup(x => x.IsEnforced).Returns(false);
        var required = Required("Approve", Admins);
        IReadOnlyCollection<ProcessStepSummary> available = [Step("Reject", Admins)];

        var continuation = await CreateSut().FilterAsync(ProcessId, required, available, TestContext.Current.CancellationToken);

        Assert.Same(required, continuation.RequiredStep);
        Assert.Same(available, continuation.AvailableSteps);
        Assert.Null(continuation.Notice);
    }

    [Fact]
    public async Task FilterAsync_WithoutTheAvailableSteps_Throws()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => CreateSut().FilterAsync(ProcessId, null, null!, TestContext.Current.CancellationToken));
    }
}
