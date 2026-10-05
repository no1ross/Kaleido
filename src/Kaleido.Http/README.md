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

Queryable requests use an optional `query.page` object. An omitted page can still apply the registered default size, and `totalCount` is not always a global match count; see [Queryable paging and totalCount](../Kaleido.Http.Abstractions/README.md#queryable-paging-and-totalcount) before implementing a non-.NET consumer.

### Process endpoint mapping
- `ProcessorEndpointRouteBuilderExtensions` — `MapProcessor()` extension
  - `GET /{prefix}/processes/steps/{step}/metadata` — per-step metadata
  - `POST /{prefix}/processes/execute` — multi-step execute endpoint
  - `GET /{prefix}/processes/{processId}` — process state
  - `POST /{prefix}/processes/steps/{step}` — per-step execute endpoint

### Registry endpoint mapping
- `RegistryEndpointRouteBuilderExtensions` — `MapRegistry()` extension (internal; mapped by `MapKaleidoHttp`)
  - `GET /{prefix}/registry` — unified discovery: local process + queryable registrations; when `MapKaleidoHttp(o => o.AggregateRegistry = true)` is used, also merges every downstream client registered via `AddHttpClients()`

### URL and route helpers
- `ProcessContractUrls` / `ProcessRoutePaths` (in `Kaleido.Http.Abstractions`) — URL generation for Process metadata and execute URLs
- `QueryableContractUrls` (in `Kaleido.Http.Abstractions`) — URL generation for Queryable metadata and query URLs

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

`AddHttp()` wires the middleware pipeline automatically via `KaleidoStartupFilter`. It places `ExceptionMiddleware` **outermost**, then `ObservabilityMiddleware`, then the rest of the host pipeline. The exception boundary catches failures from correlation setup and downstream endpoints and maps them to Kaleido HTTP error responses. The inner observability middleware initializes correlation before endpoints run and schedules response-header echo. Reversing the pair would leave errors thrown during observability setup outside Kaleido's exception mapping.

This registration always happens; there is no opt-out, and both middleware types are internal.

## Per-step Process execution contract

Each registered step publishes an `ExecuteUrl` in its registry metadata. POST to that URL with an `application/json` body containing exactly the step-input envelope; the fields inside `processStep` depend on the registered step:

```http
POST /{service}/processes/steps/{step}
Content-Type: application/json

{"processStep":{"field":"value"}}
```

To continue an existing process, send its id in the `X-Kaleido-Process-Id` **request header**, not in the JSON body. Omit that header to create a new process. The route identifies the step, so neither `processId` nor `stepName` is a request-body field. The resulting process id is returned in both the `processId` response property and the `X-Kaleido-Process-Id` response header.

A step whose handler returns a typed result has this response shape (values are illustrative):

```json
{
  "processId": "00000000-0000-0000-0000-000000000001",
  "stepName": "Example",
  "requiredStep": null,
  "targetProcessorName": null,
  "outcome": "completed",
  "availableSteps": [],
  "businessMessages": [],
  "frameworkMessages": [],
  "result": { "value": "example" }
}
```

Steps without a typed handler result return the same fields without `result`. Handler-authored `businessMessages` remain available; `frameworkMessages` is an empty collection unless `AddHttp(o => o.IncludeFrameworkMessages = true)` enables diagnostics. Inspect `outcome` rather than interpreting HTTP 200 alone as step completion. Kaleido's HTTP JSON options use camelCase property names and string enum values; see the [JSON enum contract](../Kaleido.Http.Abstractions/README.md#json-enum-values) for canonical names and numeric-input rules.

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

Capability authorization is set in core with `AddKaleido(…, o => o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust)` (see [`docs/AUTHORIZATION.md`](../../docs/AUTHORIZATION.md)). With `None` (default) Kaleido attaches no auth metadata and enforces nothing, so hosts without authentication work unchanged. With `Authenticated`, every capability requires an authenticated caller and undeclared capabilities are open to any of them. With `ZeroTrust`, a capability needs an explicit rule (its own `[KaleidoAuthorization]` or the service `DefaultAuthorization`) or it is not mapped or discovered at all. `[KaleidoAuthorization]` narrows or opens access:

```csharp
[ProcessStep(Name = "approve", ...)]
[KaleidoAuthorization(Roles = "radiology")]       // or Policy = "named-policy", or AllowAnonymous = true
public sealed record ApproveStep;
```

Enforcement works on two layers:

**Route-level gate** — `MapProcessor()`/`MapQueryable()` attach metadata to each capability's execute/query endpoint at map time: `AllowAnonymous` → `AllowAnonymous()`; otherwise `RequireAuthorization()` plus a role requirement for declared `Roles` and the named ASP.NET policy for a declared `Policy` (ANDed). The transfer endpoint requires an authenticated caller. Evaluation is done by the host's `UseAuthorization()` middleware — the host must wire `AddAuthentication()`/`AddAuthorization()` and `app.UseAuthentication(); app.UseAuthorization();`. `MapKaleidoHttp()` throws `authentication_not_configured` at startup when enforcing without any authentication scheme.

**In-handler evaluation** — `IKaleidoAuthorizer` (registered scoped by `AddHttp()`) covers what per-route metadata cannot express:
- *Multi-step execute* — `POST /processes/execute` has no route-level auth; `ProcessExecutionService` checks every submitted step against its own declaration before anything runs, and the first denial rejects the whole request (401/403). Unknown step names and empty requests are checked as undeclared.
- *Process ownership* — resuming or reading an owned process requires the owner or a role-mate.
- *Filtered discovery* — the `/{service}/registry` endpoint stays open but scopes its payload to the caller: `FilterAsync` drops capabilities the caller can't access (including views inside contexts and steps inside processors) and omits processors whose steps are all filtered out. The registry cache holds the unfiltered union; filtering is per-request with this host's `AuthorizationMode`, so routers must use `ZeroTrust` too.

Authorization failures are answered by the host's authentication scheme, as standard ASP.NET Core: endpoint-metadata denials use ASP.NET's default authorization result handler, and `ExceptionMiddleware` turns thrown `KaleidoAuthorizationException`s into `ChallengeAsync()` (401) or `ForbidAsync()` (403). Kaleido does not replace the host's authorization result handler, so consumer endpoints are unaffected.

A capability declaring `Policy` on a host with no `IAuthorizationService` fails closed — filtered out of discovery and denied at execution (with a warning log), rather than erroring the whole response.

---

## Registry endpoint

`MapKaleidoHttp()` always maps `GET /{service}/registry` when a runtime is present. The endpoint returns an `AggregatedRegistryResponse` (`Processes`, `Queryables`, `ClientErrors`) built from:
- the local processor's registry (`IProcessorRegistry`)
- the local queryable registry (`IQueryableRegistry`)
- when `AggregateRegistry` is set: every downstream client registered via `AddHttpClients()` — one `GET /{downstream}/registry` fetch per named client, deduplicated through a shared cache (`KaleidoRemoteRegistry` in `Kaleido.Http.Client`), so the Process and Queryable halves never issue separate calls

A client whose `RoutePrefix` equals this service's `ServiceName` is skipped (self-fetch would recurse). Aggregation without any registered clients throws a `KaleidoConfigurationException` at map time.

**Partial responses:** when an aggregated registry rebuild encounters an unreachable downstream, the endpoint returns HTTP **200** by default with `isPartial: true` and non-empty `clientErrors`. Append `?strict` for HTTP **502** with the **same partial body**; agents/gateways can still read both the working catalog and the error list. A healthy registry returns 200. `?strict` changes the failure status, not snapshot freshness: cached clean results may still be served, and a matching `If-None-Match` can return 304 before the strict check. Use `?refresh` when a fresh downstream probe is needed, subject to the refresh cooldown.

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
- `ProcessorEndpointRouteBuilderExtensions.cs` — Process route publication
- `RegistryEndpointRouteBuilderExtensions.cs` — Registry route publication
- `../Kaleido.Http.Abstractions/Processor/ProcessRoutes.cs` — `ProcessContractUrls`, `ProcessRoutePaths`, `ProcessEndpointNames`
- `../Kaleido.Http.Abstractions/Queryable/QueryableRoutes.cs` — `QueryableContractUrls`
