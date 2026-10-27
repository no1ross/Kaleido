using Kaleido.Exceptions;

namespace Kaleido.Http.Mcp.UnitTests;

public sealed class KaleidoMcpServerInstructionsTests
    : Kaleido.UnitTests.SutFixture
{
    [Fact]
    public void Resolve_UsesThePackagedDefaultInstructions()
    {
        var text = KaleidoMcpServerInstructions.Resolve(new KaleidoMcpOptions()).Text;

        Assert.Contains("informationRequest", text, StringComparison.Ordinal);
        Assert.Contains("Never answer on the user's behalf", text, StringComparison.Ordinal);
        Assert.DoesNotContain("call_tool", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_AppendsAdditionalInstructions_AsGiven()
    {
        var text = KaleidoMcpServerInstructions.Resolve(new KaleidoMcpOptions { AdditionalInstructions = "Start with a member search." }).Text;

        Assert.Contains("informationRequest", text, StringComparison.Ordinal);
        Assert.EndsWith("\n\nStart with a member search.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Resolve_Instructions_ReplaceTheDefaults()
    {
        Assert.Equal("Only these.", KaleidoMcpServerInstructions.Resolve(new KaleidoMcpOptions { Instructions = "Only these." }).Text);
    }

    [Fact]
    public void Resolve_InstructionsFile_ReplacesTheDefaults()
    {
        var path = Path.Join(Path.GetTempPath(), $"kaleido-mcp-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "From the file.\n");

        try
        {
            Assert.Equal("From the file.", KaleidoMcpServerInstructions.Resolve(new KaleidoMcpOptions { InstructionsFile = path }).Text);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Resolve_AMissingInstructionsFile_FailsWithItsCode()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(
            () => KaleidoMcpServerInstructions.Resolve(new KaleidoMcpOptions { InstructionsFile = "does-not-exist.md" }));

        Assert.Equal(McpErrorCodes.MissingInstructionsFile, exception.Code);
    }

    [Fact]
    public void Resolve_FrontDoor_AppendsItsInstructions_BeforeTheAdditionalOnes()
    {
        var text = KaleidoMcpServerInstructions.Resolve(
            new KaleidoMcpOptions { ToolListing = McpToolListing.FrontDoor, AdditionalInstructions = "Start with the intake." }).Text;

        Assert.Contains("call_tool", text, StringComparison.Ordinal);
        Assert.True(text.IndexOf("call_tool", StringComparison.Ordinal) < text.IndexOf("Start with the intake.", StringComparison.Ordinal));
    }
}
