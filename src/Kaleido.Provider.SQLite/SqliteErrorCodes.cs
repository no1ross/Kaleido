namespace Kaleido.Provider.SQLite;

/// <summary>
/// Stable SQLite provider configuration codes, raised as
/// <c>KaleidoConfigurationException</c> at registration: they stop the host and never reach a response.
/// </summary>
public static class SqliteErrorCodes
{
    /// <summary>The SQLite process context store connection string cannot be parsed.</summary>
    public const string InvalidConnectionString = "invalid_connection_string";
}
