using Kaleido.Exceptions;

namespace Kaleido.Http.Mcp.UnitTests;

public sealed class KaleidoMcpOptionsBuilderTests
    : Kaleido.UnitTests.SutFixture<KaleidoMcpOptionsBuilder>
{
    protected override KaleidoMcpOptionsBuilder CreateSut() => new();

    [Fact]
    public void Build_UsesThePackagedDefaultInstructions()
    {
        var instructions = Sut.Build().Instructions;

        Assert.Contains("informationRequest", instructions, StringComparison.Ordinal);
        Assert.Contains("Never answer on the user's behalf", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AppendsAdditionalInstructions_ToTheDefaults()
    {
        var builder = Sut;
        builder.AdditionalInstructions = "  Start with a member search.  ";

        var instructions = builder.Build().Instructions;

        Assert.Contains("informationRequest", instructions, StringComparison.Ordinal);
        Assert.EndsWith("\n\nStart with a member search.", instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_Instructions_ReplaceTheDefaults()
    {
        var builder = Sut;
        builder.Instructions = "Only these.";

        Assert.Equal("Only these.", builder.Build().Instructions);
    }

    [Fact]
    public void Build_InstructionsFile_ReplacesTheDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"kaleido-mcp-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, "From the file.\n");

        try
        {
            var builder = Sut;
            builder.InstructionsFile = path;

            Assert.Equal("From the file.", builder.Build().Instructions);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Build_AMissingInstructionsFile_FailsWithItsCode()
    {
        var builder = Sut;
        builder.InstructionsFile = "does-not-exist.md";

        Assert.Equal(McpErrorCodes.MissingInstructionsFile, Assert.Throws<KaleidoConfigurationException>(builder.Build).Code);
    }

    [Fact]
    public void Build_ClampsNegativeSettings_AndDefaultsBlankNames()
    {
        var builder = Sut;
        builder.MaxElicitationRounds = -1;
        builder.ServerName = " ";
        builder.ServerVersion = string.Empty;

        var options = builder.Build();

        Assert.Equal(0, options.MaxElicitationRounds);
        Assert.Equal("kaleido", options.ServerName);
        Assert.Equal("1.0.0", options.ServerVersion);
    }
}
