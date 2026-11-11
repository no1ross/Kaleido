namespace Kaleido.Process;

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
