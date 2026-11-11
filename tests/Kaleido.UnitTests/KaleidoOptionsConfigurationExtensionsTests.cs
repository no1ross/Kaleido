using Kaleido.Exceptions;
using Microsoft.Extensions.Configuration;

namespace Kaleido.UnitTests;

public sealed class KaleidoOptionsConfigurationExtensionsTests
    : SutFixture
{
    private const string Code = "area_invalid_options";

    private static IConfigurationSection Section(params (string Key, string? Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.ToDictionary(x => $"Options:{x.Key}", x => x.Value))
            .Build()
            .GetSection("Options");

    [Fact]
    public void BindOver_AKeyInTheSection_ReplacesTheCodeValue_AndAnAbsentKeyKeepsIt()
    {
        var code = new Sample { Name = "code", Kept = "kept", Count = 1 };

        var bound = Section(("Name", "configured")).BindOver(code, Code);

        Assert.Equal("configured", bound.Name);
        Assert.Equal("kept", bound.Kept);
        Assert.Equal(1, bound.Count);
        Assert.Equal("code", code.Name);
    }

    // The binder alone would write into the code's nested object; here the nested record is copied.
    [Fact]
    public void BindOver_ANestedRecord_IsCopiedBeforeConfigurationIsLaidOverIt()
    {
        var code = new Sample { Inner = new Inner { Size = 1, Other = 9 } };

        var bound = Section(("Inner:Size", "7")).BindOver(code, Code);

        Assert.Equal(7, bound.Inner.Size);
        Assert.Equal(9, bound.Inner.Other);
        Assert.Equal(1, code.Inner.Size);
        Assert.NotSame(code.Inner, bound.Inner);
    }

    [Fact]
    public void BindOver_ANullNestedRecord_StartsFromItsDefaults()
    {
        var bound = Section(("Inner:Other", "4")).BindOver(new Sample { Inner = null! }, Code);

        Assert.Equal(3, bound.Inner.Size);
        Assert.Equal(4, bound.Inner.Other);
    }

    // A positional record without a parameterless constructor and no code value is read whole.
    [Fact]
    public void BindOver_ANullPositionalRecord_IsReadFromTheSection()
    {
        var bound = Section(("Pair:Left", "a"), ("Pair:Right", "b")).BindOver(new Sample(), Code);

        Assert.Equal(new Pair("a", "b"), bound.Pair);
    }

    // The binder alone would append the configured items to the code's.
    [Fact]
    public void BindOver_AConfiguredList_ReplacesTheCodeList()
    {
        var bound = Section(("Items:0", "configured")).BindOver(new Sample { Items = ["code"] }, Code);

        Assert.Equal(["configured"], bound.Items);
    }

    [Fact]
    public void BindOver_AConfiguredDictionary_ReplacesTheCodeOne_WithCaseInsensitiveKeys()
    {
        var code = new Sample { Map = new Dictionary<string, Inner> { ["old"] = new() } };

        var bound = Section(("Map:Member:Size", "5")).BindOver(code, Code);

        var entry = Assert.Single(bound.Map);
        Assert.Equal("Member", entry.Key);
        Assert.Equal(5, bound.Map["member"].Size);
    }

    [Fact]
    public void BindOver_AValueThatCannotBeRead_ThrowsTheAreasCode_WithTheSettingsPath()
    {
        var exception = Assert.Throws<KaleidoConfigurationException>(() => Section(("Count", "many")).BindOver(new Sample(), Code));

        Assert.Equal(Code, exception.Code);
        Assert.Equal("The configured value of Options:Count cannot be read as Int32.", exception.Message);
    }

    [Fact]
    public void BindOver_ANonRecordOptionsType_IsAFrameworkError()
    {
        var exception = Assert.Throws<KaleidoFrameworkException>(() => Section(("Value", "x")).BindOver(new NotARecord(), Code));

        Assert.Equal(FrameworkErrorCodes.ReflectionError, exception.Code);
    }

    [Fact]
    public void BindOver_WhenAnArgumentIsMissing_Throws()
    {
        var section = Section();

        Assert.Throws<ArgumentNullException>(() => ((IConfigurationSection)null!).BindOver(new Sample(), Code));
        Assert.Throws<ArgumentNullException>(() => section.BindOver<Sample>(null!, Code));
        Assert.Throws<ArgumentException>(() => section.BindOver(new Sample(), " "));
    }

    [Fact]
    public void BindOptions_UsesTheOptionsPathAndValidatesTheOverlay()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Options:Count"] = "2" })
            .Build();
        var code = new ValidatedSample { Count = 3 };

        var effective = configuration.BindOptions(code);

        Assert.Equal(2, effective.Count);
        Assert.Equal(3, code.Count);
        Assert.Equal(1, new ConfigurationBuilder().Build().BindOptions<ValidatedSample>().Count);
        var invalid = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Options:Count"] = "0" })
            .Build();
        Assert.Equal(Code, Assert.Throws<KaleidoConfigurationException>(() => invalid.BindOptions<ValidatedSample>()).Code);
    }

    [Fact]
    public void BindOptions_OnBuilder_UsesTheSameConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Options:Count"] = "4" })
            .Build();
        var builder = new Mock<IKaleidoBuilder>();
        builder.SetupGet(x => x.Configuration).Returns(configuration);

        Assert.Equal(4, builder.Object.BindOptions<ValidatedSample>().Count);
    }

    private sealed record ValidatedSample : IKaleidoOptions
    {
        public int Count { get; init; } = 1;

        string IKaleidoOptions.ConfigurationPath => "Options";

        void IKaleidoOptions.Validate()
        {
            if (Count <= 0)
            {
                throw new KaleidoConfigurationException(Code, "Count must be positive.");
            }
        }
    }

    public sealed record Sample
    {
        public string Name { get; init; } = "default";

        public string? Kept { get; init; }

        public int Count { get; init; }

        public Inner Inner { get; init; } = new();

        public Pair? Pair { get; init; }

        public IReadOnlyList<string> Items { get; init; } = [];

        public IReadOnlyDictionary<string, Inner> Map { get; init; } = new Dictionary<string, Inner>();
    }

    public sealed record Inner
    {
        public int Size { get; init; } = 3;

        public int Other { get; init; }
    }

    public sealed record Pair(string Left, string Right);

    public sealed class NotARecord
    {
        public string? Value { get; init; }
    }
}
