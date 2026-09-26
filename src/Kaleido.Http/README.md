# Kaleido.Http

This project publishes all Kaleido HTTP endpoints. It maps Process, Queryable, and Registry routes onto an ASP.NET Core application.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Kaleido/README.md`](../Kaleido/README.md)
- [`../Kaleido.Http.Abstractions/README.md`](../Kaleido.Http.Abstractions/README.md)

---

## What lives here

### Registration and middleware
- `KaleidoHttpServiceCollectionExtensions` — `AddHttp()` entry point; registers routing, `IHttpContextAccessor`, the middleware pipeline, and HTTP execution services
- `KaleidoStartupFilter` — registers middlewares via `IStartupFilter` in the correct pipeline order
- `ExceptionMiddleware` — outermost middleware; maps exceptions to JSON error responses
- `ObservabilityMiddleware` — populates `IKaleidoCorrelationContextAccessor` from inbound headers and echoes correlation headers on the response
- `HttpCorrelationContextReader` — reads and sanitizes inbound HTTP headers

### Queryable endpoint mapping
- `QueryableEndpointRouteBuilderExtensions` — `MapQueryable()` extension
  - `GET /{prefix}/queryable` — catalog (summary records for discoverable contexts)
  - `GET /{prefix}/queryable/registry` — full context/view metadata
  - `GET /{prefix}/queryable/{context}/{metadataRoute}` — per-context metadata
  - `POST /{prefix}/queryable/{context}/{queryRoute}` — direct context query (Direct contexts only)
  - `POST /{prefix}/queryable/{context}/{view}/{queryRoute}` — local or delegated view query

### Process endpoint mapping
- `ProcessEndpointRouteBuilderExtensions` — `MapProcessor()` extension
  - `GET /{prefix}/processes` — processor catalog (initial steps grouped by processor)
  - `GET /{prefix}/processes/steps` — step catalog (lightweight summary of all steps)
  - `GET /{prefix}/processes/registry` — full processor registry (all step metadata)
  - `GET /{prefix}/processes/steps/{step}/metadata` — per-step metadata
  - `POST /{prefix}/processes/execute` — multi-step execute endpoint
  - `GET /{prefix}/processes/{processId}` — process state
  - `POST /{prefix}/processes/steps/{step}` — per-step execute endpoint

### Registry endpoint mapping
- `RegistryEndpointRouteBuilderExtensions` — `MapRegistry()` extension
  - `GET /{prefix}/registry` — aggregated discovery combining local process + all downstream process clients + all downstream queryable clients

### URL and route helpers
- `ProcessContractUrls` / `ProcessRoutePaths` — URL generation for Process metadata and execute URLs
- `QueryableContractUrls` / `QueryableRoutePaths` — URL generation for Queryable metadata and query URLs

---

## What this project is for

Reference this project when you need to:
- publish Queryable endpoints with `MapQueryable()`
- publish Process endpoints with `MapProcessor()`
- publish the aggregated Registry endpoint with `MapRegistry()`
- work with the URL/path generation helpers for Process or Queryable routes

## What this project is NOT for

This project does not contain:
- runtime business logic — that is [`Kaleido`](../Kaleido/README.md)
- HTTP contract types — that is [`Kaleido.Http.Abstractions`](../Kaleido.Http.Abstractions/README.md)
- remote HTTP client consumption — that is [`Kaleido.Http.Client`](../Kaleido.Http.Client/README.md)

---

## Usage

```csharp
builder.Services.AddKaleido(builder.Configuration, o =>
{
    o.ServiceName = "my-service";
    o.Assemblies = new[] { typeof(Program).Assembly };
})
    .AddHttp();

var app = builder.Build();
app.MapQueryable();
app.MapProcessor();
app.MapRegistry(); // optional aggregated discovery
```

`AddHttp()` wires the middleware pipeline automatically via `KaleidoStartupFilter` — no manual `Use...()` call is needed.

---

## Authorization

`MapProcessor()`, `MapQueryable()`, and `MapRegistry()` return `IEndpointRouteBuilder` (the same value they receive), so you cannot chain `.RequireAuthorization()` directly on them.

Use the standard ASP.NET Core `MapGroup()` pattern instead — wrap first, then map:

```csharp
// Require authorization on all Process endpoints
app.MapGroup("").RequireAuthorization().MapProcessor();

// Require authorization on all Queryable endpoints
app.MapGroup("").RequireAuthorization().MapQueryable();

// Different policies per surface
app.MapGroup("").RequireAuthorization("ProcessPolicy").MapProcessor();
app.MapGroup("").RequireAuthorization("QueryPolicy").MapQueryable();

// No auth (default)
app.MapProcessor();
app.MapQueryable();
```

`MapGroup("")` with an empty prefix adds no route prefix of its own — it only attaches the convention. This composes with any existing `IEndpointConventionBuilder` support in the ASP.NET Core pipeline.

---

## Registry endpoint

`MapRegistry()` resolves:
- the local processor's registry from `IProcessorRegistry`
- all downstream process client registries via `IKaleidoProcessClientFactory`
- all downstream queryable client registries via `IKaleidoQueryableClientFactory`

It always returns HTTP 200. Downstream clients that are unreachable populate the `ClientErrors` array in the response. A non-empty `ClientErrors` collection means the response is partial.

The route prefix is derived from `KaleidoServiceOptions.ServiceName` (bound from `Kaleido:ServiceName` configuration).

---

## Registration requirement

`AddHttp()` registers the middleware pipeline (`ExceptionMiddleware`, `ObservabilityMiddleware`) and the transport services (`ProcessExecutionService`, `ProcessStateService`) that `MapQueryable()`, `MapProcessor()`, and `MapRegistry()` depend on. Call it on the `IKaleidoBuilder` before mapping endpoints.

---

## Where to look

- `QueryableEndpointRouteBuilderExtensions.cs` — Queryable route publication
- `ProcessEndpointRouteBuilderExtensions.cs` — Process route publication
- `RegistryEndpointRouteBuilderExtensions.cs` — Registry route publication
- `Contracts/ProcessContractUrls.cs` / `ProcessRoutePaths.cs` — Process URL generation
- `Contracts/QueryableContractUrls.cs` / `QueryableRoutePaths.cs` — Queryable URL generation
