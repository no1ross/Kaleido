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
  - `GET /{prefix}/queryable/{context}/{metadataRoute}` — per-context metadata
  - `POST /{prefix}/queryable/{context}/{queryRoute}` — direct context query (Direct contexts only)
  - `POST /{prefix}/queryable/{context}/{view}/{queryRoute}` — local or delegated view query

### Process endpoint mapping
- `ProcessEndpointRouteBuilderExtensions` — `MapProcessor()` extension
  - `GET /{prefix}/processes/steps/{step}/metadata` — per-step metadata
  - `POST /{prefix}/processes/execute` — multi-step execute endpoint
  - `GET /{prefix}/processes/{processId}` — process state
  - `POST /{prefix}/processes/steps/{step}` — per-step execute endpoint

### Registry endpoint mapping
- `RegistryEndpointRouteBuilderExtensions` — `MapRegistry()` extension (internal; mapped by `MapKaleidoHttp`)
  - `GET /{prefix}/registry` — unified discovery: local process + queryable registrations; when `MapKaleidoHttp(o => o.AggregateRegistry = true)` is used, also merges every downstream client registered via `AddHttpClients()`

### URL and route helpers
- `ProcessContractUrls` / `ProcessRoutePaths` — URL generation for Process metadata and execute URLs
- `QueryableContractUrls` / `QueryableRoutePaths` — URL generation for Queryable metadata and query URLs

---

## What this project is for

Reference this project when you need to:
- publish all Kaleido endpoints at once with `MapKaleidoHttp()` (auto-detects active runtimes and maps the registry)
- publish only Queryable endpoints with `MapQueryable()`
- publish only Process endpoints with `MapProcessor()`
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
app.MapKaleidoHttp(); // maps Process + Queryable + /{service}/registry

// On a router/gateway that fans out to downstream services:
app.MapKaleidoHttp(o => o.AggregateRegistry = true); // requires AddHttpClients()
```

`AddHttp()` wires the middleware pipeline automatically via `KaleidoStartupFilter` — no manual `Use...()` call is needed.

---

## Authorization

`MapKaleidoHttp()` returns an `IEndpointConventionBuilder` covering every mapped endpoint, so conventions compose directly. For per-surface conventions, `MapProcessor()` and `MapQueryable()` still return the `RouteGroupBuilder` they mapped:

```csharp
// Require authorization on everything Kaleido publishes
app.MapKaleidoHttp().RequireAuthorization();

// Different policies per surface
app.MapProcessor().RequireAuthorization("ProcessPolicy");
app.MapQueryable().RequireAuthorization("QueryPolicy");

// No auth (default)
app.MapKaleidoHttp();
```

Any `IEndpointConventionBuilder` extension (`RequireAuthorization`, `WithMetadata`, `RequireCors`, rate limiting, etc.) composes this way. To apply conventions to a broader surface, wrap in `MapGroup("")` as usual — an empty prefix adds no route prefix of its own.

### Per-capability authorization

Individual capabilities declare requirements with `[KaleidoAuthorization]` in `Kaleido` core:

```csharp
[ProcessStep(Name = "approve", ...)]
[KaleidoAuthorization(Roles = "internal")]        // or Policy = "named-policy"
public sealed record ApproveStep;
```

Enforcement works on two layers:

**Route-level gate** — `MapProcessor()`/`MapQueryable()` attach `RequireAuthorization` metadata to each capability's execute, query, and metadata endpoint at map time. Declared `Roles` become a role requirement (`RequireRole`), declared `Policy` becomes the named ASP.NET policy; both declared are ANDed. Evaluation is done by the host's `UseAuthorization()` middleware — Kaleido performs no authentication itself, so the host must wire `AddAuthentication()`/`AddAuthorization()` and `app.UseAuthentication(); app.UseAuthorization();` in its pipeline. Without them, `RequireAuthorization` metadata is inert.

**In-handler evaluation** — `IKaleidoAuthorizer` (registered scoped by `AddHttp()`) covers what per-route metadata cannot express:
- *Multi-step execute* — `POST /processes/execute` submits N steps; `ProcessExecutionService` calls `AuthorizeAsync` per submitted step (unknown step names are left to runtime validation). A denied step fails the request with `KaleidoAuthorizationException` → 401/403.
- *Filtered discovery* — registry/metadata endpoints stay open but scope their payloads to the caller: `FilterAsync` drops capabilities whose declared roles/policy the caller doesn't satisfy, including views inside context metadata and steps inside the `/{service}/registry` response (the registry cache holds the unfiltered union; filtering is per-request).

`AddHttp(Action<KaleidoHttpOptions>)` registers `KaleidoHttpOptions` unconditionally; `RequireAuthorization = true` makes *undeclared* capabilities require an authenticated caller (default `false` — undeclared stays open).

Authorization failures are `KaleidoErrorResponse` bodies with codes `unauthorized` (401, unauthenticated) and `forbidden` (403, denied) — via `KaleidoAuthorizationResultHandler` for middleware-level denials and `ExceptionMiddleware` for thrown `KaleidoAuthorizationException`s. Both record the `endpoint_errors` counter.

A capability declaring `Policy` on a host with no `IAuthorizationService` fails closed — filtered out of discovery and denied at execution (with a warning log), rather than erroring the whole response.

---

## Registry endpoint

`MapKaleidoHttp()` always maps `GET /{service}/registry` when a runtime is present. The endpoint returns an `AggregatedRegistryResponse` (`Processes`, `Queryables`, `ClientErrors`) built from:
- the local processor's registry (`IProcessRegistry`)
- the local queryable registry (`IQueryableRegistry`)
- when `AggregateRegistry` is set: every downstream client registered via `AddHttpClients()` — one `GET /{downstream}/registry` fetch per named client, deduplicated through a shared cache (`KaleidoRemoteRegistry` in `Kaleido.Http.Client`), so the Process and Queryable halves never issue separate calls

A client whose `RoutePrefix` equals this service's `ServiceName` is skipped (self-fetch would recurse). Aggregation without any registered clients throws a `KaleidoConfigurationException` at map time.

**Partial responses:** by default the endpoint always returns HTTP 200; unreachable downstream clients populate `ClientErrors` (and set `IsPartial` on the response). Append `?strict` to get **502** when the aggregate is partial — the body is still included, so callers get both the catalog of what worked and the error list. `?strict` is for agents/gateways that need a real failure signal.

**Freshness contract:** every response carries `GeneratedAt` (snapshot build time — reflects data age even when served from cache), `Revision` (SHA-256 of the filtered payload), an `ETag` header, and `Cache-Control`. Clients may send `If-None-Match` to get a **304** when nothing changed. `Revision` is computed on the per-caller filtered payload, so ETags are correct per persona.

**Caching & TTL:** the built snapshot is stored under the canonical key `kaleido:{ServiceName}` in `IRegistrySnapshotStore` (in-memory by default; register a distributed implementation — e.g. Redis over `IDistributedCache` — on multi-replica aggregators so replicas share one snapshot and one downstream fan-out). Only fully-clean snapshots are committed: a partial result is served but never cached, so every request while degraded naturally re-probes the failed services. Options on `MapKaleidoHttp`:
- `RegistryCacheTtl` — opt-in max age for the snapshot (default: never expires). Bounds how long a clean snapshot can mask a newly-failing downstream.
- `RegistryRefreshCooldown` — minimum interval between honored `?refresh` requests (default 30s). Refreshes inside the window serve the cache instead of re-fanning-out; an honored refresh first invalidates each downstream client snapshot so the rebuild re-fetches.

The route prefix is derived from `KaleidoServiceOptions.ServiceName` (bound from `Kaleido:ServiceName` configuration).

**Route-prefix contract:** every endpoint a Kaleido service publishes lives under `/{ServiceName}/...`. Downstream consumers address a service through a named client whose `RoutePrefix` must equal that service's `ServiceName`. `RoutePrefix` defaults to the client key lowercased — so a client named `"Member"` expects the Member service to have `Kaleido:ServiceName = "member"`. A mismatch produces silent 404s at call time, not startup errors. `ServiceName` must be lowercase with no separators (enforced by KAL2005).

---

## Registration requirement

`AddHttp()` registers the middleware pipeline (`ExceptionMiddleware`, `ObservabilityMiddleware`) and the transport services (`ProcessExecutionService`, `ProcessStateService`) that `MapKaleidoHttp()`, `MapQueryable()`, and `MapProcessor()` depend on. Call it on the `IKaleidoBuilder` before mapping endpoints.

---

## Where to look

- `QueryableEndpointRouteBuilderExtensions.cs` — Queryable route publication
- `ProcessEndpointRouteBuilderExtensions.cs` — Process route publication
- `RegistryEndpointRouteBuilderExtensions.cs` — Registry route publication
- `Contracts/ProcessContractUrls.cs` / `ProcessRoutePaths.cs` — Process URL generation
- `Contracts/QueryableContractUrls.cs` / `QueryableRoutePaths.cs` — Queryable URL generation
