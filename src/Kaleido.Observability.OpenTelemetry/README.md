# Kaleido.Observability.OpenTelemetry

Optional OpenTelemetry provider for Kaleido. Wires full observability (structured logs, distributed traces, metrics) into a Kaleido service in a single call, exporting via OTLP to any compatible backend (Elastic APM, Grafana, Honeycomb, Jaeger, Datadog, etc.).

> How this project fits into a whole service, with tested examples: the [consumer guide](../../docs/guide/README.md). This README is the reference for the project.

## Quick start

```csharp
builder.Services
    .AddKaleido(builder.Configuration, o => { o.ServiceName = "myservice"; ... })
    .AddHttp()
    .AddOpenTelemetry();   // ← this package
```

### Where the telemetry goes

Configure the export and the resource under `Kaleido:OpenTelemetry` (configuration first, then the options delegate):

```json
{
  "Kaleido": {
    "ServiceName": "intake",
    "OpenTelemetry": {
      "Endpoint": "http://localhost:4317",
      "Protocol": "grpc",
      "ServiceVersion": "1.0.0",
      "ResourceAttributes": "deployment.environment=Development"
    }
  }
}
```

| Setting | Effect | Default |
|---|---|---|
| `Endpoint` | OTLP collector base address; with `http/protobuf` each signal's `/v1/traces`, `/v1/metrics`, `/v1/logs` is appended | SDK default, `http://localhost:4317` |
| `Protocol` | `grpc` or `http/protobuf` (anything else fails startup with `otel_invalid_options`) | gRPC |
| `Headers` | `key=value,...` sent with every export (for example a backend's API key) | none |
| `ServiceVersion` | `service.version` | none |
| `ResourceAttributes` | further resource attributes, `key=value,...` | none |

The resource's `service.name` is the Kaleido `ServiceName` and `service.instance.id` its `InstanceId`; there is no second service name to keep in step.

**The standard OpenTelemetry keys win:** `OTEL_EXPORTER_OTLP_ENDPOINT` (or a signal's `OTEL_EXPORTER_OTLP_{SIGNAL}_ENDPOINT`), `OTEL_EXPORTER_OTLP_PROTOCOL`, `OTEL_EXPORTER_OTLP_HEADERS`, `OTEL_SERVICE_NAME` and `OTEL_RESOURCE_ATTRIBUTES`, as environment variables or configuration keys. A deployment (a container, compose) overrides `appsettings.json` with them without a code change:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://your-backend:4317
```

## What `AddOpenTelemetry()` wires

| Signal | Instrumentation |
|---|---|
| Logs | `ILogger` -> OTLP (with formatted message + scopes) |
| Traces | Kaleido Process + Queryable + SQLite store + MCP adapter, plus ASP.NET Core + HttpClient -> OTLP |
| Metrics | Kaleido Process + Queryable + events + SQLite store + MCP adapter, plus ASP.NET Core + HttpClient + runtime -> OTLP |

Kaleido histogram instruments are registered with tuned explicit bucket boundaries (replacing the OTel defaults which are unsuitable for small integer values like step counts).

### Adding instrumentation

`AddOpenTelemetry(configure, configureTracing, configureMetrics)` also takes hooks for extra sources (EF Core, SQL Client, gRPC ...). They run after the built-in instrumentations and before the OTLP exporter, and are not called when traces (or metrics) are switched off:

```csharp
builder.Services.AddKaleido(builder.Configuration, o => { ... })
    .AddOpenTelemetry(
        configureTracing: tracing => tracing.AddEntityFrameworkCoreInstrumentation(),
        configureMetrics: metrics => metrics.AddMeter("MyCompany.Billing"));
```

### Switching parts off

Everything is on by default. `KaleidoOpenTelemetryOptions` switches each part off - in code or under `Kaleido:OpenTelemetry` (configuration first, then the delegate), so operations can change it without a release:

| Option | Default | Off means |
|---|---|---|
| `Logs` | `true` | no OTLP log export; `ILogger` still writes to the host's other providers (console, Serilog, Seq ...) |
| `Traces` | `true` | no tracer provider, no trace export |
| `Metrics` | `true` | no meter provider, no metric export |
| `HostInstrumentation` | `true` | only Kaleido's own sources and meters - no ASP.NET Core, HttpClient or runtime instrumentation |

```csharp
builder.Services.AddKaleido(builder.Configuration)
    .AddOpenTelemetry(o => o.Logs = false);   // traces + metrics over OTLP, logs go elsewhere
```

```json
{ "Kaleido": { "OpenTelemetry": { "HostInstrumentation": false } } }
```

With all three signals off, nothing is registered.

## What Kaleido measures

Kaleido emits telemetry only for what only Kaleido can observe: the Process and Queryable runtimes, the event outbox and relay, store providers and the MCP adapter ([ADR 0014](../../docs/decisions/0014-kaleido-telemetry-only-what-kaleido-observes.md)). Host and transport statistics - request rates, status codes, latencies - come from the host's own instrumentation (ASP.NET Core, HttpClient), which `HostInstrumentation` adds for convenience. There is no Kaleido HTTP meter: Kaleido's error codes are in the response body and the logs.

## Granular API

For consumers managing their own OpenTelemetry setup:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddKaleidoInstrumentation()   // all Kaleido ActivitySources
        .AddAspNetCoreInstrumentation()
        ...)
    .WithMetrics(m => m
        .AddKaleidoInstrumentation()   // all Kaleido Meters + tuned Views
        .AddAspNetCoreInstrumentation()
        ...);
```

Per area: `AddKaleidoProcessorInstrumentation()`, `AddKaleidoQueryableInstrumentation()`, `AddKaleidoSqliteInstrumentation()`, `AddKaleidoMcpInstrumentation()`.

### Telemetry names

This package references only Kaleido core, whose constants it uses directly (`ProcessorTelemetry`, `QueryableTelemetry`, `KaleidoEventTelemetry`). The names of optional projects (the SQLite store, the MCP adapter) are copied as internal constants so the package does not pull in EF Core or the MCP SDK; `Kaleido.Observability.OpenTelemetry.UnitTests` asserts every copy equals the owning project's constant.
## Provider model

Kaleido core (`Kaleido`) has **no dependency on OpenTelemetry packages**. Instrumentation is emitted via `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter` (BCL). This package wires the OTel SDK to those BCL sources.

Future providers follow the same pattern: `Kaleido.Observability.<Technology>`.
