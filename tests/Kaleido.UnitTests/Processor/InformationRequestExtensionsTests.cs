using Kaleido.UnitTests;

namespace Kaleido.Processor.UnitTests;

public sealed class InformationRequestExtensionsTests
    : SutFixture
{
    private static InformationCondition When(string question, InformationConditionOperator op, string? answer = null) =>
        new() { Question = question, Operator = op, Answer = answer };

    private static InformationItem Question(string id, InformationItemType type, params InformationCondition[] enableWhen) =>
        new()
        {
            Id = id,
            Text = id,
            Type = type,
            Options = type == InformationItemType.Choice ? [new() { Value = "Spine" }, new() { Value = "Knee" }] : [],
            EnableWhen = enableWhen,
        };

    private static Dictionary<string, IReadOnlyList<string>> Given(params (string Id, string[] Values)[] answers) =>
        answers.ToDictionary(x => x.Id, x => (IReadOnlyList<string>)x.Values, StringComparer.Ordinal);

    public static TheoryData<InformationItemType, InformationConditionOperator, string, string[], bool> Comparisons() =>
        new()
        {
            { InformationItemType.Choice, InformationConditionOperator.Equals, "Knee", ["Knee"], true },
            { InformationItemType.Choice, InformationConditionOperator.Equals, "Knee", ["Spine"], false },
            { InformationItemType.Choice, InformationConditionOperator.NotEquals, "Spine", ["Knee"], true },
            { InformationItemType.Choice, InformationConditionOperator.NotEquals, "Spine", ["Spine"], false },
            { InformationItemType.Boolean, InformationConditionOperator.Equals, "true", ["True"], true },
            { InformationItemType.WholeNumber, InformationConditionOperator.GreaterThan, "5", ["6"], true },
            { InformationItemType.WholeNumber, InformationConditionOperator.GreaterThan, "5", ["5"], false },
            { InformationItemType.WholeNumber, InformationConditionOperator.GreaterThanOrEqual, "5", ["5"], true },
            { InformationItemType.Number, InformationConditionOperator.LessThan, "2.5", ["2.25"], true },
            { InformationItemType.Number, InformationConditionOperator.LessThanOrEqual, "2.5", ["2.50"], true },
            { InformationItemType.Date, InformationConditionOperator.LessThan, "2024-05-01", ["2024-04-30"], true },
            { InformationItemType.DateTime, InformationConditionOperator.GreaterThan, "2024-05-01T00:00:00Z", ["2024-05-01T01:00:00+00:00"], true },
            { InformationItemType.Text, InformationConditionOperator.Equals, "yes", [" yes "], true },
            // Unreadable as the type: no comparison holds.
            { InformationItemType.WholeNumber, InformationConditionOperator.LessThan, "5", ["several"], false },
            // Repeats: Equals when any answer matches, NotEquals when none does.
            { InformationItemType.Choice, InformationConditionOperator.Equals, "Knee", ["Spine", "Knee"], true },
            { InformationItemType.Choice, InformationConditionOperator.NotEquals, "Knee", ["Spine", "Knee"], false },
        };

    [Theory]
    [MemberData(nameof(Comparisons))]
    public void Holds_ComparesTheAnswerByTheQuestionsType(InformationItemType type, InformationConditionOperator op, string expected, string[] answers, bool holds)
    {
        Assert.Equal(holds, When("q", op, expected).Holds(Question("q", type), answers));
    }

    // Unanswered (or disabled): only "does not exist" holds.
    [Theory]
    [InlineData(InformationConditionOperator.Exists, "false", true)]
    [InlineData(InformationConditionOperator.Exists, "true", false)]
    [InlineData(InformationConditionOperator.NotEquals, "Spine", false)]
    [InlineData(InformationConditionOperator.Equals, "Spine", false)]
    public void Holds_ForAnUnansweredQuestion_OnlyNotExistsHolds(InformationConditionOperator op, string expected, bool holds)
    {
        Assert.Equal(holds, When("q", op, expected).Holds(Question("q", InformationItemType.Choice), []));
    }

    [Fact]
    public void Holds_ExistsTrue_ForAnAnsweredQuestion()
    {
        Assert.True(When("q", InformationConditionOperator.Exists, "true").Holds(Question("q", InformationItemType.Text), ["x"]));
    }

    [Theory]
    [InlineData(InformationItemType.WholeNumber, true)]
    [InlineData(InformationItemType.Number, true)]
    [InlineData(InformationItemType.Date, true)]
    [InlineData(InformationItemType.DateTime, true)]
    [InlineData(InformationItemType.Choice, false)]
    [InlineData(InformationItemType.Boolean, false)]
    [InlineData(InformationItemType.Text, false)]
    public void IsOrdered_OnlyForNumbersAndDates(InformationItemType type, bool ordered)
    {
        Assert.Equal(ordered, type.IsOrdered());
    }

    private static readonly InformationRequest Mri =
        new()
        {
            InformationRequestId = "mri",
            Items =
            [
                Question("bodyPart", InformationItemType.Choice),
                Question("laterality", InformationItemType.Text, When("bodyPart", InformationConditionOperator.NotEquals, "Spine")),
                Question("therapy", InformationItemType.Boolean),
                new InformationItem
                {
                    Id = "therapyDetails",
                    Text = "Therapy",
                    Type = InformationItemType.Group,
                    EnableWhen = [When("therapy", InformationConditionOperator.Equals, "true")],
                    Items = [Question("weeks", InformationItemType.WholeNumber)],
                },
                Question("either", InformationItemType.Text, When("bodyPart", InformationConditionOperator.Equals, "Knee"), When("therapy", InformationConditionOperator.Equals, "true")) with
                {
                    EnableBehavior = InformationEnableBehavior.Any,
                },
            ],
        };

    [Fact]
    public void Questions_AreEveryQuestionInPresentationOrder_WithoutGroups()
    {
        Assert.Equal(["bodyPart", "laterality", "therapy", "weeks", "either"], Mri.Questions().Select(x => x.Id));
    }

    [Fact]
    public void EnabledItemIds_FollowTheAnswers_AndGroupsDisableTheirItems()
    {
        var spineNoTherapy = Mri.EnabledItemIds(Given(("bodyPart", ["Spine"]), ("therapy", ["false"])));
        var kneeTherapy = Mri.EnabledItemIds(Given(("bodyPart", ["Knee"]), ("therapy", ["true"])));

        Assert.Equal(["bodyPart", "therapy"], spineNoTherapy.Order(StringComparer.Ordinal));
        Assert.Equal(["bodyPart", "either", "laterality", "therapy", "therapyDetails", "weeks"], kneeTherapy.Order(StringComparer.Ordinal));
    }

    // Any: one condition is enough; All (default) needs every one.
    [Fact]
    public void EnabledItemIds_AnyNeedsOneCondition()
    {
        Assert.Contains("either", Mri.EnabledItemIds(Given(("bodyPart", ["Spine"]), ("therapy", ["true"]))));
        Assert.DoesNotContain("either", Mri.EnabledItemIds(Given(("bodyPart", ["Spine"]), ("therapy", ["false"]))));
    }

    // The answer of a disabled question counts as no answer.
    [Fact]
    public void EnabledItemIds_IgnoreTheAnswerOfADisabledQuestion()
    {
        var request = new InformationRequest
        {
            InformationRequestId = "chain",
            Items =
            [
                Question("a", InformationItemType.Boolean),
                Question("b", InformationItemType.Text, When("a", InformationConditionOperator.Equals, "true")),
                Question("c", InformationItemType.Text, When("b", InformationConditionOperator.Exists, "true")),
            ],
        };

        Assert.DoesNotContain("c", request.EnabledItemIds(Given(("a", ["false"]), ("b", ["stale"]))));
        Assert.Contains("c", request.EnabledItemIds(Given(("a", ["true"]), ("b", ["fresh"]))));
    }

    // What is put to the user: only the questions of this round, in their enabled groups, conditions
    // removed (Kaleido has applied them); display text only in the first round.
    [Fact]
    public void NextRound_HasOnlyThisRoundsQuestions_WithoutConditions()
    {
        var request = Mri with { Items = [new InformationItem { Id = "intro", Text = "MRI questions", Type = InformationItemType.Display }, .. Mri.Items] };

        var first = request.NextRound(Given());
        var second = request.NextRound(Given(("bodyPart", ["Knee"]), ("therapy", ["true"])));

        Assert.NotNull(first);
        Assert.Equal(["intro", "bodyPart", "therapy"], first.Items.Select(x => x.Id));
        Assert.NotNull(second);
        Assert.Equal(["laterality", "therapyDetails", "either"], second.Items.Select(x => x.Id));
        Assert.Equal(["weeks"], second.Items[1].Items.Select(x => x.Id));
        Assert.All(second.Items, x => Assert.Empty(x.EnableWhen));
        Assert.Equal(InformationEnableBehavior.All, second.Items[2].EnableBehavior);
    }

    [Fact]
    public void NextRound_WhenEverythingThatAppliesIsAnswered_IsNull()
    {
        Assert.Null(Mri.NextRound(Given(("bodyPart", ["Spine"]), ("therapy", ["false"]))));
    }

    // Without conditions nothing changes: the request is asked as it is.
    [Fact]
    public void NextRound_ForARequestWithoutConditions_IsTheRequest()
    {
        var plain = new InformationRequest { InformationRequestId = "plain", Items = [Question("a", InformationItemType.Text)] };

        Assert.Same(plain, plain.NextRound(Given()));
    }

    // Rounds: first the unconditional questions, then those the answers decide.
    [Fact]
    public void AskableQuestions_AskOnlyWhatTheAnswersSoFarDecide()
    {
        Assert.Equal(["bodyPart", "therapy"], Mri.AskableQuestions(Given()).Select(x => x.Id));
        Assert.Equal(["therapy"], Mri.AskableQuestions(Given(("bodyPart", ["Spine"]))).Select(x => x.Id));
        Assert.Equal(["laterality", "weeks", "either"], Mri.AskableQuestions(Given(("bodyPart", ["Knee"]), ("therapy", ["true"]))).Select(x => x.Id));
        Assert.Empty(Mri.AskableQuestions(Given(("bodyPart", ["Spine"]), ("therapy", ["false"]))));
    }
}
