namespace Kaleido.Queryable.AspNetCore;

/// <summary>
/// Exception thrown when a requested queryable registration cannot be found.
/// </summary>
public sealed class KaleidoQueryableRegistrationNotFoundException : Exception
{
    public KaleidoQueryableRegistrationNotFoundException(string key)
        : base($"Queryable registration '{key}' was not found.")
    {
        Key = key;
    }

    public string Key { get; }
}
