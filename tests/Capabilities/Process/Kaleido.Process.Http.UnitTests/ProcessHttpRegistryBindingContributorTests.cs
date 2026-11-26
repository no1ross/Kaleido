using Kaleido.Registry;

namespace Kaleido.Process.Http.UnitTests;

public sealed class ProcessHttpRegistryBindingContributorTests : Kaleido.UnitTests.SutFixture
{
    private static ProcessHttpRegistryBindingContributor CreateSut() => new();

    [Fact]
    public void Bind_AddsOnlyHttpOperationsToThePublishedProcessAndSteps()
    {
        var published = new ProcessRegistryItem
        {
            Name = "orders",
            ServiceName = "orders",
            Description = "Order fulfillment",
            InitialSteps = [new ProcessStepSummary { Name = "Approve" }],
            Steps = [new ProcessStepResponse { Name = "Approve" }],
        };
        var service = new KaleidoServiceOptions { ServiceName = "orders", ServiceVersion = "2026-11" };

        var result = CreateSut().Bind(published, service);

        Assert.Equal("Order fulfillment", result.Description);
        Assert.Equal(["execute", "state"], result.Bindings.Http.Select(x => x.Operation));
        Assert.Equal(["POST", "GET"], result.Bindings.Http.Select(x => x.Method));
        Assert.Equal(["/orders/2026-11/processes/execute", "/orders/2026-11/processes/{processId}"], result.Bindings.Http.Select(x => x.Path));
        Assert.Equal("/orders/2026-11/processes/steps/approve", Assert.Single(Assert.Single(result.InitialSteps).Bindings.Http).Path);
        Assert.Equal("/orders/2026-11/processes/steps/approve", Assert.Single(Assert.Single(result.Steps!).Bindings.Http).Path);
        Assert.Empty(published.Bindings.Http);
        Assert.Empty(Assert.Single(published.Steps!).Bindings.Http);
    }
}
