using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Hosting.Http.FunctionalTests.Scopes;

public sealed class AsyncDisposableScopeFunctionalTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(nameof(ScopedSucceedStep), StepExecutionStatus.Completed)]
    [InlineData(nameof(ScopedFailStep), StepExecutionStatus.Failed)]
    [InlineData(nameof(ScopedThrowStep), StepExecutionStatus.Exception)]
    public async Task AStepHandlersAsyncOnlyDependency_IsDisposed_WhateverTheStepDid(string step, StepExecutionStatus expected)
    {
        await using var host = await StartAsync();
        using var client = host.GetTestClient();

        using var response = await client.PostAsJsonAsync($"/scopes/1/processes/steps/{step.ToLowerInvariant()}", new { processStep = new { } }, KaleidoJsonOptions.Options, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = Assert.IsType<ProcessResult>(await response.Content.ReadFromJsonAsync<ProcessResult>(KaleidoJsonOptions.Options, Cancel));
        Assert.Equal(expected, Assert.Single(result.Steps).ExecutionStatus);
        Assert.Equal(1, host.Services.GetRequiredService<Disposals>().Async);
        Assert.Equal(1, host.Services.GetRequiredService<Disposals>().Sync);
    }

    [Fact]
    public async Task AStepHandlersAsyncOnlyDependency_IsDisposed_WhenTheCallerCancels()
    {
        await using var host = await StartAsync();
        using var client = host.GetTestClient();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancel);
        var disposals = host.Services.GetRequiredService<Disposals>();

        var call = client.PostAsJsonAsync("/scopes/1/processes/steps/scopedwaitstep", new { processStep = new { } }, KaleidoJsonOptions.Options, cancellation.Token);
        await disposals.Started.Task.WaitAsync(Cancel);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);
        await disposals.AsyncDisposed.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        Assert.Equal(1, disposals.Async);
    }

    [Theory]
    [InlineData("scopedrecordsource", HttpStatusCode.OK)]
    [InlineData("scopedfailingrecordsource", HttpStatusCode.InternalServerError)]
    public async Task AQuerySourcesAsyncOnlyDependency_IsDisposed_WhateverTheQueryDid(string source, HttpStatusCode expected)
    {
        await using var host = await StartAsync();
        using var client = host.GetTestClient();

        using var response = await client.PostAsJsonAsync($"/scopes/1/queryable/{source}/query", new { query = new { } }, KaleidoJsonOptions.Options, Cancel);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(1, host.Services.GetRequiredService<Disposals>().Async);
        Assert.Equal(1, host.Services.GetRequiredService<Disposals>().Sync);
    }

    private static async Task<WebApplication> StartAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<Disposals>();
        builder.Services.AddScoped<AsyncOnlyResource>();
        builder.Services.AddScoped<SyncOnlyResource>();
        builder.Services.AddKaleido(builder.Configuration, new()
        {
            ServiceName = "scopes",
            ServiceVersion = "1",
            Assemblies = [typeof(AsyncDisposableScopeFunctionalTests).Assembly],
            TypeFilter = type => type.Namespace == typeof(AsyncDisposableScopeFunctionalTests).Namespace,
        })
            .AddHosting(serverOptions: new() { AuthorizationMode = KaleidoAuthorizationMode.None })
            .AddInMemoryProviders()
            .AddHttp()
            .AddProcessHttpServer();
        var app = builder.Build();
        app.MapKaleido().MapProcess().MapQueryable();
        await app.StartAsync(Cancel);
        return app;
    }
}

public sealed class Disposals
{
    private int asyncCount;
    private int syncCount;
    public int Async => Volatile.Read(ref asyncCount);
    public int Sync => Volatile.Read(ref syncCount);
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource AsyncDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public void DisposedAsync()
    {
        Interlocked.Increment(ref asyncCount);
        AsyncDisposed.TrySetResult();
    }
    public void DisposedSync() => Interlocked.Increment(ref syncCount);
}

public sealed class AsyncOnlyResource(Disposals disposals) : IAsyncDisposable
{
    public ValueTask DisposeAsync()
    {
        disposals.DisposedAsync();
        return ValueTask.CompletedTask;
    }
}

public sealed class SyncOnlyResource(Disposals disposals) : IDisposable
{
    public void Dispose() => disposals.DisposedSync();
}

[ProcessStep(DisplayName = "Succeed", Description = "Uses scoped resources and succeeds")]
public sealed record ScopedSucceedStep : IProcessStep;

[ProcessStep(DisplayName = "Fail", Description = "Uses scoped resources and fails")]
public sealed record ScopedFailStep : IProcessStep;

[ProcessStep(DisplayName = "Throw", Description = "Uses scoped resources and throws")]
public sealed record ScopedThrowStep : IProcessStep;

[ProcessStep(DisplayName = "Wait", Description = "Uses scoped resources until the caller cancels")]
public sealed record ScopedWaitStep : IProcessStep;

public sealed class ScopedSucceedHandler(AsyncOnlyResource asyncResource, SyncOnlyResource syncResource) : IProcessStepHandler<ScopedSucceedStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(ScopedSucceedStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(asyncResource is not null && syncResource is not null ? ProcessStepHandlerResult.Success() : ProcessStepHandlerResult.Reject());
}

public sealed class ScopedFailHandler(AsyncOnlyResource asyncResource, SyncOnlyResource syncResource) : IProcessStepHandler<ScopedFailStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(ScopedFailStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(asyncResource is not null && syncResource is not null ? ProcessStepHandlerResult.Failure() : ProcessStepHandlerResult.Reject());
}

public sealed class ScopedThrowHandler(AsyncOnlyResource asyncResource, SyncOnlyResource syncResource) : IProcessStepHandler<ScopedThrowStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(ScopedThrowStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
        throw new InvalidOperationException($"The handler failed with {asyncResource.GetType().Name} and {syncResource.GetType().Name}.");
}

public sealed class ScopedWaitHandler(AsyncOnlyResource asyncResource, Disposals disposals) : IProcessStepHandler<ScopedWaitStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(ScopedWaitStep step, ProcessStepContext context, CancellationToken cancellationToken)
    {
        disposals.Started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return asyncResource is null ? ProcessStepHandlerResult.Reject() : ProcessStepHandlerResult.Success();
    }
}

public sealed record ScopedRecordContext : IQueryContext
{
    [Description("The record's code")]
    [Filterable(FilterOperator.Equals)]
    public string Code { get; init; } = string.Empty;
}

[QuerySource(DisplayName = "Scoped records", Description = "Records read with scoped resources")]
public sealed class ScopedRecordSource(AsyncOnlyResource asyncResource, SyncOnlyResource syncResource) : IDelegatedQuerySource<ScopedRecordContext, ScopedRecordContext>
{
    public Task<QueryResult<ScopedRecordContext>> ExecuteAsync(IQueryRequest<EmptyQueryViewParameters> request, CancellationToken cancellationToken) =>
        Task.FromResult(new QueryResult<ScopedRecordContext>(0, 0, 0, asyncResource is null || syncResource is null ? [new ScopedRecordContext()] : []));
}

[QuerySource(DisplayName = "Failing scoped records", Description = "Records whose read fails with scoped resources")]
public sealed class ScopedFailingRecordSource(AsyncOnlyResource asyncResource, SyncOnlyResource syncResource) : IDelegatedQuerySource<ScopedRecordContext, ScopedRecordContext>
{
    public Task<QueryResult<ScopedRecordContext>> ExecuteAsync(IQueryRequest<EmptyQueryViewParameters> request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException($"The source failed with {asyncResource.GetType().Name} and {syncResource.GetType().Name}.");
}
