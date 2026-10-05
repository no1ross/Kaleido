namespace Kaleido.Provider.SQLite;

/// <summary>
/// Stable SQLite provider configuration codes. Log-only — raised as
/// <c>KaleidoConfigurationException</c> at registration.
/// </summary>
public static class SqliteErrorCodes
{
    /// <summary>The SQLite process context store connection string cannot be parsed.</summary>
    public const string InvalidConnectionString = "invalid_connection_string";
}
