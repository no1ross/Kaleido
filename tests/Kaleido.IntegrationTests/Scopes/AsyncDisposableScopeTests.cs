using System.ComponentModel;
using System.Net;
using System.Net.Http.Json;
using Kaleido.Http;
using Kaleido.Processor;
using Kaleido.Processor.Execution;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.IntegrationTests.Scopes;

/// <summary>
/// #523 through a real container: a step handler and a query source run in their own DI scope, and a
/// scoped dependency may implement only <see cref="IAsyncDisposable"/> (an async client, a pooled
/// context). A scope disposed synchronously throws before disposing it; disposed asynchronously, it is
/// disposed whatever the step or query did. Synchronous disposables are disposed as before.
/// </summary>
public sealed class AsyncDisposableScopeTests
{
    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(nameof(ScopedSucceedStep), "completed")]
    [InlineData(nameof(ScopedFailStep), "failed")]
    [InlineData(nameof(ScopedThrowStep), "exception")]
    public async Task AStepHandlersAsyncOnlyDependency_IsDisposed_WhateverTheStepDid(string step, string status)
    {
        await using var host = await ScopeHost.StartAsync();

        using var response = await host.PostAsync($"/scopes/processes/steps/{step}", new { processStep = new { } }, Cancel);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(status, (await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(Cancel)).GetProperty("status").GetString());
        Assert.Equal(1, host.Disposals.Async);
        Assert.Equal(1, host.Disposals.Sync);
    }

    [Fact]
    public async Task AStepHandlersAsyncOnlyDependency_IsDisposed_WhenTheCallerCancels()
    {
        await using var host = await ScopeHost.StartAsync();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Cancel);

        var call = host.PostAsync($"/scopes/processes/steps/{nameof(ScopedWaitStep)}", new { processStep = new { } }, cancellation.Token);
        await host.Disposals.Started.Task.WaitAsync(Cancel);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => call);

        // Bounded: a scope disposed synchronously never disposes the resource, which must fail, not hang.
        await host.Disposals.AsyncDisposed.Task.WaitAsync(TimeSpan.FromSeconds(10), Cancel);
        Assert.Equal(1, host.Disposals.Async);
    }

    [Theory]
    [InlineData(nameof(ScopedRecordSource), HttpStatusCode.OK)]
    [InlineData(nameof(ScopedFailingRecordSource), HttpStatusCode.InternalServerError)]
    public async Task AQuerySourcesAsyncOnlyDependency_IsDisposed_WhateverTheQueryDid(string source, HttpStatusCode expected)
    {
        await using var host = await ScopeHost.StartAsync();

        using var response = await host.PostAsync($"/scopes/queryable/{source}/query", new { }, Cancel);

        Assert.Equal(expected, response.StatusCode);
        Assert.Equal(1, host.Disposals.Async);
        Assert.Equal(1, host.Disposals.Sync);
    }

    private sealed class ScopeHost : IAsyncDisposable
    {
        private readonly IHost _host;

        private ScopeHost(IHost host, Disposals disposals)
        {
            _host = host;
            Disposals = disposals;
        }

        public Disposals Disposals { get; }

        public static async Task<ScopeHost> StartAsync()
        {
            var disposals = new Disposals();

            var host = await new HostBuilder()
                .ConfigureWebHost(web =>
                {
                    web.UseTestServer();
                    web.ConfigureServices(s =>
                    {
                        s.AddRouting();
                        s.AddKaleido(new ConfigurationBuilder().Build(), new()
                        {
                            ServiceName = "scopes",
                            Assemblies = [typeof(AsyncDisposableScopeTests).Assembly],
                            TypeFilter = type => type.Namespace == typeof(AsyncDisposableScopeTests).Namespace,
                            AuthorizationMode = KaleidoAuthorizationMode.None,
                        })
                            .AddInMemoryStores()
                            .AddHttp();

                        s.AddSingleton(disposals);
                        s.AddScoped<AsyncOnlyResource>();
                        s.AddScoped<SyncOnlyResource>();
                    });
                    web.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapKaleidoHttp());
                    });
                })
                .StartAsync(Cancel);

            return new ScopeHost(host, disposals);
        }

        public async Task<HttpResponseMessage> PostAsync(string url, object body, CancellationToken cancellationToken)
        {
            using var client = _host.GetTestClient();
            return await client.PostAsync(url, JsonContent.Create(body, options: KaleidoJsonOptions.Options), cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync(Cancel);
            _host.Dispose();
        }
    }
}

public sealed class Disposals
{
    private int _async;
    private int _sync;

    public int Async => Volatile.Read(ref _async);

    public int Sync => Volatile.Read(ref _sync);

    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public TaskCompletionSource AsyncDisposed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void DisposedAsync()
    {
        Interlocked.Increment(ref _async);
        AsyncDisposed.TrySetResult();
    }

    public void DisposedSync() => Interlocked.Increment(ref _sync);
}

// Disposable only asynchronously: a synchronous scope disposal throws for it.
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

[ProcessStep(DisplayName = "Succeed", Description = "Uses scoped resources and succeeds.")]
public sealed record ScopedSucceedStep : IProcessStep;

[ProcessStep(DisplayName = "Fail", Description = "Uses scoped resources and fails.")]
public sealed record ScopedFailStep : IProcessStep;

[ProcessStep(DisplayName = "Throw", Description = "Uses scoped resources and throws.")]
public sealed record ScopedThrowStep : IProcessStep;

[ProcessStep(DisplayName = "Wait", Description = "Uses scoped resources and waits until the caller gives up.")]
public sealed record ScopedWaitStep : IProcessStep;

public sealed class ScopedSucceedHandler(AsyncOnlyResource asyncResource, SyncOnlyResource syncResource) : IProcessStepHandler<ScopedSucceedStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(ScopedSucceedStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(asyncResource is not null && syncResource is not null ? ProcessStepHandlerResult.Success() : ProcessStepHandlerResult.Reject());
}

public sealed class ScopedFailHandler(AsyncOnlyResource asyncResource, SyncOnlyResource syncResource) : IProcessStepHandler<ScopedFailStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(ScopedFailStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        Task.FromResult(asyncResource is not null && syncResource is not null ? ProcessStepHandlerResult.Failure() : ProcessStepHandlerResult.Reject());
}

public sealed class ScopedThrowHandler(AsyncOnlyResource asyncResource, SyncOnlyResource syncResource) : IProcessStepHandler<ScopedThrowStep>
{
    public Task<ProcessStepHandlerResult> ExecuteAsync(ScopedThrowStep processStep, ProcessStepContext context, CancellationToken cancellationToken) =>
        throw new InvalidOperationException($"The handler failed with {asyncResource.GetType().Name} and {syncResource.GetType().Name}.");
}

public sealed class ScopedWaitHandler(AsyncOnlyResource asyncResource, Disposals disposals) : IProcessStepHandler<ScopedWaitStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(ScopedWaitStep processStep, ProcessStepContext context, CancellationToken cancellationToken)
    {
        disposals.Started.TrySetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return asyncResource is null ? ProcessStepHandlerResult.Reject() : ProcessStepHandlerResult.Success();
    }
}

public sealed record ScopedRecordContext : IQueryContext
{
    [Description("The record's code.")]
    [Filterable(FilterOperator.Equals)]
    public string Code { get; init; } = string.Empty;
}

[QuerySource(DisplayName = "Scoped records", Description = "Records read with scoped resources.")]
public sealed class ScopedRecordSource(AsyncOnlyResource asyncResource, SyncOnlyResource syncResource)
    : IDelegatedQuerySource<ScopedRecordContext, ScopedRecordContext>
{
    public Task<QueryResult<ScopedRecordContext>> ExecuteAsync(IQueryRequest<EmptyQueryViewParameters> request, CancellationToken cancellationToken) =>
        Task.FromResult(new QueryResult<ScopedRecordContext>(0, 0, 0, asyncResource is null || syncResource is null ? [new ScopedRecordContext()] : []));
}

[QuerySource(DisplayName = "Failing scoped records", Description = "Records whose read fails, with scoped resources.")]
public sealed class ScopedFailingRecordSource(AsyncOnlyResource asyncResource, SyncOnlyResource syncResource)
    : IDelegatedQuerySource<ScopedRecordContext, ScopedRecordContext>
{
    public Task<QueryResult<ScopedRecordContext>> ExecuteAsync(IQueryRequest<EmptyQueryViewParameters> request, CancellationToken cancellationToken) =>
        throw new InvalidOperationException($"The source failed with {asyncResource.GetType().Name} and {syncResource.GetType().Name}.");
}
