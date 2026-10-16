namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessContractUrlsTests
    : Kaleido.UnitTests.SutFixture
{
    [Theory]
    [InlineData(null, "/processes")]
    [InlineData("", "/processes")]
    [InlineData("   ", "/processes")]
    [InlineData("orders", "/orders/processes")]
    [InlineData(" /orders/ ", "/orders/processes")]
    [InlineData("/orders", "/orders/processes")]
    public void ProcessesPrefix_NormalizesTheServiceName(string? serviceName, string expected)
    {
        Assert.Equal(expected, ProcessContractUrls.ProcessesPrefix(serviceName!));
    }

    [Fact]
    public void ExecuteStep_AppendsTheStepRouteToThePrefix()
    {
        Assert.Equal("/orders/processes/steps/Approve", ProcessContractUrls.ExecuteStep("orders", "Approve"));
    }

    [Fact]
    public void Execute_WithoutAServiceName_UsesTheRootPrefix()
    {
        Assert.Equal("/processes/execute", ProcessContractUrls.Execute(""));
    }

    [Fact]
    public void ProcessState_AppendsTheProcessIdToThePrefix()
    {
        var processId = Guid.NewGuid();

        Assert.Equal($"/orders/processes/{processId}", ProcessContractUrls.ProcessState("orders", processId));
    }
}
