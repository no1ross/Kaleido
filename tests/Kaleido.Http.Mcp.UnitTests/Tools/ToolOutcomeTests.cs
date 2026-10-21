using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class ToolOutcomeTests
    : Kaleido.UnitTests.SutFixture
{
    private static readonly CallToolResult Result = new() { Content = [new TextContentBlock { Text = "{}" }] };

    private static ToolOutcome CreateSut() => Result;

    [Fact]
    public void AFinishedResult_ConvertsToAnOutcomeWithoutAnInputRequest()
    {
        var outcome = CreateSut();

        Assert.Same(Result, outcome.Result);
        Assert.Null(outcome.InputRequired);
    }
}
