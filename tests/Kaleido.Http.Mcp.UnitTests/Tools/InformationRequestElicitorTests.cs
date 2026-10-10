using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools.UnitTests;

public sealed class InformationRequestElicitorTests
    : Kaleido.UnitTests.SutFixture
{
    private static InformationRequestElicitor CreateSut() => new();

    private static readonly string[] KneeAndHip = ["knee", "hip"];

    private static readonly InformationRequest Request = new()
    {
        InformationRequestId = "mri-info",
        Title = "MRI information",
        Items =
        [
            new InformationItem { Id = "intro", Text = "Answer for the current episode.", Type = InformationItemType.Display },
            new InformationItem
            {
                Id = "history",
                Text = "History",
                Type = InformationItemType.Group,
                Items =
                [
                    new InformationItem { Id = "weeks", Text = "Weeks of symptoms?", Type = InformationItemType.WholeNumber },
                    new InformationItem { Id = "pain", Text = "Pain score?", Type = InformationItemType.Number }
                ]
            },
            new InformationItem
            {
                Id = "contrast",
                Text = "Contrast?",
                Type = InformationItemType.Choice,
                Options = [new InformationOption { Value = "with", Display = "With contrast" }, new InformationOption { Value = "without" }]
            },
            new InformationItem
            {
                Id = "sites",
                Text = "Body sites?",
                Type = InformationItemType.Choice,
                Repeats = true,
                Options = [new InformationOption { Value = "knee" }, new InformationOption { Value = "hip" }]
            },
            new InformationItem { Id = "implant", Text = "Metal implant?", Type = InformationItemType.Boolean },
            new InformationItem { Id = "onset", Text = "Onset date?", Type = InformationItemType.Date },
            new InformationItem { Id = "notes", Text = "Notes?", Type = InformationItemType.LongText }
        ]
    };

    [Fact]
    public void ToElicitation_PutsTitleGroupsAndDisplayTextInTheMessage()
    {
        var elicitation = CreateSut().ToElicitation(Request);

        Assert.Contains("MRI information", elicitation.Message, StringComparison.Ordinal);
        Assert.Contains("Answer for the current episode.", elicitation.Message, StringComparison.Ordinal);
        Assert.Contains("History", elicitation.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToElicitation_AsksEveryQuestionVerbatim_AndRequiresThemAll()
    {
        var schema = CreateSut().ToElicitation(Request).RequestedSchema;

        Assert.NotNull(schema);
        Assert.Equal(["weeks", "pain", "contrast", "sites", "implant", "onset", "notes"], schema.Properties.Keys);
        Assert.Equal(schema.Properties.Keys, schema.Required);
        Assert.Equal("Weeks of symptoms?", schema.Properties["weeks"].Title);
        Assert.Equal("Part of: History", schema.Properties["weeks"].Description);
    }

    [Fact]
    public void ToElicitation_MapsEachItemTypeToAFormField()
    {
        var properties = CreateSut().ToElicitation(Request).RequestedSchema!.Properties;

        Assert.Equal("integer", Assert.IsType<ElicitRequestParams.NumberSchema>(properties["weeks"]).Type);
        Assert.IsType<ElicitRequestParams.NumberSchema>(properties["pain"]);
        Assert.IsType<ElicitRequestParams.BooleanSchema>(properties["implant"]);
        Assert.Equal("date", Assert.IsType<ElicitRequestParams.StringSchema>(properties["onset"]).Format);
        Assert.Equal(1, Assert.IsType<ElicitRequestParams.StringSchema>(properties["notes"]).MinLength);

        var single = Assert.IsType<ElicitRequestParams.TitledSingleSelectEnumSchema>(properties["contrast"]);
        Assert.Equal(["with", "without"], single.OneOf.Select(x => x.Const));
        Assert.Equal(["With contrast", "without"], single.OneOf.Select(x => x.Title));

        var multi = Assert.IsType<ElicitRequestParams.TitledMultiSelectEnumSchema>(properties["sites"]);
        Assert.Equal(["knee", "hip"], multi.Items.AnyOf.Select(x => x.Const));
    }

    [Fact]
    public void ToElicitation_WithoutATitleOrDisplayText_HasADefaultMessage()
    {
        var elicitation = CreateSut().ToElicitation(new InformationRequest
        {
            InformationRequestId = "q",
            Items = [new InformationItem { Id = "a", Text = "A?", Type = InformationItemType.Text }]
        });

        Assert.Equal("Please answer the following questions.", elicitation.Message);
    }

    [Fact]
    public void ToAnswers_ConvertsFormValuesToKaleidoAnswers()
    {
        var content = new Dictionary<string, JsonElement>
        {
            ["weeks"] = JsonSerializer.SerializeToElement(6),
            ["pain"] = JsonSerializer.SerializeToElement(7.5m),
            ["contrast"] = JsonSerializer.SerializeToElement("with"),
            ["sites"] = JsonSerializer.SerializeToElement(KneeAndHip),
            ["implant"] = JsonSerializer.SerializeToElement(false),
            ["onset"] = JsonSerializer.SerializeToElement("2026-09-01")
        };

        var answers = CreateSut().ToAnswers(Request, content).ToDictionary(x => x.ItemId, x => x.Answers.Select(a => a.Value).ToArray());

        Assert.Equal(["6"], answers["weeks"]);
        Assert.Equal(["7.5"], answers["pain"]);
        Assert.Equal(["with"], answers["contrast"]);
        Assert.Equal(["knee", "hip"], answers["sites"]);
        Assert.Equal(["false"], answers["implant"]);
        Assert.Equal(["2026-09-01"], answers["onset"]);
        Assert.False(answers.ContainsKey("notes"));
    }

    [Fact]
    public void ToAnswers_IgnoresKeysThatAreNotQuestions_AndEmptyValues()
    {
        var content = new Dictionary<string, JsonElement>
        {
            ["history"] = JsonSerializer.SerializeToElement("x"),
            ["intro"] = JsonSerializer.SerializeToElement("x"),
            ["notes"] = JsonSerializer.SerializeToElement(" ")
        };

        var answers = CreateSut().ToAnswers(Request, content);

        var notes = Assert.Single(answers);
        Assert.Equal("notes", notes.ItemId);
        Assert.Empty(notes.Answers);
    }
}
