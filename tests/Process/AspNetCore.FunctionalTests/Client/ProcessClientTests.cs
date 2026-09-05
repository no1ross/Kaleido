using Kaleido.Process.AspNetCore.FunctionalTests.Fixtures;
using Kaleido.Process.AspNetCore.FunctionalTests.Infrastructure;

namespace Kaleido.Process.AspNetCore.FunctionalTests.Client;

[Collection(nameof(ProcessAspNetCoreCollection))]
public sealed class ProcessClientTests
{
    private readonly IKaleidoProcessClientFactory _factory;
    private readonly HttpClient _rawClient;

    public ProcessClientTests(ProcessAspNetCoreFixture fixture)
    {
        _factory = fixture.ClientFactory;
        _rawClient = fixture.Client;
    }

    // ---------------------------------------------------------------------------
    // GetRegistryAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetRegistryAsync_ReturnsProcessorWithSteps()
    {
        var registry = await _factory.GetClient("test").GetRegistryAsync();

        var processor = Assert.Single(registry, p => p.Name == "test-processor");
        Assert.NotEmpty(processor.Steps);
        Assert.Contains(processor.Steps, s => s.Name == RuntimeStepNames.Root);
    }

    [Fact]
    public async Task GetRegistryAsync_StepsContainExpectedUrls()
    {
        var registry = await _factory.GetClient("test").GetRegistryAsync();

        var processor = Assert.Single(registry);
        var step = Assert.Single(processor.Steps, s => s.Name == RuntimeStepNames.Root);
        Assert.NotEmpty(step.ExecuteUrl);
        Assert.NotEmpty(step.MetadataUrl);
        Assert.Contains("runtimeroot", step.ExecuteUrl, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------
    // GetStepMetadataAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetStepMetadataAsync_ReturnsStepDetails()
    {
        var metadata = await _factory.GetClient("test").GetStepMetadataAsync(RuntimeStepNames.Root);

        Assert.Equal(RuntimeStepNames.Root, metadata.Name);
        Assert.NotEmpty(metadata.ExecuteUrl);
        Assert.NotEmpty(metadata.MetadataUrl);
    }

    [Fact]
    public async Task GetStepMetadataAsync_WhenStepNotFound_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _factory.GetClient("test").GetStepMetadataAsync("NoSuchStep"));
    }

    // ---------------------------------------------------------------------------
    // GetProcessStateAsync
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task GetProcessStateAsync_WhenNotFound_ReturnsNull()
    {
        var result = await _factory.GetClient("test").GetProcessStateAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProcessStateAsync_AfterExecution_ReturnsState()
    {
        var processId = Guid.NewGuid();

        // Execute a step via the raw client to create process state
        await _rawClient.PostAsJsonAsync(
            "/kaleido/processes/steps/runtimeroot",
            new Kaleido.Process.AspNetCore.Contracts.ExecuteStepRequest<RuntimeRootStep>
            {
                ProcessId = processId,
                ProcessStep = new RuntimeRootStep()
            });

        var state = await _factory.GetClient("test").GetProcessStateAsync(processId);

        Assert.NotNull(state);
        Assert.Equal(processId, state!.ProcessId);
        Assert.Contains(state.Steps, s => s.StepName == RuntimeStepNames.Root);
    }

    // ---------------------------------------------------------------------------
    // ExecuteStepAsync (untyped)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_Untyped_ExecutesAndReturnsResponse()
    {
        var result = await _factory.GetClient("test").ExecuteStepAsync(new RuntimeRootStep());

        Assert.Equal(RuntimeStepNames.Root, result.StepName);
    }

    // ---------------------------------------------------------------------------
    // ExecuteStepAsync (typed)
    // ---------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteStepAsync_Typed_ReturnsTypedResult()
    {
        var result = await _factory.GetClient("test")
            .ExecuteStepAsync<RuntimeRootStep, RuntimeRootStepResponse>(new RuntimeRootStep());

        Assert.Equal(RuntimeStepNames.Root, result.StepName);
        Assert.NotNull(result.Result);
        Assert.Equal(RuntimeStepNames.Root, result.Result!.Value);
    }
}
