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

    private static InformationResponseItem Answer(string itemId, params string[] values) =>
        new()
        {
            ItemId = itemId,
            Answers = values.Select(x => new InformationAnswer { Value = x }).ToArray()
        };

    private sealed record Answers : IInformationStep
    {
        public string InformationRequestId { get; init; } = string.Empty;

        public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
    }
}
