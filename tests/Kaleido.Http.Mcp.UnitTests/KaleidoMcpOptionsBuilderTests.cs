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
        var path = Path.Join(Path.GetTempPath(), $"kaleido-mcp-{Guid.NewGuid():N}.md");
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
    public void Build_DefaultsBlankNames()
    {
        var builder = Sut;
        builder.ServerName = " ";
        builder.ServerVersion = string.Empty;

        var options = builder.Build();

        Assert.Equal("kaleido", options.ServerName);
        Assert.Equal("1.0.0", options.ServerVersion);
    }

    [Fact]
    public void Build_MaxElicitationRounds_DefaultsToFive()
    {
        Assert.Equal(5, Sut.Build().MaxElicitationRounds);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(50)]
    public void Build_MaxElicitationRounds_KeepsZeroOrMore(int rounds)
    {
        var builder = Sut;
        builder.MaxElicitationRounds = rounds;

        Assert.Equal(rounds, builder.Build().MaxElicitationRounds);
    }

    [Fact]
    public void Build_RequireConfirmation_IsOffByDefault_AndKeptWhenSet()
    {
        var builder = Sut;
        Assert.False(builder.Build().RequireConfirmation);

        builder.RequireConfirmation = true;

        Assert.True(builder.Build().RequireConfirmation);
    }

    [Fact]
    public void Build_ListsEverything_ByDefault()
    {
        var options = Sut.Build();

        Assert.Equal(McpToolListing.All, options.ToolListing);
        Assert.Empty(options.ListedTools);
        Assert.DoesNotContain("call_tool", options.Instructions, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_FrontDoor_AppendsItsInstructions_BeforeTheAdditionalOnes()
    {
        var builder = Sut;
        builder.ToolListing = McpToolListing.FrontDoor;
        builder.AdditionalInstructions = "Start with the intake.";

        var instructions = builder.Build().Instructions;

        Assert.Contains("call_tool", instructions, StringComparison.Ordinal);
        Assert.True(instructions.IndexOf("call_tool", StringComparison.Ordinal) < instructions.IndexOf("Start with the intake.", StringComparison.Ordinal));
    }

    [Fact]
    public void Build_ListedTools_AreTrimmedAndDistinct()
    {
        var builder = Sut;
        builder.ListedTools = [" a ", "a", "", "b"];

        Assert.Equal(["a", "b"], builder.Build().ListedTools);
    }

    [Fact]
    public void Build_AnUndefinedToolListing_FailsWithItsCode()
    {
        var builder = Sut;
        builder.ToolListing = (McpToolListing)42;

        Assert.Equal(McpErrorCodes.InvalidOptions, Assert.Throws<KaleidoConfigurationException>(builder.Build).Code);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Build_NegativeMaxElicitationRounds_FailsWithItsCode(int rounds)
    {
        var builder = Sut;
        builder.MaxElicitationRounds = rounds;

        var exception = Assert.Throws<KaleidoConfigurationException>(builder.Build);

        Assert.Equal(McpErrorCodes.InvalidOptions, exception.Code);
        Assert.Contains("Kaleido:Mcp:MaxElicitationRounds", exception.Message, StringComparison.Ordinal);
    }
}
