namespace Kaleido.Provider.SQLite;

/// <summary>
/// Stable SQLite provider configuration codes, raised as
/// <c>KaleidoConfigurationException</c> at registration: they stop the host and never reach a response.
/// </summary>
public static class SqliteErrorCodes
{
    /// <summary>A SQLite store's connection string cannot be parsed.</summary>
    public const string InvalidConnectionString = "invalid_connection_string";

    /// <summary>
    /// The SQLite stores (<c>AddSqliteContextStore</c>, <c>AddSqliteOutboxStore</c>,
    /// <c>AddSqliteIdempotencyStore</c>) were given different connection strings; they share one database.
    /// </summary>
    public const string ConnectionStringMismatch = "connection_string_mismatch";
}
