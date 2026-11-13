using System.ComponentModel;
using Kaleido.Process;
using Kaleido.Process.Execution;
using Kaleido.Process.Storage;
using Kaleido.Queryable;
using Kaleido.Queryable.Query;
using Kaleido.Server;
using Kaleido.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Hosting.FunctionalTests.Hosting;

public sealed class ConsoleHostingTests
{
    [Fact]
    public async Task ConsoleHost_StartsAProcessAndQueriesALocalSource_WithoutWebOrRegistry()
    {
        var state = new ConsoleProcessStore();
        var outbox = new ConsoleOutboxStore();
        using var host = Host.CreateDefaultBuilder()
            .UseDefaultServiceProvider(options =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .ConfigureServices(services =>
            {
                services.AddSingleton<IProcessContextStore>(state);
                services.AddSingleton<IKaleidoOutboxStore>(outbox);
                services.AddKaleido(new ConfigurationBuilder().Build(), new()
                {
                    ServiceName = "console-job",
                    ServiceVersion = "1",
                    Assemblies = [typeof(ConsoleHostingTests).Assembly],
                    TypeFilter = type => type == typeof(StartStep) || type == typeof(LocalSource),
                }).AddHosting(serverOptions: new() { AuthorizationMode = KaleidoAuthorizationMode.None });
                services.AddSingleton<ConsoleJob>();
                services.AddSingleton<IHostedService>(provider => provider.GetRequiredService<ConsoleJob>());
            })
            .Build();

        await host.StartAsync(TestContext.Current.CancellationToken);
        var job = host.Services.GetRequiredService<ConsoleJob>();

        Assert.NotNull(job.Process);
        Assert.Equal(StepExecutionStatus.Completed, Assert.Single(job.Process.Steps).ExecutionStatus);
        Assert.NotNull(await state.LoadAsync(new ProcessKey("console-job", "1", job.Process.ProcessId), TestContext.Current.CancellationToken));
        Assert.NotNull(job.Query);
        Assert.Equal(1, job.Query.TotalCount);
        Assert.Equal(7, Assert.Single(job.Query.Results).Id);
        Assert.NotEmpty(outbox.Events);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    public sealed class ConsoleJob(IServiceScopeFactory scopes) : IHostedService
    {
        public ProcessResult? Process { get; private set; }
        public QueryResult<LocalRecord>? Query { get; private set; }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await using var scope = scopes.CreateAsyncScope();
            Process = await scope.ServiceProvider.GetRequiredService<IProcessorRuntime>()
                .ExecuteAsync(ProcessRequest.ForStep(new StartStep()), cancellationToken);
            Query = await scope.ServiceProvider.GetRequiredService<IQueryableService>()
                .QueryAsync<LocalSource, LocalRecord>(new QueryRequest(), cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [ProcessStep(DisplayName = "Start console job", Description = "Starts the console-hosted test process.")]
    public sealed record StartStep : IProcessStep;

    public sealed class StartStepHandler : IProcessStepHandler<StartStep>
    {
        public Task<ProcessStepHandlerResult> ExecuteAsync(StartStep step, ProcessStepContext context, CancellationToken cancellationToken) =>
            Task.FromResult(ProcessStepHandlerResult.Success());
    }

    public sealed record LocalRecord : IQueryContext
    {
        [Description("The sample record's identifier.")]
        [Sortable]
        public int Id { get; init; }
    }

    [QuerySource(DisplayName = "Console records", Description = "Returns the sample record for a console job.")]
    public sealed class LocalSource : IQuerySource<LocalRecord>
    {
        public IQueryable<LocalRecord> CreateQuery(QueryExecutionContext executionContext) =>
            new[] { new LocalRecord { Id = 7 } }.AsQueryable();
    }
}
