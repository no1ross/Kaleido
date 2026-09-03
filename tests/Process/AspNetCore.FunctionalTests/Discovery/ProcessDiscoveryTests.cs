using Kaleido.Process.AspNetCore.Contracts;
using Kaleido.Process.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Process.AspNetCore.FunctionalTests.Infrastructure;
using System.Net;

namespace Kaleido.Process.AspNetCore.FunctionalTests.Discovery;

[Collection(nameof(ProcessAspNetCoreCollection))]
public sealed class ProcessDiscoveryTests
{
    private readonly HttpClient _client;

    public ProcessDiscoveryTests(ProcessAspNetCoreFixture fixture)
    {
        _client = fixture.Client;
    }

    [Fact]
    public async Task GetCatalog_ReturnsProcessorsWithInitialSteps()
    {
        var response =
            await _client.GetAsync("/kaleido/processes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<ProcessCatalogResponse>();

        Assert.NotNull(contract);

        var processor =
            Assert.Single(contract.Processors);

        Assert.Equal("test-processor", processor.Name);
        Assert.Contains(processor.InitialSteps, x => x.Name == RuntimeStepNames.Root);
        Assert.Contains(processor.InitialSteps, x => x.Name == RuntimeStepNames.RequiredRoot);
        Assert.Contains(processor.InitialSteps, x => x.Name == RuntimeStepNames.InvalidRequiredRoot);
    }

    [Fact]
    public async Task GetStepMetadata_ReturnsDependenciesLinksAndResultMetadata()
    {
        var response =
            await _client.GetAsync("/kaleido/processes/steps/runtimemerge");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<ProcessStepResponse>();

        Assert.NotNull(contract);
        Assert.Equal(RuntimeStepNames.Merge, contract.Name);
        Assert.Equal(2, contract.Dependencies.Count);
        Assert.Contains(contract.Dependencies, x => x.Name == RuntimeStepNames.StepA);
        Assert.Contains(contract.Dependencies, x => x.Name == RuntimeStepNames.StepB);
        Assert.NotNull(contract.Result);
        Assert.NotEmpty(contract.Result!.OutputFields);
        Assert.Equal("/kaleido/processes/steps/runtimemerge", contract.ExecuteUrl);
        Assert.Equal("/kaleido/processes/steps/runtimemerge/metadata", contract.MetadataUrl);
    }
}
