using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Kaleido.Http.Mcp.Observability;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.WebUtilities;

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
    /// The state, when <paramref name="value"/> was protected for the current caller with a key this
    /// instance has, and has not expired; null otherwise. Every refusal is observed with its reason.
    /// </summary>
    McpLookupState? Unprotect(string? value);
}

// ASP.NET Data Protection (encrypted and signed) keeps the adapter stateless: instances behind a load
// balancer share one key ring (docs/guide/deployment.md). A state protected with a key this instance
// does not have is the one refusal that is a deployment problem, so it is told apart from the client's.
internal sealed class McpLookupStateProtector(
    IDataProtectionProvider provider,
    IKeyManager keys,
    IKaleidoCorrelationContextAccessor correlation,
    IMcpObservability observability)
    : IMcpLookupStateProtector
{
    internal const string Prefix = "kaleido-mcp-lookup:";

    // Far more than the rows of one pick list; a larger value is refused before it is decoded.
    internal const int MaxLength = 64 * 1024;

    internal const string Purpose = "Kaleido.Http.Mcp.LookupState.v1";

    // A lookup is a few form rounds with the user; an older state is refused.
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    // A Data Protection payload starts with this magic number and the id of the key that protected it.
    private const uint PayloadMagic = 0x09F0C9F0;

    private readonly IDataProtector _protector = provider.CreateProtector(Purpose);

    public string Protect(McpLookupState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return Prefix + _protector.Protect(
            JsonSerializer.Serialize(
                state with { Caller = Caller(), ExpiresUtc = DateTimeOffset.UtcNow + Lifetime },
                KaleidoJsonOptions.Options));
    }

    public McpLookupState? Unprotect(string? value)
    {
        if (value is null ||
            value.Length > MaxLength ||
            !value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return Refused(McpTelemetry.LookupStateInvalid);
        }

        var payload = value[Prefix.Length..];
        McpLookupState? state;

        try
        {
            state = JsonSerializer.Deserialize<McpLookupState>(_protector.Unprotect(payload), KaleidoJsonOptions.Options);
        }
        catch (CryptographicException)
        {
            return KeyId(payload) is { } keyId && keys.GetAllKeys().All(x => x.KeyId != keyId)
                ? Refused(McpTelemetry.LookupStateUnknownKey, keyId)
                : Refused(McpTelemetry.LookupStateInvalid);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return Refused(McpTelemetry.LookupStateInvalid);
        }

        return state switch
        {
            null => Refused(McpTelemetry.LookupStateInvalid),
            { ExpiresUtc: var expires } when expires <= DateTimeOffset.UtcNow => Refused(McpTelemetry.LookupStateExpired),
            _ when !string.Equals(state.Caller, Caller(), StringComparison.Ordinal) => Refused(McpTelemetry.LookupStateAnotherCaller),
            _ => state,
        };
    }

    private McpLookupState? Refused(string reason, Guid? keyId = null)
    {
        observability.LookupStateRefused(reason, keyId);

        return null;
    }

    // The id of the key that protected the payload, read from its header; null when it is not a Data
    // Protection payload at all.
    private static Guid? KeyId(string payload)
    {
        try
        {
            var bytes = WebEncoders.Base64UrlDecode(payload);

            return bytes.Length >= 20 && BinaryPrimitives.ReadUInt32BigEndian(bytes) == PayloadMagic
                ? new Guid(bytes.AsSpan(4, 16))
                : null;
        }
        catch (FormatException)
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

    /// <summary>Until when the state is accepted; set by the protector.</summary>
    public DateTimeOffset ExpiresUtc { get; init; }
}

/// <summary>One row of a lookup's pick list: its label and the values that fill the step's inputs.</summary>
/// <param name="Label">What the user sees.</param>
/// <param name="Values">The ids that fill the inputs; never shown to the model.</param>
internal sealed record McpLookupCandidate(
    string Label,
    IReadOnlyDictionary<string, JsonElement> Values);
