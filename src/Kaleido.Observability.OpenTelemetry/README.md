# Kaleido.Observability.OpenTelemetry

Optional OpenTelemetry provider for Kaleido. Wires full observability (structured logs, distributed traces, metrics) into a Kaleido service in a single call, exporting via OTLP to any compatible backend (Elastic APM, Grafana, Honeycomb, Jaeger, Datadog, etc.).

## Quick start

```csharp
builder.Services
    .AddKaleido(builder.Configuration, o => { o.ServiceName = "my-service"; ... })
    .AddHttp()
    .AddOpenTelemetry();   // ← this package
```

Configure the OTLP endpoint via standard OTel environment variables:

```
OTEL_EXPORTER_OTLP_ENDPOINT=http://your-backend:4317
OTEL_SERVICE_NAME=my-service          # optional — overrides KaleidoServiceOptions.ServiceName
```

## What `AddOpenTelemetry()` wires

| Signal | Instrumentation |
|---|---|
| Logs | `ILogger` → OTLP (with formatted message + scopes) |
| Traces | Kaleido Process + Queryable + ASP.NET Core + HttpClient → OTLP |
| Metrics | Kaleido Process + Queryable + ASP.NET Core + HttpClient + Runtime → OTLP |

Kaleido histogram instruments are registered with tuned explicit bucket boundaries (replacing the OTel defaults which are unsuitable for small integer values like step counts).

## Granular API

For consumers managing their own OpenTelemetry setup:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(t => t
        .AddKaleidoInstrumentation()   // Kaleido Process + Queryable ActivitySources
        .AddAspNetCoreInstrumentation()
        ...)
    .WithMetrics(m => m
        .AddKaleidoInstrumentation()   // Kaleido Process + Queryable Meters + tuned Views
        .AddAspNetCoreInstrumentation()
        ...);
```

## Provider model

Kaleido core (`Kaleido`, `Kaleido.AspNetCore`) has **no dependency on OpenTelemetry packages**. Instrumentation is emitted via `System.Diagnostics.ActivitySource` and `System.Diagnostics.Metrics.Meter` (BCL). This package wires the OTel SDK to those BCL sources.

Future providers follow the same pattern: `Kaleido.Observability.<Technology>`.
