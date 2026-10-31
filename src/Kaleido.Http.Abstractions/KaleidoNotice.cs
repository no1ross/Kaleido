namespace Kaleido.Http;

/// <summary>
/// Something the caller should know about a response that is not an error: the response is
/// complete for this caller, but leaves something out on purpose (for example a next step the caller
/// may not run, code <c>pro_not_permitted</c>). Sent whatever the transport's message options.
/// </summary>
/// <param name="Code">A stable machine-readable code (e.g. <c>"pro_not_permitted"</c>).</param>
/// <param name="Message">A human-readable explanation, for the caller.</param>
[ExcludeFromCodeCoverage]
public sealed record KaleidoNotice(
    string Code,
    string Message);
