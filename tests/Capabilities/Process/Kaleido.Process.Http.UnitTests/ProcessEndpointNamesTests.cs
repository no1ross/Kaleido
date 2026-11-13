namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessEndpointNamesTests
    : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void StepExecutionEndpointName_IsUniquePerStep()
    {
        Assert.Equal("KaleidoProcessStepExecute_Approve", ProcessEndpointNames.StepExecutionEndpointName("Approve"));
        Assert.NotEqual(
            ProcessEndpointNames.StepExecutionEndpointName("Approve"),
            ProcessEndpointNames.StepExecutionEndpointName("Reject"));
    }

    [Fact]
    public void Constants_NameTheExecuteAndStateEndpoints()
    {
        Assert.Equal("KaleidoProcessExecute", ProcessEndpointNames.ExecuteEndpointName);
        Assert.Equal("KaleidoProcessState", ProcessEndpointNames.ProcessEndpointName);
    }
}
