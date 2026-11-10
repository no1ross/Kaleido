using Kaleido.UnitTests;

namespace Kaleido.Processor.UnitTests;

public sealed class InformationValidatorTests
    : SutFixture
{
    private static InformationValidator CreateSut() =>
        new();

    private static readonly InformationRequest Request =
        new()
        {
            InformationRequestId = "out-of-network",
            Title = "Out-of-network attestation",
            Items =
            [
                new InformationItem { Id = "intro", Text = "Please answer the following.", Type = InformationItemType.Display },
                new InformationItem
                {
                    Id = "reason",
                    Text = "Why is an out-of-network provider needed?",
                    Type = InformationItemType.Choice,
                    Options =
                    [
                        new InformationOption { Value = "no-in-network", Display = "No in-network provider available" },
                        new InformationOption { Value = "continuity", Display = "Continuity of care" }
                    ]
                },
                new InformationItem
                {
                    Id = "details",
                    Text = "Details",
                    Type = InformationItemType.Group,
                    Items =
                    [
                        new InformationItem { Id = "attested", Text = "I attest this is accurate.", Type = InformationItemType.Boolean },
                        new InformationItem { Id = "visits", Text = "Number of visits", Type = InformationItemType.WholeNumber },
                        new InformationItem { Id = "start", Text = "Start date", Type = InformationItemType.Date },
                        new InformationItem { Id = "codes", Text = "Diagnosis codes", Type = InformationItemType.Text, Repeats = true }
                    ]
                }
            ]
        };

    // ── Request ───────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateRequest_WellFormed_ReturnsNoProblems()
    {
        Assert.Empty(CreateSut().ValidateRequest(Request));
    }

    private static InformationRequest Proposing(params InformationItem[] items) =>
        new() { InformationRequestId = "mri", Items = items };

    private static InformationItem Side(params string[] proposed) =>
        new()
        {
            Id = "laterality",
            Text = "Which side is affected?",
            Type = InformationItemType.Choice,
            Options = [new InformationOption { Value = "Left" }, new InformationOption { Value = "Right" }],
            ProposedAnswers = proposed.Select(x => new InformationAnswer { Value = x }).ToArray()
        };

    [Fact]
    public void ValidateRequest_ProposedAnswersThatFit_AreWellFormed()
    {
        Assert.Empty(CreateSut().ValidateRequest(Proposing(
            Side("Left"),
            new InformationItem { Id = "date", Text = "When?", Type = InformationItemType.Date, ProposedAnswers = [new InformationAnswer { Value = "2026-10-10" }] })));
    }

    [Theory]
    [InlineData("Top")]
    [InlineData("")]
    public void ValidateRequest_AProposedAnswerTheUserCouldNotSubmit_IsInvalid(string value)
    {
        var problem = Assert.Single(CreateSut().ValidateRequest(Proposing(Side(value))));

        Assert.Equal(ProcessorErrorCodes.InformationRequestInvalid, problem.Code);
        Assert.Contains("Proposed answer", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRequest_SeveralProposedAnswers_ForAQuestionThatDoesNotRepeat_IsInvalid()
    {
        Assert.Contains(
            CreateSut().ValidateRequest(Proposing(Side("Left", "Right"))),
            x => x.Message.Contains("proposes 2", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRequest_AProposedAnswerOnDisplayText_IsInvalid()
    {
        Assert.Contains(
            CreateSut().ValidateRequest(Proposing(
                Side(),
                new InformationItem { Id = "note", Text = "Note", Type = InformationItemType.Display, ProposedAnswers = [new InformationAnswer { Value = "x" }] })),
            x => x.Message.Contains("takes no proposed answer", StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateResponse_AProposalIsNotAnAnswer_TheQuestionMustStillBeAnswered()
    {
        var problems = CreateSut().ValidateResponse(
            Proposing(Side("Left")),
            new Answers { InformationRequestId = "mri" });

        Assert.Contains(problems, x => x.Code == ProcessorErrorCodes.InformationResponseUnanswered);
    }

    [Fact]
    public void ValidateRequest_WithoutId_IsInvalid()
    {
        var problems = CreateSut().ValidateRequest(Request with { InformationRequestId = " " });

        Assert.Contains(problems, x => x.Code == ProcessorErrorCodes.InformationRequestInvalid && x.Message.Contains("informationRequestId"));
    }

    [Fact]
    public void ValidateRequest_WithoutItems_IsInvalid()
    {
        Assert.NotEmpty(CreateSut().ValidateRequest(Request with { Items = [] }));
    }

    [Fact]
    public void ValidateRequest_DuplicateItemIds_AcrossNesting_IsInvalid()
    {
        var request =
            Request with
            {
                Items = [.. Request.Items, new InformationItem { Id = "attested", Text = "Again", Type = InformationItemType.Boolean }]
            };

        Assert.Contains(CreateSut().ValidateRequest(request), x => x.Message.Contains("'attested' is used more than once"));
    }

    [Theory]
    [InlineData(InformationItemType.Choice, "has no options")]
    [InlineData(InformationItemType.Group, "has no nested items")]
    public void ValidateRequest_IncompleteItem_IsInvalid(InformationItemType type, string expected)
    {
        var request =
            Request with { Items = [new InformationItem { Id = "x", Text = "X", Type = type }] };

        Assert.Contains(CreateSut().ValidateRequest(request), x => x.Message.Contains(expected));
    }

    [Theory]
    [InlineData("same", "same")]
    [InlineData("a", " ")]
    public void ValidateRequest_ChoiceWithDuplicateOrEmptyOptionValues_IsInvalid(string first, string second)
    {
        var request =
            Request with
            {
                Items =
                [
                    new InformationItem
                    {
                        Id = "x",
                        Text = "X",
                        Type = InformationItemType.Choice,
                        Options = [new InformationOption { Value = first }, new InformationOption { Value = second }],
                    },
                ],
            };

        Assert.Contains(CreateSut().ValidateRequest(request), x => x.Message.Contains("has empty or duplicate option values"));
    }

    [Fact]
    public void ValidateRequest_NestedItemsOnANonGroup_IsInvalid()
    {
        var request =
            Request with
            {
                Items =
                [
                    new InformationItem
                    {
                        Id = "x",
                        Text = "X",
                        Type = InformationItemType.Text,
                        Items = [new InformationItem { Id = "y", Text = "Y", Type = InformationItemType.Text }]
                    }
                ]
            };

        Assert.Contains(CreateSut().ValidateRequest(request), x => x.Message.Contains("only groups nest"));
    }

    // ── Response ──────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateResponse_CompleteAnswers_ReturnsNoProblems()
    {
        Assert.Empty(CreateSut().ValidateResponse(Request, ValidAnswers()));
    }

    [Fact]
    public void ValidateResponse_AnswersMayMirrorTheRequestTree()
    {
        var answers =
            new Answers
            {
                InformationRequestId = "out-of-network",
                Items =
                [
                    Answer("reason", "continuity"),
                    new InformationResponseItem
                    {
                        ItemId = "details",
                        Items = [Answer("attested", "true"), Answer("visits", "3"), Answer("start", "2024-05-01"), Answer("codes", "M54.5")]
                    }
                ]
            };

        Assert.Empty(CreateSut().ValidateResponse(Request, answers));
    }

    [Fact]
    public void ValidateResponse_ForAnotherRequest_IsAMismatch()
    {
        var problem =
            Assert.Single(CreateSut().ValidateResponse(Request, ValidAnswers() with { InformationRequestId = "site-of-care" }));

        Assert.Equal(ProcessorErrorCodes.InformationResponseMismatch, problem.Code);
    }

    // #442: answers to an earlier exchange of the same questionnaire - same id, same items, answers that
    // would otherwise fit - are stale, and that is the only problem reported.
    [Fact]
    public void ValidateResponse_ForAnEarlierExchange_IsStale_BeforeAnyOtherCheck()
    {
        var pending = Request with { ExchangeId = "second" };

        var problem = Assert.Single(CreateSut().ValidateResponse(pending, ValidAnswers() with { ExchangeId = "first", InformationRequestId = "site-of-care" }));

        Assert.Equal(ProcessorErrorCodes.InformationResponseStale, problem.Code);
        Assert.Contains("earlier exchange", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateResponse_ForThePendingExchange_IsChecked()
    {
        Assert.Empty(CreateSut().ValidateResponse(Request with { ExchangeId = "second" }, ValidAnswers() with { ExchangeId = "second" }));
    }

    [Fact]
    public void ValidateResponse_MissingAnswer_IsUnanswered()
    {
        var answers = ValidAnswers() with { Items = ValidAnswers().Items.Where(x => x.ItemId != "visits").ToArray() };

        var problem = Assert.Single(CreateSut().ValidateResponse(Request, answers));

        Assert.Equal(ProcessorErrorCodes.InformationResponseUnanswered, problem.Code);
        Assert.Contains("'visits'", problem.Message);
    }

    [Theory]
    [InlineData("attested", "yes")]
    [InlineData("visits", "3.5")]
    [InlineData("start", "05/01/2024")]
    [InlineData("reason", "other")]
    [InlineData("codes", " ")]
    public void ValidateResponse_AnswerThatDoesNotFit_IsInvalid(string itemId, string value)
    {
        var answers =
            ValidAnswers() with
            {
                Items = ValidAnswers().Items.Select(x => x.ItemId == itemId ? Answer(itemId, value) : x).ToArray()
            };

        var problem = Assert.Single(CreateSut().ValidateResponse(Request, answers));

        Assert.Equal(ProcessorErrorCodes.InformationResponseInvalidAnswer, problem.Code);
        Assert.Contains($"'{itemId}'", problem.Message);
    }

    [Fact]
    public void ValidateResponse_UnknownItem_IsInvalid()
    {
        var answers = ValidAnswers() with { Items = [.. ValidAnswers().Items, Answer("extra", "x")] };

        Assert.Contains(CreateSut().ValidateResponse(Request, answers), x => x.Message.Contains("'extra' is not part of"));
    }

    [Fact]
    public void ValidateResponse_SeveralAnswersToANonRepeatingQuestion_IsInvalid()
    {
        var answers =
            ValidAnswers() with
            {
                Items = ValidAnswers().Items.Select(x => x.ItemId == "visits" ? Answer("visits", "1", "2") : x).ToArray()
            };

        Assert.Contains(CreateSut().ValidateResponse(Request, answers), x => x.Message.Contains("takes one answer"));
    }

    [Fact]
    public void ValidateResponse_SeveralAnswersToARepeatingQuestion_Fit()
    {
        var answers =
            ValidAnswers() with
            {
                Items = ValidAnswers().Items.Select(x => x.ItemId == "codes" ? Answer("codes", "M54.5", "M51.2") : x).ToArray()
            };

        Assert.Empty(CreateSut().ValidateResponse(Request, answers));
    }

    [Fact]
    public void ValidateResponse_AnswerToDisplayItem_IsInvalid()
    {
        var answers = ValidAnswers() with { Items = [.. ValidAnswers().Items, Answer("intro", "ok")] };

        Assert.Contains(CreateSut().ValidateResponse(Request, answers), x => x.Message.Contains("takes no answer"));
    }

    [Fact]
    public void ValidateRequest_ItemWithoutIdOrText_IsInvalid()
    {
        var problems =
            CreateSut().ValidateRequest(Proposing(
                new InformationItem { Id = " ", Text = "Unnamed", Type = InformationItemType.Text },
                new InformationItem { Id = "blank", Text = " ", Type = InformationItemType.Text }));

        Assert.Equal(2, problems.Count);
        Assert.All(problems, x => Assert.Equal(ProcessorErrorCodes.InformationRequestInvalid, x.Code));
        Assert.Contains(problems, x => x.Message.Contains("an item without an id"));
        Assert.Contains(problems, x => x.Message.Contains("Item 'blank' has no text."));
    }

    [Fact]
    public void ValidateRequest_ProposedAnswerOnAGroup_IsInvalid()
    {
        var group =
            new InformationItem
            {
                Id = "details",
                Text = "Details",
                Type = InformationItemType.Group,
                Items = [new InformationItem { Id = "note", Text = "Note", Type = InformationItemType.Text }],
                ProposedAnswers = [new InformationAnswer { Value = "x" }]
            };

        var problem = Assert.Single(CreateSut().ValidateRequest(Proposing(group)));

        Assert.Contains("is a Group and takes no proposed answer", problem.Message);
    }

    [Fact]
    public void ValidateRequest_WithoutProposedAnswers_OrSeveralForARepeatingQuestion_IsWellFormed()
    {
        Assert.Empty(CreateSut().ValidateRequest(Proposing(
            new InformationItem { Id = "plain", Text = "Plain", Type = InformationItemType.Text, ProposedAnswers = null! },
            new InformationItem
            {
                Id = "codes",
                Text = "Codes",
                Type = InformationItemType.Text,
                Repeats = true,
                ProposedAnswers = [new InformationAnswer { Value = "M54.5" }, new InformationAnswer { Value = "M51.2" }]
            })));
    }

    [Fact]
    public void ValidateRequest_ANullProposedAnswer_DoesNotFit()
    {
        var item = new InformationItem { Id = "note", Text = "Note", Type = InformationItemType.Text, ProposedAnswers = [null!] };

        var problem = Assert.Single(CreateSut().ValidateRequest(Proposing(item)));

        Assert.Contains("Proposed answer '' does not fit question 'note' (expects non-empty text)", problem.Message);
    }

    [Theory]
    [InlineData(InformationItemType.LongText, "free text", true)]
    [InlineData(InformationItemType.Number, "3.5", true)]
    [InlineData(InformationItemType.Number, "three", false)]
    [InlineData(InformationItemType.DateTime, "2024-05-01T10:00:00Z", true)]
    [InlineData(InformationItemType.DateTime, "later", false)]
    [InlineData((InformationItemType)999, "anything", false)]
    public void ValidateResponse_AnswerFit_FollowsTheItemType(InformationItemType type, string value, bool fits)
    {
        var request = new InformationRequest { InformationRequestId = "r", Items = [new InformationItem { Id = "q", Text = "Q?", Type = type }] };
        var answers = new Answers { InformationRequestId = "r", Items = [Answer("q", value)] };

        var problems = CreateSut().ValidateResponse(request, answers);

        Assert.Equal(fits, problems.Count == 0);
    }

    [Theory]
    [InlineData(InformationItemType.Boolean, "expects true or false")]
    [InlineData(InformationItemType.WholeNumber, "expects a whole number")]
    [InlineData(InformationItemType.Number, "expects a number")]
    [InlineData(InformationItemType.Date, "expects a date, yyyy-MM-dd")]
    [InlineData(InformationItemType.DateTime, "expects an ISO 8601 date and time")]
    [InlineData(InformationItemType.Text, "expects non-empty text")]
    public void ValidateResponse_AnAnswerThatDoesNotFit_DescribesWhatTheQuestionExpects(InformationItemType type, string expected)
    {
        var request = new InformationRequest { InformationRequestId = "r", Items = [new InformationItem { Id = "q", Text = "Q?", Type = type }] };
        var answers = new Answers { InformationRequestId = "r", Items = [Answer("q", " ")] };

        var problem = Assert.Single(CreateSut().ValidateResponse(request, answers));

        Assert.Contains($"({expected})", problem.Message);
    }

    [Fact]
    public void ValidateResponse_ANullAnswer_DoesNotFit()
    {
        var request = new InformationRequest { InformationRequestId = "r", Items = [new InformationItem { Id = "q", Text = "Q?", Type = InformationItemType.Text }] };
        var answers = new Answers { InformationRequestId = "r", Items = [new InformationResponseItem { ItemId = "q", Answers = [null!] }] };

        var problem = Assert.Single(CreateSut().ValidateResponse(request, answers));

        Assert.Equal(ProcessorErrorCodes.InformationResponseInvalidAnswer, problem.Code);
        Assert.Contains("Answer '' does not fit question 'q'", problem.Message);
    }

    [Fact]
    public void ValidateResponse_AQuestionAnsweredTwice_IsInvalid()
    {
        var answers = ValidAnswers() with { Items = [.. ValidAnswers().Items, Answer("visits", "4")] };

        Assert.Contains(CreateSut().ValidateResponse(Request, answers), x => x.Message.Contains("Item 'visits' is answered more than once."));
    }

    [Fact]
    public void ValidateResponse_AnItemWithoutAnIdOrWithoutAnswers_IsReported()
    {
        var answers =
            ValidAnswers() with
            {
                Items = [.. ValidAnswers().Items.Where(x => x.ItemId != "visits"), Answer(null!, "x"), Answer("visits")]
            };

        var problems = CreateSut().ValidateResponse(Request, answers);

        Assert.Contains(problems, x => x.Message.Contains("Item '' is not part of"));
        Assert.Contains(problems, x => x.Code == ProcessorErrorCodes.InformationResponseUnanswered && x.Message.Contains("'visits'"));
    }

    [Fact]
    public void ValidateResponse_AnEmptyAnswerToAGroup_AndNoItemsAtAll_AreHandled()
    {
        var withGroup = ValidAnswers() with { Items = [.. ValidAnswers().Items, Answer("details")] };
        Assert.Empty(CreateSut().ValidateResponse(Request, withGroup));

        var withoutItems = ValidAnswers() with { Items = null! };
        Assert.All(CreateSut().ValidateResponse(Request, withoutItems), x => Assert.Equal(ProcessorErrorCodes.InformationResponseUnanswered, x.Code));
    }

    [Fact]
    public void Validate_WithNullArguments_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => CreateSut().ValidateRequest(null!));
        Assert.Throws<ArgumentNullException>(() => CreateSut().ValidateResponse(null!, ValidAnswers()));
        Assert.Throws<ArgumentNullException>(() => CreateSut().ValidateResponse(Request, null!));
    }

    private static Answers ValidAnswers() =>
        new()
        {
            InformationRequestId = "out-of-network",
            Items =
            [
                Answer("reason", "no-in-network"),
                Answer("attested", "true"),
                Answer("visits", "3"),
                Answer("start", "2024-05-01"),
                Answer("codes", "M54.5")
            ]
        };

    // ---------------------------------------------------------------------------
    // Conditional questions (EnableWhen, ADR 0024)

    // MRI: laterality only when the body part is not the spine; weeks of therapy only when therapy was tried.
    private static readonly InformationRequest Conditional =
        new()
        {
            InformationRequestId = "mri",
            Items =
            [
                new InformationItem
                {
                    Id = "bodyPart",
                    Text = "Which body part?",
                    Type = InformationItemType.Choice,
                    Options = [new() { Value = "Spine" }, new() { Value = "Knee" }],
                },
                new InformationItem
                {
                    Id = "laterality",
                    Text = "Which side?",
                    Type = InformationItemType.Choice,
                    Options = [new() { Value = "Left" }, new() { Value = "Right" }],
                    EnableWhen = [When("bodyPart", InformationConditionOperator.NotEquals, "Spine")],
                },
                new InformationItem { Id = "therapy", Text = "Was physical therapy tried?", Type = InformationItemType.Boolean },
                new InformationItem
                {
                    Id = "therapyDetails",
                    Text = "Therapy",
                    Type = InformationItemType.Group,
                    EnableWhen = [When("therapy", InformationConditionOperator.Equals, "true")],
                    Items = [new InformationItem { Id = "weeks", Text = "For how many weeks?", Type = InformationItemType.WholeNumber }],
                },
            ],
        };

    private static InformationCondition When(string question, InformationConditionOperator op, string? answer) =>
        new() { Question = question, Operator = op, Answer = answer };

    private static Answers ConditionalAnswers(params InformationResponseItem[] items) =>
        new() { InformationRequestId = "mri", Items = items };

    [Fact]
    public void ValidateRequest_WithConditionsOnEarlierQuestions_IsWellFormed()
    {
        Assert.Empty(CreateSut().ValidateRequest(Conditional));
    }

    public static TheoryData<InformationCondition, string> InvalidConditions() =>
        new()
        {
            { When("", InformationConditionOperator.Equals, "x"), "without a question" },
            { When("unknown", InformationConditionOperator.Equals, "x"), "not an earlier question" },
            { When("later", InformationConditionOperator.Exists, "true"), "comes after it" },
            { When("target", InformationConditionOperator.Exists, "true"), "not an earlier question" },
            { When("intro", InformationConditionOperator.Exists, "true"), "not an earlier question" },
            { When("side", InformationConditionOperator.GreaterThan, "Left"), "has no order" },
            { When("side", InformationConditionOperator.Equals, "Up"), "does not fit" },
            { When("count", InformationConditionOperator.LessThan, "many"), "does not fit" },
            { When("side", InformationConditionOperator.Exists, "maybe"), "not true or false" },
            { When("side", (InformationConditionOperator)42, "Left"), "unknown operator" },
        };

    // A condition tests an earlier question (so no cycles), with an operator its type supports and a fitting value.
    [Theory]
    [MemberData(nameof(InvalidConditions))]
    public void ValidateRequest_AnInvalidCondition_IsReported(InformationCondition condition, string expected)
    {
        var request = new InformationRequest
        {
            InformationRequestId = "conditions",
            Items =
            [
                new InformationItem { Id = "intro", Text = "Intro", Type = InformationItemType.Display },
                new InformationItem { Id = "side", Text = "Side", Type = InformationItemType.Choice, Options = [new() { Value = "Left" }] },
                new InformationItem { Id = "count", Text = "Count", Type = InformationItemType.WholeNumber },
                new InformationItem { Id = "target", Text = "Target", Type = InformationItemType.Text, EnableWhen = [condition] },
                new InformationItem { Id = "later", Text = "Later", Type = InformationItemType.Text },
            ],
        };

        var problem = Assert.Single(CreateSut().ValidateRequest(request));

        Assert.Equal(ProcessorErrorCodes.InformationRequestInvalid, problem.Code);
        Assert.Contains(expected, problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRequest_AnUnknownEnableBehavior_IsReported()
    {
        var request = Conditional with { Items = [Conditional.Items[0], Conditional.Items[1] with { EnableBehavior = (InformationEnableBehavior)7 }] };

        Assert.Contains(CreateSut().ValidateRequest(request), x => x.Message.Contains("unknown enableBehavior", StringComparison.Ordinal));
    }

    // Spine: laterality does not apply, so it is neither required nor accepted.
    [Fact]
    public void ValidateResponse_ADisabledQuestion_IsNotRequired()
    {
        Assert.Empty(CreateSut().ValidateResponse(Conditional, ConditionalAnswers(Answer("bodyPart", "Spine"), Answer("therapy", "false"))));
    }

    [Fact]
    public void ValidateResponse_AnEnabledQuestion_IsRequired()
    {
        var problem = Assert.Single(CreateSut().ValidateResponse(Conditional, ConditionalAnswers(Answer("bodyPart", "Knee"), Answer("therapy", "false"))));

        Assert.Equal(ProcessorErrorCodes.InformationResponseUnanswered, problem.Code);
        Assert.Contains("'laterality'", problem.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateResponse_AnAnswerToADisabledQuestion_IsRejected()
    {
        var problem = Assert.Single(CreateSut().ValidateResponse(
            Conditional,
            ConditionalAnswers(Answer("bodyPart", "Spine"), Answer("laterality", "Left"), Answer("therapy", "false"))));

        Assert.Equal(ProcessorErrorCodes.InformationResponseInvalidAnswer, problem.Code);
        Assert.Contains("does not apply", problem.Message, StringComparison.Ordinal);
    }

    // A disabled group disables its questions; an enabled one requires them.
    [Fact]
    public void ValidateResponse_AGroupsCondition_AppliesToItsQuestions()
    {
        Assert.Empty(CreateSut().ValidateResponse(Conditional, ConditionalAnswers(Answer("bodyPart", "Spine"), Answer("therapy", "false"))));
        Assert.Contains(
            CreateSut().ValidateResponse(Conditional, ConditionalAnswers(Answer("bodyPart", "Spine"), Answer("therapy", "true"))),
            x => x.Code == ProcessorErrorCodes.InformationResponseUnanswered && x.Message.Contains("'weeks'", StringComparison.Ordinal));
        Assert.Empty(CreateSut().ValidateResponse(Conditional, ConditionalAnswers(Answer("bodyPart", "Spine"), Answer("therapy", "true"), Answer("weeks", "6"))));
    }

    private static InformationResponseItem Answer(string itemId, params string[] values) =>
        new()
        {
            ItemId = itemId,
            Answers = values.Select(x => new InformationAnswer { Value = x }).ToArray()
        };

    private sealed record Answers : IInformationStep
    {
        public string InformationRequestId { get; init; } = string.Empty;
        public string ExchangeId { get; init; } = string.Empty;

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }
}
