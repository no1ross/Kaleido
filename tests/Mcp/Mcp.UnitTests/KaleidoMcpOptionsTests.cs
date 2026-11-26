using Kaleido.Exceptions;

namespace Kaleido.Mcp.UnitTests;

public sealed class KaleidoMcpOptionsTests
    : Kaleido.UnitTests.SutFixture<KaleidoMcpOptions>
{
    protected override KaleidoMcpOptions CreateSut() => new();

    [Fact]
    public void Defaults_AreValid()
    {
        var options = Sut;

        KaleidoMcpOptions.Validate(options);

        Assert.Equal(5, options.MaxElicitationRounds);
        Assert.Equal(McpToolListing.All, options.ToolListing);
        Assert.Empty(options.ListedTools);
        Assert.False(options.RequireConfirmation);
        Assert.Null(options.Instructions);
    }

    [Fact]
    public void Options_UseTheSharedConfigurationPathAndValidationContract()
    {
        var options = Assert.IsAssignableFrom<IKaleidoOptions>(Sut);

        Assert.Equal(KaleidoMcpOptions.SectionName, options.ConfigurationPath);
        options.Validate();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    public void Validate_ZeroOrMoreElicitationRounds_Pass(int rounds)
    {
        KaleidoMcpOptions.Validate(Sut with { MaxElicitationRounds = rounds });
    }

    // ADR 0030: nothing is trimmed or replaced - a blank, repeated, out-of-range or conflicting value
    // stops startup, naming the setting.
    [Theory]
    [InlineData("Instructions")]
    [InlineData("InstructionsFile")]
    [InlineData("AdditionalInstructions")]
    [InlineData("InstructionsAndFile")]
    [InlineData("MaxElicitationRounds")]
    [InlineData("ToolListing")]
    [InlineData("BlankListedTool")]
    [InlineData("RepeatedListedTool")]
    public void Validate_AnInvalidValue_FailsWithItsCode(string setting)
    {
        var (options, name) = setting switch
        {
            "Instructions" => (Sut with { Instructions = " " }, "Instructions"),
            "InstructionsFile" => (Sut with { InstructionsFile = string.Empty }, "InstructionsFile"),
            "AdditionalInstructions" => (Sut with { AdditionalInstructions = "\n" }, "AdditionalInstructions"),
            "InstructionsAndFile" => (Sut with { Instructions = "These.", InstructionsFile = "these.md" }, "InstructionsFile"),
            "MaxElicitationRounds" => (Sut with { MaxElicitationRounds = -1 }, "MaxElicitationRounds"),
            "ToolListing" => (Sut with { ToolListing = (McpToolListing)42 }, "ToolListing"),
            "BlankListedTool" => (Sut with { ListedTools = ["a", " "] }, "ListedTools"),
            _ => (Sut with { ListedTools = ["a", "a"] }, "ListedTools"),
        };

        var exception = Assert.Throws<KaleidoConfigurationException>(() => KaleidoMcpOptions.Validate(options));

        Assert.Equal(McpErrorCodes.InvalidOptions, exception.Code);
        Assert.StartsWith($"Kaleido:Mcp:{name} ", exception.Message, StringComparison.Ordinal);
    }
}
