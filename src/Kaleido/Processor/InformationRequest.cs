namespace Kaleido.Processor;

/// <summary>
/// A set of questions a step needs answered before it can run, returned by a handler with
/// <c>RequireInformation&lt;TNext&gt;</c> for its required step.
/// </summary>
/// <remarks>
/// <para>
/// Modelled on the structure of the FHIR R4 Questionnaire (a tree of items with ids, types,
/// options and nested items), with Kaleido's own schema. Kaleido owns this schema, never the
/// content: the questions, their wording and their options always come from the implementer
/// (rules engines, clinical systems, configuration…).
/// </para>
/// <para>
/// The answers come back as the payload of the required step, an <see cref="IInformationStep"/>,
/// and are checked structurally against this request: the
/// <see cref="InformationRequestId"/> must match, every enabled question must be answered, each
/// answer must fit its item type and choice answers must be offered options.
/// </para>
/// <para>
/// A question may apply only when an earlier answer says so (<see cref="InformationItem.EnableWhen"/>,
/// ADR 0024). Kaleido decides which questions are enabled - in validation, in the MCP adapter and in
/// any client following the same rules - so an agent or a person is only ever asked the questions that
/// apply.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record InformationRequest
{
    /// <summary>
    /// Identifies the questionnaire (for example <c>out-of-network-attestation</c>). Set by the
    /// implementer and required; the response must echo it.
    /// </summary>
    public required string InformationRequestId { get; init; }

    /// <summary>A short title for the whole request, shown to the person answering.</summary>
    public string? Title { get; init; }

    /// <summary>The questions, display text and groups, in presentation order.</summary>
    public IReadOnlyList<InformationItem> Items { get; init; } = [];
}

/// <summary>
/// One question, display text or group in an <see cref="InformationRequest"/>.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record InformationItem
{
    /// <summary>Unique within the request; the response refers to it as the item id.</summary>
    public required string Id { get; init; }

    /// <summary>
    /// The question or display text, delivered verbatim: clients must not paraphrase it.
    /// </summary>
    public required string Text { get; init; }

    /// <summary>What kind of item this is, and so which answers it accepts.</summary>
    public InformationItemType Type { get; init; }

    /// <summary>Whether the question accepts more than one answer.</summary>
    public bool Repeats { get; init; }

    /// <summary>The allowed answers of a <see cref="InformationItemType.Choice"/> question.</summary>
    public IReadOnlyList<InformationOption> Options { get; init; } = [];

    /// <summary>Nested items (for a <see cref="InformationItemType.Group"/>).</summary>
    public IReadOnlyList<InformationItem> Items { get; init; } = [];

    /// <summary>
    /// Answers the implementer proposes for this question, for example from what the user already
    /// stated earlier in the process. Clients show them pre-filled; the user confirms or corrects
    /// them, and only what the user submits is an answer. A proposal is never submitted on the
    /// user's behalf.
    /// </summary>
    /// <remarks>
    /// Checked like answers when the request is created: each must fit the question (an offered
    /// option of a choice, a date for a date…), and there is at most one unless the question
    /// <see cref="Repeats"/>. Groups and display text take none. Which answers to propose, and from
    /// what, is the implementer's decision (ADR 0001).
    /// </remarks>
    public IReadOnlyList<InformationAnswer> ProposedAnswers { get; init; } = [];

    /// <summary>
    /// The conditions under which this item applies, on the answers to earlier questions; empty (the
    /// default) when it always applies. Modelled on FHIR's <c>enableWhen</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An item is enabled when its conditions hold - all of them, or any one, per
    /// <see cref="EnableBehavior"/> - and its enclosing group is enabled. Only enabled questions must be
    /// answered; an answer to a disabled question is rejected
    /// (<c>pro_information_response_invalid_answer</c>). A disabled group disables everything in it.
    /// </para>
    /// <para>
    /// Each condition refers to a question that comes earlier in the request (presentation order), so
    /// conditions never form a cycle; a condition on an unanswered or disabled question is false, except
    /// <see cref="InformationConditionOperator.Exists"/> with <see langword="false"/>. Which conditions to set is the
    /// implementer's decision (ADR 0001): Kaleido only evaluates them.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// new InformationItem
    /// {
    ///     Id = "Laterality",
    ///     Text = "Which side?",
    ///     Type = InformationItemType.Choice,
    ///     Options = [new() { Value = "Left" }, new() { Value = "Right" }],
    ///     EnableWhen = [new() { Question = "BodyPart", Operator = InformationConditionOperator.NotEquals, Answer = "Spine" }],
    /// }
    /// </code>
    /// </example>
    public IReadOnlyList<InformationCondition> EnableWhen { get; init; } = [];

    /// <summary>
    /// Whether all <see cref="EnableWhen"/> conditions must hold (<see cref="InformationEnableBehavior.All"/>,
    /// the default) or any one of them (<see cref="InformationEnableBehavior.Any"/>). Matters only with
    /// several conditions.
    /// </summary>
    public InformationEnableBehavior EnableBehavior { get; init; } = InformationEnableBehavior.All;
}

/// <summary>
/// A condition on the answer to an earlier question, under which an <see cref="InformationItem"/> applies
/// (<see cref="InformationItem.EnableWhen"/>).
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record InformationCondition
{
    /// <summary>The <see cref="InformationItem.Id"/> of the earlier question whose answer is tested.</summary>
    public required string Question { get; init; }

    /// <summary>How the answer is compared with <see cref="Answer"/>.</summary>
    public InformationConditionOperator Operator { get; init; }

    /// <summary>
    /// The value compared with, as text read by the tested question's type (an option value for a
    /// choice, <see langword="true"/>/<see langword="false"/> for a yes/no question, <c>2024-05-01</c> for a date...); for
    /// <see cref="InformationConditionOperator.Exists"/>, <see langword="true"/> (answered) or <see langword="false"/> (not answered).
    /// </summary>
    public string? Answer { get; init; }
}

/// <summary>How an <see cref="InformationCondition"/> compares the tested question's answer.</summary>
/// <remarks>
/// With a repeating question, <see cref="Equals"/> and the ordering operators hold when any answer
/// matches, <see cref="NotEquals"/> when none does. The ordering operators apply to whole numbers,
/// numbers, dates and date-times only.
/// </remarks>
public enum InformationConditionOperator
{
    /// <summary>The question is answered (<see cref="InformationCondition.Answer"/> <see langword="true"/>) or not (<see langword="false"/>).</summary>
    Exists,

    /// <summary>The answer equals the value.</summary>
    Equals,

    /// <summary>The question is answered, and no answer equals the value.</summary>
    NotEquals,

    /// <summary>The answer is greater than the value.</summary>
    GreaterThan,

    /// <summary>The answer is less than the value.</summary>
    LessThan,

    /// <summary>The answer is greater than or equal to the value.</summary>
    GreaterThanOrEqual,

    /// <summary>The answer is less than or equal to the value.</summary>
    LessThanOrEqual,
}

/// <summary>How the <see cref="InformationItem.EnableWhen"/> conditions of an item combine.</summary>
public enum InformationEnableBehavior
{
    /// <summary>Every condition must hold.</summary>
    All,

    /// <summary>At least one condition must hold.</summary>
    Any,
}

/// <summary>An allowed answer of a choice question.</summary>
[ExcludeFromCodeCoverage]
public sealed record InformationOption
{
    /// <summary>The value submitted as the answer.</summary>
    public required string Value { get; init; }

    /// <summary>The text shown for this option; defaults to <see cref="Value"/> when absent.</summary>
    public string? Display { get; init; }
}

/// <summary>The kind of an <see cref="InformationItem"/> and the answers it accepts.</summary>
public enum InformationItemType
{
    /// <summary>Free text on one line; any non-empty answer.</summary>
    Text,

    /// <summary>Free text that may span lines; any non-empty answer.</summary>
    LongText,

    /// <summary>Yes/no; the answer is <see langword="true"/> or <see langword="false"/>.</summary>
    Boolean,

    /// <summary>A whole number, for example <c>42</c>.</summary>
    WholeNumber,

    /// <summary>A number, for example <c>12.5</c> (invariant culture).</summary>
    Number,

    /// <summary>A date, <c>yyyy-MM-dd</c>.</summary>
    Date,

    /// <summary>A date and time in ISO 8601, for example <c>2024-05-01T13:30:00Z</c>.</summary>
    DateTime,

    /// <summary>One of the item's <see cref="InformationItem.Options"/> (by value).</summary>
    Choice,

    /// <summary>Text to show, with no answer.</summary>
    Display,

    /// <summary>A container for nested items, with no answer of its own.</summary>
    Group,
}

/// <summary>
/// The answers to one item of an <see cref="InformationRequest"/>, inside an
/// <see cref="IInformationStep"/> payload.
/// </summary>
[ExcludeFromCodeCoverage]
public sealed record InformationResponseItem
{
    /// <summary>The <see cref="InformationItem.Id"/> being answered.</summary>
    public required string ItemId { get; init; }

    /// <summary>The answers; more than one only when the item <see cref="InformationItem.Repeats"/>.</summary>
    public IReadOnlyList<InformationAnswer> Answers { get; init; } = [];

    /// <summary>Answers to the nested items of a group, when the client mirrors the request's tree.</summary>
    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}

/// <summary>One answer to a question.</summary>
/// <remarks>
/// Answers are text, interpreted by the question's <see cref="InformationItemType"/>:
/// <see langword="true"/>/<see langword="false"/>, <c>42</c>, <c>12.5</c>, <c>2024-05-01</c>, an ISO 8601 date-time,
/// an option value, or free text. Kaleido checks the shape only; converting answers into
/// internal representations is the handler's job.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record InformationAnswer
{
    /// <summary>The answer, as text.</summary>
    public required string Value { get; init; }
}
