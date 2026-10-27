using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>
/// Protects the guided-lookup state that goes round trip through the MCP client between form rounds
/// (MRTR <c>requestState</c>, #452). The state holds the candidate rows whose ids fill the step's
/// inputs, the process and the original arguments, so the client must neither read nor change it.
/// </summary>
internal interface IMcpLookupStateProtector
{
    /// <summary>Encrypts and signs <paramref name="state"/> for the current caller, valid for <see cref="McpLookupStateProtector.Lifetime"/>.</summary>
    string Protect(McpLookupState state);

    /// <summary>
    /// The state, when <paramref name="value"/> was protected here for the current caller and has not
    /// expired; null when it is missing, oversized, changed, expired or another caller's.
    /// </summary>
    McpLookupState? Unprotect(string? value);
}

// ASP.NET Data Protection (encrypted and signed, time-limited) keeps the adapter stateless: a host with
// several instances shares its key ring (PersistKeysTo...), as for any protected payload.
internal sealed class McpLookupStateProtector(
    IDataProtectionProvider provider,
    IKaleidoCorrelationContextAccessor correlation)
    : IMcpLookupStateProtector
{
    internal const string Prefix = "kaleido-mcp-lookup:";

    // Far more than the rows of one pick list; a larger value is refused before it is decoded.
    internal const int MaxLength = 64 * 1024;

    // A lookup is a few form rounds with the user; an older state is refused.
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    internal const string Purpose = "Kaleido.Http.Mcp.LookupState.v1";

    private readonly ITimeLimitedDataProtector _protector =
        provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();

    public string Protect(McpLookupState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Prefix + _protector.Protect(
            JsonSerializer.Serialize(state with { Caller = Caller() }, KaleidoJsonOptions.Options),
            Lifetime);
    }

    public McpLookupState? Unprotect(string? value)
    {
        if (value is null ||
            value.Length > MaxLength ||
            !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            var state = JsonSerializer.Deserialize<McpLookupState>(
                _protector.Unprotect(value[Prefix.Length..]),
                KaleidoJsonOptions.Options);

            return state is not null && string.Equals(state.Caller, Caller(), StringComparison.Ordinal)
                ? state
                : null;
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException or JsonException)
        {
            return null;
        }
    }

    // The same caller identity the idempotency keys use: the stable subject id, else the name.
    private string Caller() =>
        correlation.Current switch
        {
            { CallerId: { } id } => $"id:{id}",
            { CallerName: { } name } => $"name:{name}",
            _ => "anonymous",
        };
}

/// <summary>The state of a guided lookup between MRTR form rounds; protected by <see cref="IMcpLookupStateProtector"/>.</summary>
/// <param name="Tool">The step tool the lookup fills.</param>
/// <param name="Group">The lookup group of that tool.</param>
/// <param name="ProcessId">The process the step continues, when it does.</param>
/// <param name="Arguments">The original call's arguments, serialized.</param>
/// <param name="SearchText">What the user searched for; null before the search.</param>
/// <param name="Candidates">The rows shown to pick from.</param>
internal sealed record McpLookupState(
    string Tool,
    string Group,
    Guid? ProcessId,
    string Arguments,
    string? SearchText,
    McpLookupCandidate[] Candidates)
{
    /// <summary>The caller the state was issued to; set by the protector.</summary>
    public string Caller { get; init; } = string.Empty;
}

/// <summary>One row of a lookup's pick list: its label and the values that fill the step's inputs.</summary>
/// <param name="Label">What the user sees.</param>
/// <param name="Values">The ids that fill the inputs; never shown to the model.</param>
internal sealed record McpLookupCandidate(
    string Label,
    IReadOnlyDictionary<string, JsonElement> Values);
