# Kaleido.AspNetCore

This project contains Kaleido's ASP.NET Core DI registration and transport services. It wires the Process and Queryable runtimes into an ASP.NET Core application and provides the request-handling layer that sits between HTTP endpoints and the core runtime.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Kaleido/README.md`](../Kaleido/README.md)
- [`../Kaleido.Http/README.md`](../Kaleido.Http/README.md)

---

## What lives here

### Shared infrastructure
- `AspNetCoreServiceCollectionExtensions` — `UseKaleidoExceptionHandling()` pipeline registration
- `ExceptionMiddleware` — shared exception middleware (catches `KaleidoFrameworkException`, `ArgumentException`)
- `KaleidoAspNetCoreCorrelation` / `KaleidoAspNetCoreHeaders` — shared correlation-header names and parsing
- `ApiErrorContract` — shared HTTP error response shape

### Queryable ASP.NET Core
- `QueryableAspNetCoreServiceCollectionExtensions` — `AddQueryableAspNetCore(...)` extension
- `QueryableValueNormalizer` — normalizes incoming filter values to real CLR field types before query execution

### Process ASP.NET Core
- `ProcessAspNetCoreServiceCollectionExtensions` — `AddProcessorAspNetCore(...)` extension
- `ProcessExecutionService` — adapts HTTP execute requests into runtime `ProcessRequest` values
- `ProcessStateService` — adapts HTTP state requests to runtime state reads

---

## What this project is for

Reference this project when you need to:
- add the shared Kaleido exception middleware to the pipeline with `UseKaleidoExceptionHandling()`
- register Queryable ASP.NET Core services with `AddQueryableAspNetCore(...)`
- register Process ASP.NET Core services with `AddProcessorAspNetCore(...)`
- work with correlation-header parsing or the shared error response shape

## What this project is NOT for

This project does not contain:
- HTTP endpoint route mapping — that is [`Kaleido.Http`](../Kaleido.Http/README.md)
- HTTP contract types — that is [`Kaleido.Http.Abstractions`](../Kaleido.Http.Abstractions/README.md)
- The core Queryable/Process runtime — that is [`Kaleido`](../Kaleido/README.md)

---

## Registration pattern

```csharp
// Shared exception middleware (optional)
app.UseKaleidoExceptionHandling();

// Queryable ASP.NET Core transport services
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddQueryable()
        .AddQueryableAspNetCore();

// Process ASP.NET Core transport services
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddProcessor(options => { ... })
        .AddProcessorAspNetCore();
```

---

## Exception middleware

`ExceptionMiddleware` catches the following exception types:

| Exception | HTTP status | Error code |
|-----------|-------------|------------|
| `KaleidoFrameworkException` | 500 | `framework_error` |
| `ArgumentException` | 400 | `argument_error` |

`KaleidoFrameworkException` is thrown by internal runtime paths that detect broken DI wiring or unexpected type mismatches. It is not a user-input error.

`QueryableValidationException` subtypes (user query-input errors) are handled directly at the Queryable endpoint level in `Kaleido.Http` and do not reach this middleware.

---

## Correlation headers

`KaleidoAspNetCoreHeaders` defines the shared header names used to populate `KaleidoCorrelationContext`.

`KaleidoAspNetCoreCorrelation.Create(HttpContext)` reads those headers and parses GUID-backed values, failing fast on malformed GUIDs.

---

## Where to look

- `AspNetCoreServiceCollectionExtensions.cs` — exception middleware registration
- `Middleware/ExceptionMiddleware.cs` — exception handling behavior
- `Observability/KaleidoAspNetCoreCorrelation.cs` — correlation-header parsing
- `QueryableAspNetCoreServiceCollectionExtensions.cs` — Queryable transport DI
- `QueryableValueNormalizer.cs` — filter value normalization
- `ProcessAspNetCoreServiceCollectionExtensions.cs` — Process transport DI
- `Services/ProcessExecutionService.cs` — HTTP-to-runtime execution adapter
- `Services/ProcessStateService.cs` — HTTP-to-runtime state adapter
