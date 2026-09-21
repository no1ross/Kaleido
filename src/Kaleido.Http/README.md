# Kaleido.Http

This project publishes all Kaleido HTTP endpoints. It maps Process, Queryable, and Registry routes onto an ASP.NET Core application.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Kaleido/README.md`](../Kaleido/README.md)
- [`../Kaleido.AspNetCore/README.md`](../Kaleido.AspNetCore/README.md)
- [`../Kaleido.Http.Abstractions/README.md`](../Kaleido.Http.Abstractions/README.md)

---

## What lives here

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
- ASP.NET Core DI registration — that is [`Kaleido.AspNetCore`](../Kaleido.AspNetCore/README.md)
- HTTP contract types — that is [`Kaleido.Http.Abstractions`](../Kaleido.Http.Abstractions/README.md)
- remote HTTP client consumption — that is [`Kaleido.Http.Client`](../Kaleido.Http.Client/README.md)

---

## Usage

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddQueryable()
        .AddQueryableAspNetCore()
    .AddProcessor(options => { ... })
        .AddProcessorAspNetCore();

var app = builder.Build();
app.UseKaleidoExceptionHandling();
app.MapQueryable();
app.MapProcessor();
app.MapRegistry(); // optional aggregated discovery
```

---

## Registry endpoint

`MapRegistry()` resolves:
- the local processor's registry from `IProcessorRegistry`
- all downstream process client registries via `IKaleidoProcessClientFactory`
- all downstream queryable client registries via `IKaleidoQueryableClientFactory`

It always returns HTTP 200. Downstream clients that are unreachable populate the `ClientErrors` array in the response. A non-empty `ClientErrors` collection means the response is partial.

The route prefix is derived from `KaleidoServiceOptions.ServiceName` (bound from `Kaleido:ServiceName` configuration).

---

## Relationship to Kaleido.AspNetCore

`Kaleido.Http` maps endpoints. `Kaleido.AspNetCore` adds the DI registrations and transport services those endpoints depend on.

`AddQueryableAspNetCore()` and `AddProcessorAspNetCore()` (in `Kaleido.AspNetCore`) must be called before `MapQueryable()` and `MapProcessor()`.

---

## Where to look

- `QueryableEndpointRouteBuilderExtensions.cs` — Queryable route publication
- `ProcessEndpointRouteBuilderExtensions.cs` — Process route publication
- `RegistryEndpointRouteBuilderExtensions.cs` — Registry route publication
- `Contracts/ProcessContractUrls.cs` / `ProcessRoutePaths.cs` — Process URL generation
- `Contracts/QueryableContractUrls.cs` / `QueryableRoutePaths.cs` — Queryable URL generation
