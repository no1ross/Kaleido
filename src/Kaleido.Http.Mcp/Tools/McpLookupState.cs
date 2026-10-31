using System.Text.Json;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// The state of a guided lookup between MRTR form rounds; protected by <see cref="IMcpStateProtector"/>
/// as a <see cref="McpStateKind.Lookup"/> state.
/// </summary>
/// <param name="Tool">The step tool the lookup fills.</param>
/// <param name="Group">The lookup group of that tool.</param>
/// <param name="ProcessId">The process the step continues, when it does.</param>
/// <param name="Arguments">The original call's arguments, serialized.</param>
/// <param name="SearchText">What the user searched for; null before the search.</param>
/// <param name="Candidates">The rows shown to pick from.</param>
[ExcludeFromCodeCoverage]
internal sealed record McpLookupState(
    string Tool,
    string Group,
    Guid? ProcessId,
    string Arguments,
    string? SearchText,
    McpLookupCandidate[] Candidates);

/// <summary>One row of a lookup's pick list: its label and the values that fill the step's inputs.</summary>
/// <param name="Label">What the user sees.</param>
/// <param name="Values">The ids that fill the inputs; never shown to the model.</param>
[ExcludeFromCodeCoverage]
internal sealed record McpLookupCandidate(
    string Label,
    IReadOnlyDictionary<string, JsonElement> Values);
