namespace Kaleido.Process.Http.Server.UnitTests;

public sealed class ProcessRoutePathsTests
    : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void ExecuteStep_ReturnsTheStepRouteRelativeToTheProcessesGroup()
    {
        Assert.Equal("steps/Approve", ProcessRoutePaths.ExecuteStep("Approve"));
    }

    [Fact]
    public void Constants_AreTheProcessAndExecuteRoutes()
    {
        Assert.Equal("{processId}", ProcessRoutePaths.Process);
        Assert.Equal("execute", ProcessRoutePaths.Execute);
    }
}
