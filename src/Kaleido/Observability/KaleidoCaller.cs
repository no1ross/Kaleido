namespace Kaleido.Observability;

/// <summary>
/// The authenticated caller, exactly as the request principal provides it: what Kaleido stamps on
/// published events (the event context's <c>Caller</c>) and on process and step state
/// (<c>CreatedBy</c>, <c>UpdatedBy</c>, <c>StartedBy</c>, <c>LastUpdatedBy</c>).
/// </summary>
/// <remarks>
/// <para>
/// Kaleido only tags: it never decides anything from these values beyond the declared step and
/// query rules, and it keeps no "who did what" history - downstream systems build that from the
/// tagged events.
/// </para>
/// <para>
/// The caller is personal data. It is published unclassified on every event; a consumer that must
/// not publish it removes it in an <c>IKaleidoEventRedactor</c>.
/// </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public sealed record KaleidoCaller
{
    /// <summary>The caller's name (<c>Identity.Name</c>), if the principal has one.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// The caller's stable subject id - the <c>sub</c> claim, else <c>ClaimTypes.NameIdentifier</c> -
    /// if the principal has one. Passed through, not interpreted.
    /// </summary>
    public string? Id { get; init; }
}
