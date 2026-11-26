namespace Kaleido.Provider.SQLite;

/// <summary>
/// Stable SQLite provider configuration and persisted-schema codes, raised as
/// <c>KaleidoConfigurationException</c>. Invalid registration or stored data cannot be treated as a successful replay.
/// </summary>
public static class SqliteErrorCodes
{
    /// <summary>A SQLite store's connection string cannot be parsed.</summary>
    public const string InvalidConnectionString = "invalid_connection_string";

    /// <summary>The named SQLite connection string is missing from the builder configuration.</summary>
    public const string MissingConnectionString = "sqlite_missing_connection_string";

    /// <summary>A stored row lacks fields required by the current SQLite provider schema.</summary>
    public const string InvalidStoredRecord = "sqlite_invalid_stored_record";

    /// <summary>
    /// The SQLite stores (<c>AddSqliteContextProvider</c>, <c>AddSqliteOutboxProvider</c>,
    /// <c>AddSqliteIdempotencyProvider</c>) were given different connection strings; they share one database.
    /// </summary>
    public const string ConnectionStringMismatch = "connection_string_mismatch";
}
