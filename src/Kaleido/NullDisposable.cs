namespace Kaleido;

/// <summary>
/// A singleton no-op <see cref="IDisposable"/> used as a safe fallback
/// when an observable scope or activity is not available.
/// </summary>
[ExcludeFromCodeCoverage]
internal sealed class NullDisposable : IDisposable
{
    /// <summary>The shared singleton instance.</summary>
    public static readonly NullDisposable Instance = new();

    private NullDisposable() { }

    /// <inheritdoc/>
    public void Dispose() { }
}
