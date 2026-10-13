# 0014. Kaleido emits telemetry only for what Kaleido alone can observe

- **Status:** Accepted
- **Date:** 2026-10-13
- **Related:** #109

## Context
Kaleido's telemetry had grown in two directions. Next to the runtime signals only Kaleido can
produce (process steps, plans, queries, the event relay, store calls, MCP tool calls), `Kaleido.Http`
counted endpoint errors (`kaleido.http.endpoint_errors`) - a signal the host's standard HTTP metrics
already provide (`http.server.request.duration` by status code), with the Kaleido error code as the
only extra, and that code is in the response body and the logs. The meter's name lived in
`Kaleido.Http.Abstractions`, so the OpenTelemetry package referenced HTTP contracts only for it.

The OpenTelemetry package's `AddOpenTelemetry()` also bundled everything: OTLP log export, traces and
metrics, plus the host's ASP.NET Core, HttpClient and runtime instrumentation - with no way to take
Kaleido's traces and metrics without exporting logs over OTLP, or the reverse.

## Decision
- **Principle:** Kaleido emits telemetry only for what only Kaleido can observe - the Process and
  Queryable runtimes, the event outbox and relay, store providers, the MCP adapter. Host and
  transport statistics come from the host's own instrumentation.
- **`kaleido.http.endpoint_errors` is removed**, together with the `Kaleido.Http` meter,
  `KaleidoHttpTelemetry` and `AddKaleidoHttpInstrumentation()`.
- **`Kaleido.Observability.OpenTelemetry` stays an optional add-on**, and `AddOpenTelemetry()` keeps
  its one-call default, but every part can be switched off (`KaleidoOpenTelemetryOptions`: `Logs`,
  `Traces`, `Metrics`, `HostInstrumentation`; also from `Kaleido:OpenTelemetry`).
- **Logging never depends on OpenTelemetry**: Kaleido logs through `ILogger` only.
- **One rule for names:** the OpenTelemetry package references only core; names of optional
  projects (SQLite store, MCP adapter) are copied as internal constants, guarded by tests that compare
  them with the owning constants.

## Alternatives considered
- **Keep the endpoint-error counter** for the Kaleido error-code dimension. Rejected: it duplicates
  the host's HTTP metrics; the code is available in the response and the logs.
- **`AddOpenTelemetry()` with Kaleido's sources only.** Rejected for now: the one-call setup with the
  host instrumentation is what most services want; `HostInstrumentation = false` gives the minimal
  form.
- **Reference the optional projects from the OpenTelemetry package.** Rejected: it would pull EF Core
  and the MCP SDK into every service that only wants Kaleido's telemetry.

## Consequences
- Dashboards built on `kaleido.http.endpoint_errors` move to the host's HTTP metrics by status code.
- `AddOpenTelemetry` takes its options first; callers passing `configureTracing` positionally name it.
- A new Kaleido meter or source must be registered in `AddKaleidoInstrumentation()`, and a copied name
  needs its equality test.
