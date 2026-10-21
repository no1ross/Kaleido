using ModelContextProtocol.Protocol;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// A tool's answer: a finished <see cref="CallToolResult"/>, or an <see cref="InputRequiredResult"/>
/// (MRTR) asking the client to collect the user's answers and call the tool again. Both are
/// successful protocol responses.
/// </summary>
internal sealed record ToolOutcome
{
    public CallToolResult? Result { get; init; }

    public InputRequiredResult? InputRequired { get; init; }

    public static implicit operator ToolOutcome(CallToolResult result) => new() { Result = result };
}
