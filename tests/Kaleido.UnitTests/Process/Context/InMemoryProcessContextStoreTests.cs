using Kaleido.Process.Context;
using Kaleido.Process.Execution;
using System.Collections.Generic;
using Xunit;

namespace Kaleido.Process.UnitTests.Context;

public sealed class InMemoryProcessContextStoreTests
{
    [Fact]
    public async Task SaveAsync_WhenContextIsNull_Throws()
    {
        var store = new InMemoryProcessContextStore();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.SaveAsync(null!));
    }

    [Fact]
    public async Task SaveAsync_WhenContextProvided_SavesContext()
    {
        var store = new InMemoryProcessContextStore();
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor",
            State = ProcessExecutionState.Active
        };

        await store.SaveAsync(context, CancellationToken.None);

        var loaded = await store.LoadAsync(context.ProcessId, CancellationToken.None);
        
        Assert.NotNull(loaded);
        Assert.Equal(context.ProcessId, loaded.ProcessId);
        Assert.Equal("test-processor", loaded.ProcessorName);
        Assert.Equal(ProcessExecutionState.Active, loaded.State);
    }

    [Fact]
    public async Task LoadAsync_WhenContextNotExists_ReturnsNull()
    {
        var store = new InMemoryProcessContextStore();
        var processId = Guid.NewGuid();

        var loaded = await store.LoadAsync(processId, CancellationToken.None);
        
        Assert.Null(loaded);
    }

    [Fact]
    public async Task LoadAsync_WhenContextExists_ReturnsContext()
    {
        var store = new InMemoryProcessContextStore();
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor",
            State = ProcessExecutionState.Complete,
            Steps = new List<StepContext>
            {
                new StepContext
                {
                    StepName = "test-step",
                    Version = "1.0",
                    Status = StepExecutionStatus.Completed
                }
            }
        };

        await store.SaveAsync(context, CancellationToken.None);
        var loaded = await store.LoadAsync(context.ProcessId, CancellationToken.None);
        
        Assert.NotNull(loaded);
        Assert.Equal(context.ProcessId, loaded.ProcessId);
        Assert.Single(loaded.Steps);
        Assert.Equal("test-step", loaded.Steps.First().StepName);
    }

    [Fact]
    public async Task SaveAsync_WhenContextAlreadyExists_Overwrites()
    {
        var store = new InMemoryProcessContextStore();
        var processId = Guid.NewGuid();
        
        var context1 = new ProcessorContext
        {
            ProcessId = processId,
            ProcessorName = "test-processor",
            State = ProcessExecutionState.Active
        };

        var context2 = new ProcessorContext
        {
            ProcessId = processId,
            ProcessorName = "test-processor",
            State = ProcessExecutionState.Complete
        };

        await store.SaveAsync(context1, CancellationToken.None);
        await store.SaveAsync(context2, CancellationToken.None);
        
        var loaded = await store.LoadAsync(processId, CancellationToken.None);
        
        Assert.NotNull(loaded);
        Assert.Equal(ProcessExecutionState.Complete, loaded.State);
    }

    [Fact]
    public async Task SaveAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var store = new InMemoryProcessContextStore();
        var context = new ProcessorContext
        {
            ProcessId = Guid.NewGuid(),
            ProcessorName = "test-processor"
        };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.SaveAsync(context, cts.Token));
    }

    [Fact]
    public async Task LoadAsync_WhenCancelled_ThrowsOperationCanceled()
    {
        var store = new InMemoryProcessContextStore();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.LoadAsync(Guid.NewGuid(), cts.Token));
    }
}
