namespace Kaleido.Provider.SQLite;

/// <summary>
/// Activity source and metric names for the SQLite process context store: constants only. The store
/// owns the instruments (#525).
/// </summary>
internal static class SqliteTelemetry
{
    // ── ActivitySource / Meter ────────────────────────────────────────────────

    public const string ActivitySourceName = "Kaleido.Provider.SQLite";

    public const string MeterName = "Kaleido.Provider.SQLite";

    // ── Activity names ────────────────────────────────────────────────────────

    public const string LoadActivityName = "kaleido.sqlite.context.load";

    public const string SaveActivityName = "kaleido.sqlite.context.save";

    // ── Tag key names ─────────────────────────────────────────────────────────

    // ── Metric names ──────────────────────────────────────────────────────────

    public const string LoadFailuresCounterName = "kaleido.sqlite.context.load_failures";

    public const string SaveFailuresCounterName = "kaleido.sqlite.context.save_failures";
}
