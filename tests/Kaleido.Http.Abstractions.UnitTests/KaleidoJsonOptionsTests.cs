using System.Text.Json;
using Kaleido.Observability;
using Kaleido.Processor;
using Kaleido.Queryable;
using Kaleido.Queryable.Query;
using Kaleido.Registry;
using Kaleido.UnitTests;

namespace Kaleido.Http.UnitTests;

public sealed class KaleidoJsonOptionsTests
    : SutFixture
{
    // A shared options instance that is still mutable gets written to on first use (Results.Json sets
    // its type-info resolver), which races with a concurrent serialization that locks it (#270).
    [Fact]
    public void Options_AreReadOnlyFromTheStart()
    {
        Assert.True(KaleidoJsonOptions.Options.IsReadOnly);
        Assert.NotNull(KaleidoJsonOptions.Options.TypeInfoResolver);
    }

    [Fact]
    public void Options_WriteEnumsAsCamelCaseStringsAndPropertiesInCamelCase()
    {
        var json = JsonSerializer.Serialize(new Sample(SampleKind.FirstKind), KaleidoJsonOptions.Options);

        Assert.Equal("""{"sampleKind":"firstKind"}""", json);
    }

    [Fact]
    public void Options_RejectIntegerEnumValues()
    {
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<Sample>("""{"sampleKind":0}""", KaleidoJsonOptions.Options));
    }

    [Fact]
    public void Options_CanBeCopiedToChangeASetting()
    {
        var copy = new JsonSerializerOptions(KaleidoJsonOptions.Options) { WriteIndented = true };

        Assert.False(copy.IsReadOnly);
        Assert.True(copy.WriteIndented);
    }

    // Snapshots of the core shapes the wire contracts carry (#448, ADR 0017): each is a supported
    // wire contract, so a change to its JSON shows up here, and after 1.0 must be additive.
    [Fact]
    public void Options_SerializeAnInformationRequestAsItsWireShape()
    {
        var request = new InformationRequest
        {
            InformationRequestId = "attestation",
            ExchangeId = "0f1e2d3c4b5a69788796a5b4c3d2e1f0",
            Title = "Attestation",
            Items =
            [
                new InformationItem
                {
                    Id = "Group",
                    Text = "About the member",
                    Type = InformationItemType.Group,
                    Items =
                    [
                        new InformationItem
                        {
                            Id = "Side",
                            Text = "Which side?",
                            Type = InformationItemType.Choice,
                            Repeats = true,
                            Options = [new InformationOption { Value = "Left", Display = "Left side" }],
                            ProposedAnswers = [new InformationAnswer { Value = "Left" }],
                            EnableWhen = [new InformationCondition { Question = "Other", Operator = InformationConditionOperator.NotEquals, Answer = "Spine" }],
                            EnableBehavior = InformationEnableBehavior.Any,
                        },
                    ],
                },
            ],
        };

        Assert.Equal(
            """{"informationRequestId":"attestation","exchangeId":"0f1e2d3c4b5a69788796a5b4c3d2e1f0","title":"Attestation","items":[{"id":"Group","text":"About the member","type":"group","repeats":false,"options":[],"items":[{"id":"Side","text":"Which side?","type":"choice","repeats":true,"options":[{"value":"Left","display":"Left side"}],"items":[],"proposedAnswers":[{"value":"Left"}],"enableWhen":[{"question":"Other","operator":"notEquals","answer":"Spine"}],"enableBehavior":"any"}],"proposedAnswers":[],"enableWhen":[],"enableBehavior":"all"}]}""",
            JsonSerializer.Serialize(request, KaleidoJsonOptions.Options));
    }

    [Fact]
    public void Options_SerializeTheProcessShapesAsTheirWireShape()
    {
        Assert.Equal(
            """{"code":"pro_custom","type":"warning","message":"Check the member."}""",
            JsonSerializer.Serialize(new ProcessMessage { Code = "pro_custom", Type = MessageType.Warning, Message = "Check the member." }, KaleidoJsonOptions.Options));
        Assert.Equal(
            """{"name":"Alice","id":"user-1"}""",
            JsonSerializer.Serialize(new KaleidoCaller { Name = "Alice", Id = "user-1" }, KaleidoJsonOptions.Options));
    }

    [Fact]
    public void Options_SerializeTheRegistryAndQueryShapesAsTheirWireShape()
    {
        Assert.Equal(
            """{"policy":"named","roles":["radiology"],"allowAnonymous":false}""",
            JsonSerializer.Serialize(new AuthorizationMetadata("named", ["radiology"]), KaleidoJsonOptions.Options));
        Assert.Equal(
            """{"totalCount":3,"offset":2,"pageSize":1,"results":["c"]}""",
            JsonSerializer.Serialize(new QueryResult<string>(3, 2, 1, ["c"]), KaleidoJsonOptions.Options));
    }

    // Every member of the core enums on the wire, as its camelCase string: renaming or removing one is
    // a wire change.
    [Fact]
    public void Options_WriteTheWireEnumsWithTheirContractNames()
    {
        Assert.Equal(
            """["active","complete","businessFailure","processViolation","handOff","awaitingRequiredStep","awaitingStepSelection","exception","canceled","awaitingInformation"]""",
            Names<ProcessExecutionState>());
        Assert.Equal(
            """["pending","completed","validationFailed","exception","skipped","canceled","failed","rejected"]""",
            Names<StepExecutionStatus>());
        Assert.Equal(
            """["pending","completed","failed","blocked","canceled","rejected"]""",
            Names<StepExecutionOutcome>());
        Assert.Equal(
            """["information","warning","error"]""",
            Names<MessageType>());
        Assert.Equal(
            """["text","longText","boolean","wholeNumber","number","date","dateTime","choice","display","group"]""",
            Names<InformationItemType>());
        Assert.Equal(
            """["exists","equals","notEquals","greaterThan","lessThan","greaterThanOrEqual","lessThanOrEqual"]""",
            Names<InformationConditionOperator>());
        Assert.Equal("""["all","any"]""", Names<InformationEnableBehavior>());
        Assert.Equal(
            """["equals","notEquals","greaterThan","lessThan","greaterThanOrEqual","lessThanOrEqual","contains","notContains","startsWith","endsWith","in","notIn","between","notBetween","isNull","isNotNull","isTrue","isFalse"]""",
            Names<FilterOperator>());
        Assert.Equal("""["exact","startsWith","endsWith","contains"]""", Names<MatchMode>());
    }

    private static string Names<TEnum>()
        where TEnum : struct, Enum =>
        JsonSerializer.Serialize(Enum.GetValues<TEnum>(), KaleidoJsonOptions.Options);

    private enum SampleKind
    {
        FirstKind,
    }

    private sealed record Sample(SampleKind SampleKind);
}
