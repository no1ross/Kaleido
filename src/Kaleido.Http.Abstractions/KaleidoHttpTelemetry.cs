namespace Kaleido.Http;

public static class KaleidoHttpTelemetry
{
    // ── Meter ─────────────────────────────────────────────────────────────────

    public const string MeterName =
        "Kaleido.Http";

    // ── Metric names ──────────────────────────────────────────────────────────

    public const string EndpointErrorsCounterName =
        "kaleido.http.endpoint_errors";

    // ── Tag names ─────────────────────────────────────────────────────────────

    public const string TagErrorCode =
        "kaleido.error.code";

    public const string TagHttpStatusCode =
        "http.response.status_code";
}
