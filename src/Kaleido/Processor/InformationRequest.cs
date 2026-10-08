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
/// <see cref="InformationRequestId"/> must match, every question must be answered, each answer
/// must fit its item type and choice answers must be offered options.
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

    /// <summary>Yes/no; the answer is <c>true</c> or <c>false</c>.</summary>
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
    Group
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
/// <c>true</c>/<c>false</c>, <c>42</c>, <c>12.5</c>, <c>2024-05-01</c>, an ISO 8601 date-time,
/// an option value, or free text. Kaleido checks the shape only; converting answers into
/// internal representations is the handler's job.
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record InformationAnswer
{
    /// <summary>The answer, as text.</summary>
    public required string Value { get; init; }
}
