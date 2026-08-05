using Kaleido.Process.FunctionalTests.Assets.Runtime;
using Kaleido.Process.FunctionalTests.Fixtures;
using Kaleido.Process.Participant;
using Kaleido.Process.Participant.Execution;
using Kaleido.Process.Participant.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Kaleido.Process.FunctionalTests.Tests.Runtime;

[Collection(nameof(RuntimeTestCollection))]
public sealed class RuntimeAutoContinuationTests
{
    private readonly IParticipantRuntime _runtime;

    public RuntimeAutoContinuationTests(RuntimeTestFixture fixture)
    {
        _runtime = fixture.ServiceProvider.GetRequiredService<IParticipantRuntime>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenAllDependentStepsAreSupplied_ContinuesUntilProcessCompletes()
    {
        var result = await _runtime.ExecuteAsync(
            RuntimeRequestFactory.Create(
                Guid.NewGuid().ToString("N"),
                "request-1",
                RuntimeStepNames.Root,
                RuntimeStepNames.StepA,
                RuntimeStepNames.StepB,
                RuntimeStepNames.Merge));

        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.Root);
        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.StepA);
        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.StepB);
        RuntimeResultAssert.StepCompleted(result, RuntimeStepNames.Merge);

        Assert.DoesNotContain(
            result.Steps,
            x => x.Decision == ExecutionDecisionType.ProcessViolation);
    }
}
