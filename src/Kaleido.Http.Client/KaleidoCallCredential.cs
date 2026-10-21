namespace Kaleido.Http.Client;

/// <summary>The credential a Kaleido client call sends, as decided by an <see cref="IKaleidoCallCredentialProvider"/>.</summary>
public sealed record KaleidoCallCredential
{
    private KaleidoCallCredential(KaleidoCallCredentialKind kind, string? value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>Send no <c>Authorization</c> header.</summary>
    public static KaleidoCallCredential None { get; } = new(KaleidoCallCredentialKind.None, value: null);

    /// <summary>
    /// Keep what the host's client handlers set (e.g. a service token on the client). Refused for a
    /// <see cref="KaleidoCallPurpose.OnBehalfOfCaller"/> call: a caller's call never goes out with the
    /// host's own credentials.
    /// </summary>
    public static KaleidoCallCredential Configured { get; } = new(KaleidoCallCredentialKind.Configured, value: null);

    /// <summary>What to do with the call's <c>Authorization</c> header.</summary>
    public KaleidoCallCredentialKind Kind { get; }

    /// <summary>The header value for <see cref="KaleidoCallCredentialKind.Header"/>; otherwise <see langword="null"/>.</summary>
    public string? Value { get; }

    /// <summary>Send <paramref name="authorization"/> (e.g. <c>Bearer eyJ…</c>) as the <c>Authorization</c> header.</summary>
    /// <exception cref="ArgumentException"><paramref name="authorization"/> is null, empty or whitespace.</exception>
    public static KaleidoCallCredential Header(string authorization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(authorization);
        return new KaleidoCallCredential(KaleidoCallCredentialKind.Header, authorization);
    }
}
