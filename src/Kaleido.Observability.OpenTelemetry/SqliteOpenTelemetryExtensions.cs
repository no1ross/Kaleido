namespace Kaleido.Observability.OpenTelemetry;

/// <summary>
/// Registers the SQLite provider's ActivitySource and Meter for consumers
/// managing their own OTel pipeline. The names are duplicated here rather
/// than referencing Kaleido.Provider.SQLite so this package does not take
/// an EF Core dependency for consumers who do not use the SQLite store —
/// they are part of the provider's stable telemetry contract
/// (<see cref="SqliteTelemetryNames"/>).
/// </summary>
internal static class SqliteTelemetryNames
{
    // Must match SqliteTelemetry.ActivitySourceName / MeterName in
    // Kaleido.Provider.SQLite — both are the string "Kaleido.Provider.SQLite".
    public const string ActivitySourceName = "Kaleido.Provider.SQLite";
    public const string MeterName = "Kaleido.Provider.SQLite";
}

public static class SqliteOpenTelemetryExtensions
{
    /// <summary>Registers the SQLite provider's <c>ActivitySource</c>. Requires the Kaleido.Provider.SQLite package to be in use for any signals to be emitted.</summary>
    public static TracerProviderBuilder AddKaleidoSqliteInstrumentation(
        this TracerProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddSource(
            SqliteTelemetryNames.ActivitySourceName);
    }

    /// <summary>Registers the SQLite provider's <c>Meter</c>. Requires the Kaleido.Provider.SQLite package to be in use for any signals to be emitted.</summary>
    public static MeterProviderBuilder AddKaleidoSqliteInstrumentation(
        this MeterProviderBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.AddMeter(
            SqliteTelemetryNames.MeterName);
    }
}
