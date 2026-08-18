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
    public async Task GetCatalog_ReturnsInitialSteps()
    {
        var response =
            await _client.GetAsync("/kaleido/processes");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var contract =
            await response.Content.ReadAsync<ProcessCatalogRequest>();

        Assert.NotNull(contract);
        Assert.Contains(contract.InitialSteps, x => x.Name == RuntimeStepNames.Root);
        Assert.Contains(contract.InitialSteps, x => x.Name == RuntimeStepNames.RequiredRoot);
        Assert.Contains(contract.InitialSteps, x => x.Name == RuntimeStepNames.InvalidRequiredRoot);
    }

    [Fact]
    public async Task GetStepMetadata_ReturnsDependenciesAndLinks()
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
        Assert.Equal("/kaleido/processes/steps/runtimemerge", contract.ExecuteUrl);
        Assert.Equal("/kaleido/processes/steps/runtimemerge", contract.MetadataUrl);
    }
}
