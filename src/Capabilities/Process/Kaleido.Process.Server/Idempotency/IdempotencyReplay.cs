namespace Kaleido.Process.Server.Idempotency;

/// <summary>An opaque completed answer, identified by a caller-defined format.</summary>
public sealed class IdempotencyReplay
{
    private readonly byte[] _payload;

    /// <summary>Creates an immutable snapshot of an answer.</summary>
    /// <param name="format">A nonempty format identifier understood by the caller.</param>
    /// <param name="payload">Opaque answer bytes; an empty payload is valid.</param>
    public IdempotencyReplay(string format, byte[] payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(format);
        ArgumentNullException.ThrowIfNull(payload);
        Format = format;
        _payload = (byte[])payload.Clone();
    }

    /// <summary>The caller-defined format identifier.</summary>
    public string Format { get; }

    /// <summary>A copy of the opaque answer bytes.</summary>
    public byte[] Payload => (byte[])_payload.Clone();
}
