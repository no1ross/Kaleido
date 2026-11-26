using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kaleido.Provider.InMemory.FunctionalTests;

public sealed class InMemoryProviderHostFunctionalTests
{
    [Fact]
    public async Task ExplicitMemoryChoice_StartsAComposedHostAndSharesRealStoresAcrossScopes()
    {
        using var host = Host.CreateDefaultBuilder()
            .UseEnvironment("Production")
            .UseDefaultServiceProvider((_, options) =>
            {
                options.ValidateOnBuild = true;
                options.ValidateScopes = true;
            })
            .ConfigureServices((context, services) => services.AddKaleido(context.Configuration, new()
            {
                ServiceName = "orders",
                ServiceVersion = "1",
                Assemblies = [typeof(Orders.ReviewStep).Assembly],
                TypeFilter = type => type.Namespace == typeof(Orders.ReviewStep).Namespace,
            }).AddHosting().AddInMemoryProviders())
            .Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        Guid processId;
        var now = host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        var admission = new IdempotencyRecord
        {
            Key = "orders:1:operation",
            ProcessorName = "orders",
            ProcessorVersion = "1",
            AdmissionId = Guid.NewGuid(),
            RequestHash = "one-operation",
            State = IdempotencyState.InProgress,
            LeaseUntilUtc = now.AddMinutes(5),
            ExpiresUtc = now.AddDays(1),
        };
        using (var first = host.Services.CreateScope())
        {
            var runtime = first.ServiceProvider.GetRequiredService<IProcessorRuntime>();
            var result = await runtime.ExecuteAsync(ProcessRequest.ForStep(new Orders.ReviewStep { OrderId = "A-1" }), TestContext.Current.CancellationToken);
            processId = result.ProcessId;
            Assert.Equal(StepExecutionStatus.Completed, Assert.Single(result.Steps).ExecutionStatus);
            Assert.Null(await first.ServiceProvider.GetRequiredService<IProcessIdempotencyStore>()
                .AdmitAsync(admission, TestContext.Current.CancellationToken));
        }

        using (var second = host.Services.CreateScope())
        {
            var state = await second.ServiceProvider.GetRequiredService<IProcessContextStore>()
                .LoadAsync(new ProcessKey("orders", "1", processId), TestContext.Current.CancellationToken);
            Assert.Equal(processId, Assert.IsType<ProcessContext>(state).ProcessId);
            Assert.Equal(StepExecutionStatus.Completed, Assert.Single(state.Steps, x => x.StepName == nameof(Orders.ReviewStep)).Status);
            var retry = await second.ServiceProvider.GetRequiredService<IProcessIdempotencyStore>()
                .AdmitAsync(admission with { AdmissionId = Guid.NewGuid() }, TestContext.Current.CancellationToken);
            Assert.Equal(admission.AdmissionId, Assert.IsType<IdempotencyRecord>(retry).AdmissionId);
            var events = await second.ServiceProvider.GetRequiredService<IKaleidoOutboxStore>()
                .ClaimEventsAsync(20, TimeSpan.FromMinutes(1), TestContext.Current.CancellationToken);
            Assert.NotEmpty(events);
        }

        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}
