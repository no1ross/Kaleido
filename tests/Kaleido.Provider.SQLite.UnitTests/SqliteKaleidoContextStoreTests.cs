using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using Kaleido.Observability;
using Kaleido.Processor.Context;
using Kaleido.Storage;
using Kaleido.Provider.SQLite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kaleido.Provider.SQLite.UnitTests;

// The reference implementation's fidelity tests double as the executable
// IKaleidoContextStore contract spec - a consumer-authored store should
// satisfy the same shape.
public sealed class SqliteKaleidoContextStoreTests
    : Kaleido.UnitTests.SutFixture
{
    private static SqliteKaleidoContextDbContext CreateDbContext(IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<SqliteKaleidoContextDbContext>()
            .UseSqlite("DataSource=:memory:");

        if (interceptor is not null)
        {
            builder.AddInterceptors(interceptor);
        }

        var context = new SqliteKaleidoContextDbContext(builder.Options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }

    private static SqliteKaleidoContextStore CreateSut(
        SqliteKaleidoContextDbContext dbContext,
        Guid? instanceId = null,
        ILogger<SqliteKaleidoContextStore>? logger = null) =>
        new(dbContext,
            new KaleidoServiceOptions { ServiceName = "test-svc", InstanceId = instanceId ?? Guid.NewGuid() },
            logger ?? NullLogger<SqliteKaleidoContextStore>.Instance);

    private static Mock<ILogger<SqliteKaleidoContextStore>> CreateLogger()
    {
        var logger = new Mock<ILogger<SqliteKaleidoContextStore>>();
        logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        return logger;
    }

    private static int LoggedCount(
        Mock<ILogger<SqliteKaleidoContextStore>> logger,
        LogLevel level) =>
        logger.Invocations.Count(x =>
            x.Method.Name == nameof(ILogger.Log) &&
            Equals(x.Arguments[0], level));

    private static ActivityListener ListenToTheStore(ConcurrentQueue<Activity> stopped)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SqliteTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = stopped.Enqueue,
        };

        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static Activity StoppedActivity(ConcurrentQueue<Activity> stopped, string name, Guid processId) =>
        Assert.Single(stopped, x =>
            x.OperationName == name &&
            Equals(x.GetTagItem(KaleidoTelemetryTags.ProcessId), processId.ToString()));

    private sealed class CancelOnQueryInterceptor(CancellationTokenSource cancellation)
        : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            cancellation.Cancel();
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

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
                    FirstExecuted = new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero),
                    StartedBy = Alice,
                    LastExecuted = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero),
                    LastUpdatedBy = SubjectOnly,
                },
                new StepContext
                {
                    StepName = "StepB",
                    Version = "1.0.0",
                    Status = StepExecutionStatus.Pending
                }
            ],
            CreatedUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            UpdatedUtc = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = Alice,
            UpdatedBy = SubjectOnly,
        };

    // The database refuses every INSERT or UPDATE of a process row (not a conflict: another failure).
    private static Task<int> RejectAsync(SqliteKaleidoContextDbContext dbContext, string operation) =>
        dbContext.Database.ExecuteSqlRawAsync(
            string.Concat("CREATE TRIGGER reject_", operation, " BEFORE ", operation, " ON ProcessContexts BEGIN SELECT RAISE(ABORT, 'rejected'); END;"));

    private static readonly KaleidoCaller Alice = new() { Name = "alice", Id = "sub-alice" };
    private static readonly KaleidoCaller SubjectOnly = new() { Id = "sub-42" };

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
        Assert.Equal(Alice, loaded.CreatedBy);
        Assert.Equal(SubjectOnly, loaded.UpdatedBy);
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
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero), stepA.FirstExecuted);
        Assert.Equal(Alice, stepA.StartedBy);
        Assert.Equal(SubjectOnly, stepA.LastUpdatedBy);

        var stepB = loaded.FindStep("StepB");
        Assert.NotNull(stepB);
        Assert.Equal(StepExecutionStatus.Pending, stepB.Status);
        Assert.Null(stepB.LatestRequestId);
        Assert.Null(stepB.LastExecuted);
        Assert.Null(stepB.StartedBy);
        Assert.Null(stepB.LastUpdatedBy);
    }

    [Fact]
    public async Task SaveAsync_AnUpdatedStep_StoresItsNewCallerTags()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var saved = await sut.SaveAsync(CreateContext(Guid.NewGuid()));
        var bob = new KaleidoCaller { Name = "bob" };
        var updated = saved with
        {
            UpdatedBy = bob,
            Steps = [.. saved.Steps.Select(x => x.StepName == "StepB" ? x with { StartedBy = bob, LastUpdatedBy = bob } : x)],
        };

        await sut.SaveAsync(updated);
        var loaded = await sut.LoadAsync(saved.ProcessId);

        Assert.Equal(bob, loaded!.UpdatedBy);
        Assert.Equal(Alice, loaded.CreatedBy);
        Assert.Equal(bob, loaded.FindStep("StepB")!.StartedBy);
        Assert.Equal(bob, loaded.FindStep("StepB")!.LastUpdatedBy);
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

        var saved = await sut.SaveAsync(
            CreateContext(processId) with
            {
                RequiredStep = "StepB",
                TargetProcessorName = null,
                RequiredInformationRequest = new InformationRequest { InformationRequestId = "out-of-network" }
            });

        await sut.SaveAsync(
            CreateContext(processId) with
            {
                Version = saved.Version,
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

        var saved = await sut.SaveAsync(CreateContext(processId, ProcessExecutionState.Active));

        var updated = CreateContext(processId, ProcessExecutionState.Complete) with
        {
            Version = saved.Version,
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

    [Fact]
    public async Task SaveAsync_ReturnsTheContextWithTheNextVersion_AndLoadReturnsIt()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var processId = Guid.NewGuid();

        var first = await sut.SaveAsync(CreateContext(processId));
        var second = await sut.SaveAsync(first with { State = ProcessExecutionState.Complete });

        Assert.Equal(1, first.Version);
        Assert.Equal(2, second.Version);
        Assert.Equal(2, (await sut.LoadAsync(processId))!.Version);
    }

    [Fact]
    public async Task SaveAsync_WithAStaleVersion_ThrowsAConflictAndKeepsTheStoredState()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var loaded = await sut.SaveAsync(CreateContext(Guid.NewGuid()));

        await sut.SaveAsync(loaded with { State = ProcessExecutionState.Complete });

        var exception = await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() =>
            sut.SaveAsync(loaded with { State = ProcessExecutionState.Exception }));

        Assert.Equal(loaded.ProcessId, exception.ProcessId);
        Assert.Equal(ProcessExecutionState.Complete, (await sut.LoadAsync(loaded.ProcessId))!.State);
    }

    [Fact]
    public async Task SaveAsync_ANewProcessWhoseIdAlreadyExists_ThrowsAConflict()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var context = CreateContext(Guid.NewGuid());
        await sut.SaveAsync(context);

        await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() => sut.SaveAsync(context));
    }

    // Two service instances (separate DbContexts) on one database: both read version 1, the first
    // save wins, the second is refused instead of overwriting it.
    [Fact]
    public async Task SaveAsync_FromTwoInstancesOnOneDatabase_TheSecondIsRefused()
    {
        var connectionString = $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var first = CreateSharedDbContext(connectionString);
        using var second = CreateSharedDbContext(connectionString);
        var processId = Guid.NewGuid();

        await CreateSut(first).SaveAsync(CreateContext(processId));
        var loadedByFirst = await CreateSut(first).LoadAsync(processId);
        var loadedBySecond = await CreateSut(second).LoadAsync(processId);

        await CreateSut(first).SaveAsync(loadedByFirst! with { State = ProcessExecutionState.Complete });

        await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() =>
            CreateSut(second).SaveAsync(loadedBySecond! with { State = ProcessExecutionState.Exception }));
    }

    private static SqliteKaleidoContextDbContext CreateSharedDbContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<SqliteKaleidoContextDbContext>()
            .UseSqlite(connectionString)
            .Options;

        var context = new SqliteKaleidoContextDbContext(options);
        context.Database.OpenConnection();
        context.Database.EnsureCreated();
        return context;
    }


    [Fact]
    public async Task LoadAsync_And_SaveAsync_TraceTheProcessId()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = ListenToTheStore(stopped);
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var context = CreateContext(Guid.NewGuid());

        await sut.SaveAsync(context);
        await sut.LoadAsync(context.ProcessId);

        Assert.Equal(ActivityStatusCode.Unset, StoppedActivity(stopped, SqliteTelemetry.SaveActivityName, context.ProcessId).Status);
        Assert.Equal(ActivityStatusCode.Unset, StoppedActivity(stopped, SqliteTelemetry.LoadActivityName, context.ProcessId).Status);
    }

    [Fact]
    public async Task LoadAsync_WhenTheDatabaseFails_LogsAnErrorMarksTheActivityAndRethrows()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = ListenToTheStore(stopped);
        using var dbContext = CreateDbContext();
        var logger = CreateLogger();
        var sut = CreateSut(dbContext, logger: logger.Object);
        var processId = Guid.NewGuid();
        await dbContext.Database.ExecuteSqlRawAsync("DROP TABLE ProcessContexts");

        await Assert.ThrowsAnyAsync<DbException>(() => sut.LoadAsync(processId));

        Assert.Equal(1, LoggedCount(logger, LogLevel.Error));
        Assert.Equal(ActivityStatusCode.Error, StoppedActivity(stopped, SqliteTelemetry.LoadActivityName, processId).Status);
    }

    [Fact]
    public async Task LoadAsync_WhenCanceledDuringTheQuery_PropagatesTheCancellationWithoutLoggingAFailure()
    {
        using var cancellation = new CancellationTokenSource();
        using var dbContext = CreateDbContext(new CancelOnQueryInterceptor(cancellation));
        var logger = CreateLogger();
        var sut = CreateSut(dbContext, logger: logger.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.LoadAsync(Guid.NewGuid(), cancellation.Token));

        Assert.Equal(0, LoggedCount(logger, LogLevel.Error));
    }

    [Fact]
    public async Task LoadAsync_And_SaveAsync_WhenAlreadyCanceled_ThrowBeforeTouchingTheStore()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var context = CreateContext(Guid.NewGuid());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.LoadAsync(context.ProcessId, new CancellationToken(true)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SaveAsync(context, new CancellationToken(true)));
        Assert.Null(await sut.LoadAsync(context.ProcessId));
    }

    [Fact]
    public async Task SaveAsync_WhenCanceledDuringTheSave_PropagatesTheCancellationWithoutLoggingAFailure()
    {
        using var cancellation = new CancellationTokenSource();
        using var dbContext = CreateDbContext(new CancelOnQueryInterceptor(cancellation));
        var logger = CreateLogger();
        var sut = CreateSut(dbContext, logger: logger.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.SaveAsync(CreateContext(Guid.NewGuid()), cancellation.Token));

        Assert.Equal(0, LoggedCount(logger, LogLevel.Error));
    }

    [Fact]
    public async Task SaveAsync_WhenTheDatabaseRejectsAnUpdate_LogsAnErrorAndKeepsTheStoredState()
    {
        var stopped = new ConcurrentQueue<Activity>();
        using var listener = ListenToTheStore(stopped);
        using var dbContext = CreateDbContext();
        var logger = CreateLogger();
        var sut = CreateSut(dbContext, logger: logger.Object);
        var saved = await sut.SaveAsync(CreateContext(Guid.NewGuid()));
        dbContext.ChangeTracker.Clear();
        await RejectAsync(dbContext, "UPDATE");

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.SaveAsync(saved with { State = ProcessExecutionState.Complete }));

        Assert.Equal(1, LoggedCount(logger, LogLevel.Error));
        Assert.Contains(stopped, x => x.OperationName == SqliteTelemetry.SaveActivityName && x.Status == ActivityStatusCode.Error);
        var loaded = await sut.LoadAsync(saved.ProcessId);
        Assert.NotNull(loaded);
        Assert.Equal(1, loaded.Version);
        Assert.Equal(ProcessExecutionState.Active, loaded.State);
    }

    [Fact]
    public async Task SaveAsync_ANewProcessTheDatabaseRejectsForAnotherReason_IsAFailureNotAConflict()
    {
        using var dbContext = CreateDbContext();
        var logger = CreateLogger();
        var sut = CreateSut(dbContext, logger: logger.Object);
        await RejectAsync(dbContext, "INSERT");
        var newProcess = CreateContext(Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => sut.SaveAsync(newProcess));

        Assert.IsNotType<Kaleido.Exceptions.KaleidoConcurrencyException>(exception);
        Assert.Equal(1, LoggedCount(logger, LogLevel.Error));
        dbContext.ChangeTracker.Clear();
        Assert.Null(await sut.LoadAsync(newProcess.ProcessId));
    }

    [Fact]
    public async Task SaveAsync_ANewProcessCreatedConcurrently_IsAConflict()
    {
        using var dbContext = CreateDbContext(new InsertProcessTwiceInterceptor());
        var logger = CreateLogger();
        var sut = CreateSut(dbContext, logger: logger.Object);

        var exception = await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() => sut.SaveAsync(CreateContext(Guid.NewGuid())));

        Assert.IsType<DbUpdateException>(exception.InnerException);
        Assert.Equal(0, LoggedCount(logger, LogLevel.Error));
    }

    // Runs the process INSERT once more just before EF does: the same process created concurrently.
    private sealed class InsertProcessTwiceInterceptor : DbCommandInterceptor
    {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO \"ProcessContexts\"", StringComparison.Ordinal))
            {
                await using var duplicate = command.Connection!.CreateCommand();
                duplicate.Transaction = command.Transaction;
                duplicate.CommandText = command.CommandText.Split(';')[0];

                foreach (DbParameter parameter in command.Parameters)
                {
                    var copy = duplicate.CreateParameter();
                    copy.ParameterName = parameter.ParameterName;
                    copy.Value = parameter.Value;
                    duplicate.Parameters.Add(copy);
                }

                await duplicate.ExecuteNonQueryAsync(cancellationToken);
            }

            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    [Fact]
    public async Task SaveAsync_WhenTheRowChangedAfterItWasRead_ThrowsAConflictFromTheConditionalUpdate()
    {
        var connectionString = $"DataSource=file:{Guid.NewGuid():N}?mode=memory&cache=shared";
        using var first = CreateSharedDbContext(connectionString);
        using var second = CreateSharedDbContext(connectionString);
        var processId = Guid.NewGuid();
        var savedBySecond = await CreateSut(second).SaveAsync(CreateContext(processId));
        var loadedByFirst = await CreateSut(first).LoadAsync(processId);
        await CreateSut(first).SaveAsync(loadedByFirst! with { State = ProcessExecutionState.Complete });

        var exception = await Assert.ThrowsAsync<Kaleido.Exceptions.KaleidoConcurrencyException>(() =>
            CreateSut(second).SaveAsync(savedBySecond with { State = ProcessExecutionState.Exception }));

        Assert.IsType<DbUpdateConcurrencyException>(exception.InnerException);
        Assert.Equal(ProcessExecutionState.Complete, (await CreateSut(first).LoadAsync(processId))!.State);
    }

    [Fact]
    public async Task SaveAsync_WithoutTimestamps_StampsTheCurrentTime()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var context = CreateContext(Guid.NewGuid()) with { CreatedUtc = default, UpdatedUtc = default };
        var before = DateTimeOffset.UtcNow;

        await sut.SaveAsync(context);

        var after = DateTimeOffset.UtcNow;
        var loaded = await sut.LoadAsync(context.ProcessId);
        Assert.NotNull(loaded);
        Assert.InRange(loaded.CreatedUtc, before, after);
        Assert.InRange(loaded.UpdatedUtc, before, after);
    }

    [Fact]
    public async Task SaveAsync_ANewProcessWithoutCallersOrARequiredStep_LoadsThemAsNull()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);
        var context = CreateContext(Guid.NewGuid()) with
        {
            CreatedBy = null,
            UpdatedBy = null,
            RequiredStep = null,
            TargetProcessorName = null
        };

        await sut.SaveAsync(context);

        var loaded = await sut.LoadAsync(context.ProcessId);
        Assert.NotNull(loaded);
        Assert.Null(loaded.CreatedBy);
        Assert.Null(loaded.UpdatedBy);
        Assert.Null(loaded.RequiredStep);
        Assert.Null(loaded.TargetProcessorName);
        Assert.Null(loaded.RequiredInformationRequest);
    }

    [Fact]
    public async Task SaveAsync_WhenDebugLoggingIsEnabled_LogsTheSave()
    {
        using var dbContext = CreateDbContext();
        var logger = CreateLogger();
        var sut = CreateSut(dbContext, logger: logger.Object);

        await sut.SaveAsync(CreateContext(Guid.NewGuid()));

        Assert.Equal(1, LoggedCount(logger, LogLevel.Debug));
        Assert.Equal(0, LoggedCount(logger, LogLevel.Error));
    }

    [Fact]
    public async Task Writes_WithNullArguments_Throw()
    {
        using var dbContext = CreateDbContext();
        var sut = CreateSut(dbContext);

        await Assert.ThrowsAsync<ArgumentNullException>(() => sut.SaveAsync(null!));
    }
}
