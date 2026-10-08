using Kaleido.Processor.Context;
using Kaleido.Provider.SQLite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Provider.SQLite.UnitTests;

// The reference implementation's fidelity tests double as the executable
// IProcessorContextStore contract spec - a consumer-authored store should
// satisfy the same shape.
public sealed class SqliteProcessorContextStoreTests
    : Kaleido.UnitTests.SutFixture
{
    private static SqliteProcessorContextDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<SqliteProcessorContextDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        var context = new SqliteProcessorContextDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static SqliteProcessorContextStore CreateSut(SqliteProcessorContextDbContext dbContext) =>
        new(dbContext,
            new KaleidoServiceOptions { ServiceName = "test-svc" },
            NullLogger<SqliteProcessorContextStore>.Instance);

    private static ProcessorContext CreateContext(
        Guid processId,
        ProcessExecutionState state = ProcessExecutionState.Active) =>
        new()
        {
            ProcessId = processId,
            ProcessorName = "test-svc",
            LatestRequestId = "req-1",
            State = state,
            RequiredStep = "NextStep",
            TargetProcessorName = "target-svc",
            AvailableSteps = ["StepA", "StepB"],
            Steps =
            [
                new StepContext
                {
                    StepName = "StepA",
                    Version = "1.0.0",
                    Status = StepExecutionStatus.Completed,
                    LatestRequestId = "req-0",
                    LastExecuted = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero)
                },
                new StepContext
                {
                    StepName = "StepB",
                    Version = "1.0.0",
                    Status = StepExecutionStatus.Pending
                }
            ],
            CreatedUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
        };

    [Fact]
    public async Task LoadAsync_WhenProcessNotFound_ReturnsNull()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);

        var result = await sut.LoadAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsAllFields()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var expected = CreateContext(Guid.NewGuid());

        await sut.SaveAsync(expected);

        var loaded = await sut.LoadAsync(expected.ProcessId);

        Assert.NotNull(loaded);
        Assert.Equal(expected.ProcessId, loaded.ProcessId);
        Assert.Equal(expected.ProcessorName, loaded.ProcessorName);
        Assert.Equal(expected.LatestRequestId, loaded.LatestRequestId);
        Assert.Equal(expected.State, loaded.State);
        Assert.Equal(expected.RequiredStep, loaded.RequiredStep);
        Assert.Equal(expected.TargetProcessorName, loaded.TargetProcessorName);
        Assert.Equal(expected.AvailableSteps, loaded.AvailableSteps);
        Assert.Equal(expected.CreatedUtc, loaded.CreatedUtc);
        Assert.Equal(expected.UpdatedUtc, loaded.UpdatedUtc);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsStepFidelity()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var expected = CreateContext(Guid.NewGuid());

        await sut.SaveAsync(expected);

        var loaded = await sut.LoadAsync(expected.ProcessId);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Steps.Count);

        var stepA = loaded.FindStep("StepA");
        Assert.NotNull(stepA);
        Assert.Equal("1.0.0", stepA.Version);
        Assert.Equal(StepExecutionStatus.Completed, stepA.Status);
        Assert.Equal("req-0", stepA.LatestRequestId);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), stepA.LastExecuted);

        var stepB = loaded.FindStep("StepB");
        Assert.NotNull(stepB);
        Assert.Equal(StepExecutionStatus.Pending, stepB.Status);
        Assert.Null(stepB.LatestRequestId);
        Assert.Null(stepB.LastExecuted);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsThePendingInformationRequest()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var expected =
            CreateContext(Guid.NewGuid(), ProcessExecutionState.AwaitingInformation) with
            {
                RequiredStep = "StepB",
                TargetProcessorName = null,
                RequiredInformationRequest = new InformationRequest
                {
                    InformationRequestId = "out-of-network",
                    Title = "Out-of-network attestation",
                    Items =
                    [
                        new InformationItem
                        {
                            Id = "reason",
                            Text = "Why?",
                            Type = InformationItemType.Choice,
                            Options = [new InformationOption { Value = "continuity", Display = "Continuity of care" }]
                        },
                        new InformationItem
                        {
                            Id = "details",
                            Text = "Details",
                            Type = InformationItemType.Group,
                            Items = [new InformationItem { Id = "visits", Text = "Visits", Type = InformationItemType.WholeNumber, Repeats = true }]
                        }
                    ]
                }
            };

        await sut.SaveAsync(expected);

        var loaded = await sut.LoadAsync(expected.ProcessId);

        Assert.NotNull(loaded?.RequiredInformationRequest);
        var request = loaded.RequiredInformationRequest;
        Assert.Equal("out-of-network", request.InformationRequestId);
        Assert.Equal("Out-of-network attestation", request.Title);
        Assert.Equal(InformationItemType.Choice, request.Items[0].Type);
        Assert.Equal("Continuity of care", Assert.Single(request.Items[0].Options).Display);
        var nested = Assert.Single(request.Items[1].Items);
        Assert.Equal(InformationItemType.WholeNumber, nested.Type);
        Assert.True(nested.Repeats);
    }

    [Fact]
    public async Task SaveAsync_WhenTheRequestIsAnswered_ClearsThePendingInformationRequest()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var processId = Guid.NewGuid();

        await sut.SaveAsync(
            CreateContext(processId) with
            {
                RequiredStep = "StepB",
                TargetProcessorName = null,
                RequiredInformationRequest = new InformationRequest { InformationRequestId = "out-of-network" }
            });

        await sut.SaveAsync(
            CreateContext(processId) with
            {
                RequiredStep = "StepB",
                TargetProcessorName = null,
                RequiredInformationRequest = null
            });

        var loaded = await sut.LoadAsync(processId);

        Assert.NotNull(loaded);
        Assert.Equal("StepB", loaded.RequiredStep);
        Assert.Null(loaded.RequiredInformationRequest);
    }

    [Fact]
    public async Task SaveAsync_WhenSavedTwice_SecondSaveWins()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var processId = Guid.NewGuid();

        await sut.SaveAsync(CreateContext(processId, ProcessExecutionState.Active));

        var updated = CreateContext(processId, ProcessExecutionState.Complete) with
        {
            RequiredStep = null,
            AvailableSteps = [],
            Steps = []
        };

        await sut.SaveAsync(updated);

        var loaded = await sut.LoadAsync(processId);

        Assert.NotNull(loaded);
        Assert.Equal(ProcessExecutionState.Complete, loaded.State);
        Assert.Null(loaded.RequiredStep);
        Assert.Empty(loaded.Steps);
        Assert.Empty(loaded.AvailableSteps);
    }

    [Fact]
    public async Task SaveAsync_WhenMultipleInstances_OnlyTargetedInstanceReturned()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var first = CreateContext(Guid.NewGuid());
        var second = CreateContext(Guid.NewGuid(), ProcessExecutionState.Complete);

        await sut.SaveAsync(first);
        await sut.SaveAsync(second);

        var loaded = await sut.LoadAsync(first.ProcessId);

        Assert.NotNull(loaded);
        Assert.Equal(first.ProcessId, loaded.ProcessId);
        Assert.Equal(ProcessExecutionState.Active, loaded.State);
    }
}
