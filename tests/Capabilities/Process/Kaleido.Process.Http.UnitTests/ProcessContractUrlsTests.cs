namespace Kaleido.Http.Processor.UnitTests;

public sealed class ProcessContractUrlsTests : Kaleido.UnitTests.SutFixture
{
    [Theory]
    [InlineData("orders", "1", "/orders/1/processes")]
    [InlineData(" /orders/ ", " /2026-11/ ", "/orders/2026-11/processes")]
    public void ProcessesPrefix_AlwaysIncludesTheServiceVersion(string service, string version, string expected)
    {
        Assert.Equal(expected, ProcessContractUrls.ProcessesPrefix(service, version));
    }

    [Fact]
    public void VersionedRoutes_PinEveryProcessCallToItsServiceVersion()
    {
        var processId = Guid.NewGuid();

        Assert.Equal("/orders/2026-11/processes/steps/Approve", ProcessContractUrls.ExecuteStep("orders", "2026-11", "Approve"));
        Assert.Equal("/orders/2026-11/processes/execute", ProcessContractUrls.Execute("orders", "2026-11"));
        Assert.Equal($"/orders/2026-11/processes/{processId}", ProcessContractUrls.ProcessState("orders", "2026-11", processId));
        Assert.Equal("/orders/2026-11/processes/{processId}", ProcessContractUrls.ProcessStateTemplate("orders", "2026-11"));
    }

    [Fact]
    public void VersionedRoutes_RequireAServiceAndVersion()
    {
        Assert.Throws<ArgumentException>(() => ProcessContractUrls.ProcessesPrefix("", "1"));
        Assert.Throws<ArgumentException>(() => ProcessContractUrls.ProcessesPrefix("orders", ""));
    }
}
