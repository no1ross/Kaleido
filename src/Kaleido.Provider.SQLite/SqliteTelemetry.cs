using System.Diagnostics.Metrics;

namespace Kaleido.Provider.SQLite;

/// <summary>
/// Activity source and metric names for the SQLite process context store.
/// </summary>
public static class SqliteTelemetry
{
    // ── ActivitySource / Meter ────────────────────────────────────────────────

    public const string ActivitySourceName = "Kaleido.Provider.SQLite";

    public const string MeterName = "Kaleido.Provider.SQLite";

    // ── Activity names ────────────────────────────────────────────────────────

    public const string LoadActivityName = "kaleido.sqlite.context.load";

    public const string SaveActivityName = "kaleido.sqlite.context.save";

    // ── Tag key names ─────────────────────────────────────────────────────────

    public const string TagProcessId = "kaleido.process.id";

    // ── Metric names ──────────────────────────────────────────────────────────

    public const string LoadFailuresCounterName = "kaleido.sqlite.context.load_failures";

    public const string SaveFailuresCounterName = "kaleido.sqlite.context.save_failures";

    // ── Internal instrumentation singletons ──────────────────────────────────

    internal static readonly ActivitySource ActivitySource =
        new(ActivitySourceName);

    internal static readonly Meter Meter =
        new(MeterName);

    internal static readonly Counter<long> LoadFailuresCounter =
        Meter.CreateCounter<long>(
            LoadFailuresCounterName,
            description: "Number of failed process context load operations.");

    internal static readonly Counter<long> SaveFailuresCounter =
        Meter.CreateCounter<long>(
            SaveFailuresCounterName,
            description: "Number of failed process context save operations.");
}
