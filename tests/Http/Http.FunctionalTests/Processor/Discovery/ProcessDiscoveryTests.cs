using System.Net;
using Kaleido.Http.FunctionalTests.Processor.Fixtures;
using Kaleido.Http.FunctionalTests.Processor.Infrastructure;
using Kaleido.Platform.Registry;

namespace Kaleido.Http.FunctionalTests.Processor.Discovery;

[Collection(nameof(ProcessorAspNetCoreSuite))]
public sealed class ProcessDiscoveryTests(ProcessorAspNetCoreFixture fixture)
{
    private readonly HttpClient _client = fixture.RegistryClient;

    [Fact]
    public async Task GetRegistry_ReturnsProcessorsWithInitialSteps()
    {
        var response =
            await _client.GetAsync("/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<KaleidoRegistry>();

        Assert.NotNull(contract);

        var processor =
            Assert.Single(contract.Processes);

        Assert.Equal(FunctionalProcessorNames.TestProcessor, processor.Name);
        Assert.Contains(processor.InitialSteps, x => x.Name == RuntimeStepNames.Root);
        Assert.Contains(processor.InitialSteps, x => x.Name == RuntimeStepNames.RequiredRoot);
        Assert.Contains(processor.InitialSteps, x => x.Name == RuntimeStepNames.InvalidRequiredRoot);

        var root = Assert.Single(processor.InitialSteps, x => x.Name == RuntimeStepNames.Root);
        Assert.Equal(Assert.Single(processor.Steps!, x => x.Name == RuntimeStepNames.Root).ExecuteUrl, root.ExecuteUrl);
        Assert.NotEmpty(root.ExecuteUrl);
    }

    [Fact]
    public async Task AService_MapsOnlyItsVersionedStepRoute()
    {
        using var versioned = await fixture.Client.PostAsJsonAsync("/kaleido/1/processes/steps/runtimerootstep", new { processStep = new { } });
        using var unversioned = await fixture.Client.PostAsJsonAsync("/kaleido/processes/steps/runtimerootstep", new { processStep = new { } });

        Assert.Equal(HttpStatusCode.OK, versioned.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unversioned.StatusCode);
    }

    [Fact]
    public async Task GetRegistry_StepRecordsCarryDependenciesLinksAndResultMetadata()
    {
        var response =
            await _client.GetAsync("/registry");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var registry =
            await response.Content.ReadAsync<KaleidoRegistry>();

        var processor = Assert.Single(registry!.Processes);
        var contract = Assert.Single(processor.Steps!, s => s.Name == RuntimeStepNames.Merge);

        Assert.Equal(2, contract.Dependencies.Count);
        Assert.Contains(contract.Dependencies, x => x.Name == RuntimeStepNames.StepA);
        Assert.Contains(contract.Dependencies, x => x.Name == RuntimeStepNames.StepB);
        Assert.NotNull(contract.Result);
        Assert.NotEmpty(contract.Result!.OutputFields);
        Assert.Equal("/kaleido/1/processes/steps/runtimemergestep", contract.ExecuteUrl);
    }
}
