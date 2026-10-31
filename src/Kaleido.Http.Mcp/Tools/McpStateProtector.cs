using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Kaleido.Http.Mcp.Observability;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.WebUtilities;

namespace Kaleido.Http.Mcp.Tools;

/// <summary>The kinds of state the adapter hands to the MCP client and reads back.</summary>
internal enum McpStateKind
{
    /// <summary>A guided lookup between form rounds (#452): the rows to pick from and the call to fill.</summary>
    Lookup,

    /// <summary>An MRTR answers form: the process, request, exchange and earlier answers.</summary>
    Continuation,

    /// <summary>The <c>answersToken</c> the agent passes back (ADR 0024): the exchange and earlier answers.</summary>
    Answers,

    /// <summary>An MRTR confirmation form (#439): exactly what runs once the user accepts.</summary>
    Confirmation,
}

/// <summary>
/// Protects every state that goes round trip through the MCP client (#452, #516): the client must
/// neither read nor change it, nor hand one caller's state to another, nor replay it later.
/// </summary>
internal interface IMcpStateProtector
{
    /// <summary>
    /// Encrypts and signs <paramref name="state"/> as a <paramref name="kind"/> state for the current
    /// caller, valid for <see cref="McpStateProtector.Lifetime"/>.
    /// </summary>
    string Protect<T>(McpStateKind kind, T state)
        where T : class;

    /// <summary>
    /// The state, when <paramref name="value"/> was protected as a <paramref name="kind"/> state for the
    /// current caller with a key this instance has, and has not expired; null otherwise. Every refusal
    /// is observed with its kind and reason.
    /// </summary>
    T? Unprotect<T>(McpStateKind kind, string? value)
        where T : class;
}

// ASP.NET Data Protection (encrypted and signed) keeps the adapter stateless: instances behind a load
// balancer share one key ring (docs/guide/deployment.md). A state protected with a key this instance
// does not have is the one refusal that is a deployment problem, so it is told apart from the client's.
// Each kind has its own purpose: a state of one kind is never accepted as another.
internal sealed class McpStateProtector(
    IDataProtectionProvider provider,
    IKeyManager keys,
    IKaleidoCorrelationContextAccessor correlation,
    IMcpObservability observability,
    TimeProvider time)
    : IMcpStateProtector
{
    // Far more than the rows of one pick list or the answers of one questionnaire; a larger value is
    // refused before it is decoded.
    internal const int MaxLength = 64 * 1024;

    // A form round is a few minutes with the user; an older state is refused.
    internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    // A Data Protection payload starts with this magic number and the id of the key that protected it.
    private const uint PayloadMagic = 0x09F0C9F0;

    /// <summary>What a value of the kind starts with, so the adapter routes it without decoding it.</summary>
    internal static string Prefix(McpStateKind kind) =>
        kind switch
        {
            McpStateKind.Lookup => "kaleido-mcp-lookup:",
            McpStateKind.Continuation => "kaleido-mcp-state:",
            McpStateKind.Answers => "kaleido-mcp-answers:",
            _ => "kaleido-mcp-confirm:",
        };

    /// <summary>The Data Protection purpose of the kind.</summary>
    internal static string Purpose(McpStateKind kind) => $"Kaleido.Http.Mcp.{kind}State.v1";

    /// <summary>The kind's value of the <c>kaleido.mcp.state.kind</c> tag.</summary>
    internal static string TagValue(McpStateKind kind) =>
        kind switch
        {
            McpStateKind.Lookup => McpTelemetry.StateKindLookup,
            McpStateKind.Continuation => McpTelemetry.StateKindContinuation,
            McpStateKind.Answers => McpTelemetry.StateKindAnswers,
            _ => McpTelemetry.StateKindConfirmation,
        };

    public string Protect<T>(McpStateKind kind, T state)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(state);

        return Prefix(kind) + provider.CreateProtector(Purpose(kind)).Protect(
            JsonSerializer.Serialize(
                new Envelope<T>(state, Caller(), time.GetUtcNow() + Lifetime),
                KaleidoJsonOptions.Options));
    }

    public T? Unprotect<T>(McpStateKind kind, string? value)
        where T : class
    {
        var prefix = Prefix(kind);

        if (value is null ||
            value.Length > MaxLength ||
            !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return Refused<T>(kind, McpTelemetry.StateInvalid);
        }

        var payload = value[prefix.Length..];
        Envelope<T>? envelope;

        try
        {
            envelope = JsonSerializer.Deserialize<Envelope<T>>(provider.CreateProtector(Purpose(kind)).Unprotect(payload), KaleidoJsonOptions.Options);
        }
        catch (CryptographicException)
        {
            return KeyId(payload) is { } keyId && keys.GetAllKeys().All(x => x.KeyId != keyId)
                ? Refused<T>(kind, McpTelemetry.StateUnknownKey, keyId)
                : Refused<T>(kind, McpTelemetry.StateInvalid);
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return Refused<T>(kind, McpTelemetry.StateInvalid);
        }

        return envelope switch
        {
            null or { State: null } => Refused<T>(kind, McpTelemetry.StateInvalid),
            { ExpiresUtc: var expires } when expires <= time.GetUtcNow() => Refused<T>(kind, McpTelemetry.StateExpired),
            _ when !string.Equals(envelope.Caller, Caller(), StringComparison.Ordinal) => Refused<T>(kind, McpTelemetry.StateAnotherCaller),
            _ => envelope.State,
        };
    }

    private T? Refused<T>(McpStateKind kind, string reason, Guid? keyId = null)
        where T : class
    {
        observability.StateRefused(TagValue(kind), reason, keyId);

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

    // What is protected: the state, the caller it was issued to and until when it is accepted.
    private sealed record Envelope<T>(T? State, string Caller, DateTimeOffset ExpiresUtc);
}
