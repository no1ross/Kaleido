# Kaleido.Http

This project publishes all Kaleido HTTP endpoints. It maps Process, Queryable, and Registry routes onto an ASP.NET Core application.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Kaleido/README.md`](../Kaleido/README.md)
- [`../Kaleido.Http.Abstractions/README.md`](../Kaleido.Http.Abstractions/README.md)

---

## What lives here

### Registration and endpoint filters
- `KaleidoHttpServiceCollectionExtensions` — `AddHttp()` entry point; registers routing, `IHttpContextAccessor` and the HTTP execution services. It adds nothing to the host's middleware pipeline.
- Endpoint filters on every Kaleido route group, in order (all internal):
  - `KaleidoExceptionEndpointFilter` maps exceptions to JSON error responses (see [Error responses](#error-responses));
  - `KaleidoCorrelationEndpointFilter` reads the inbound correlation headers, populates `IKaleidoCorrelationContextAccessor`, tags the trace and echoes the correlation headers;
  - `KaleidoJsonEndpointFilter` writes results with `KaleidoJsonOptions`;
  - `KaleidoCallerContextEndpointFilter` sets the caller from the authenticated user.
- `HttpCorrelationContextReader` — reads and sanitizes inbound HTTP headers

### Queryable endpoint mapping
- `QueryableEndpointRouteBuilderExtensions` — `MapQueryable()` (internal; mapped by `MapKaleidoHttp`)
  - `POST /{prefix}/queryable/{source}/query` — direct query of a query source (every source, local or delegated)
  - `POST /{prefix}/queryable/{source}/{view}/query` — query view of a local source

  Segments are the source and view type names, lowercased. All sources are mapped the same way; how a source is fulfilled is not visible to callers.

Queryable requests use an optional `query.page` object. An omitted page on a pageable source or view applies its default page; `totalCount` is always the number of matching rows. See [Queryable paging and totalCount](../Kaleido.Http.Abstractions/README.md#queryable-paging-and-totalcount) before implementing a non-.NET consumer.

### Process endpoint mapping
- `ProcessorEndpointRouteBuilderExtensions` — `MapProcessor()` (internal; mapped by `MapKaleidoHttp`)
  - `POST /{prefix}/processes/execute` — multi-step execute endpoint. Each submitted step is read into its step type here, with the same JSON rules as the per-step routes; a step that cannot be built (unknown name: `pro_unknown_step`; a value that does not fit or a `required` member left out: `argument_error` naming the step and the JSON path) rejects the whole request with 400 before anything runs, so no process is created
  - `GET /{prefix}/processes/{processId}` — process state
  - `POST /{prefix}/processes/steps/{step}` — per-step execute endpoint

  Step metadata (inputs, results, relationships, authorization, execute URL) is published by the registry endpoint below; there is no per-step metadata endpoint.

### Registry endpoint mapping
- `RegistryEndpointRouteBuilderExtensions` — `MapRegistry()` extension (internal; mapped by `MapKaleidoHttp`)
  - `GET /{prefix}/registry` — unified discovery: local process + queryable registrations; when `MapKaleidoHttp(o => o.AggregateRegistry = true)` is used, also merges every downstream client registered via `AddHttpClients()`

### URL and route helpers (internal)
- `ProcessContractUrls` / `ProcessRoutePaths` and `QueryableContractUrls` (in `Kaleido.Http.Abstractions`) build the execute, state and query URLs for both the server and the clients. They are internal, as are the endpoint-name constants (`ProcessEndpointNames`, `QueryableEndpointNames`, `RegistryEndpointNames`): consumers take URLs from the registry (`executeUrl`, `queryUrl`).

---

## What this project is for

Reference this project when you need to:
- publish all Kaleido endpoints with `MapKaleidoHttp()` (it detects the active runtimes, maps their endpoints and the registry; per-surface mapping is internal)

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
    o.ServiceName = "myservice"; // lowercase letters, digits, '-' and '_' (KAL2005)
    o.Assemblies = [typeof(Program).Assembly];
    // AuthorizationMode is Authenticated unless set: add host authentication, or set None explicitly (ADR 0021)
})
    .AddHttp();

var app = builder.Build();
app.MapKaleidoHttp(); // maps Process + Queryable + /{service}/registry

// On a router/gateway that fans out to downstream services:
app.MapKaleidoHttp(o => o.AggregateRegistry = true); // requires AddHttpClients()
```

Kaleido's HTTP behavior lives on Kaleido's own endpoints: `AddHttp()` adds nothing to the host's middleware pipeline. Every Kaleido route group runs the exception filter first, then the correlation filter, so a failure during correlation setup (e.g. a malformed `X-Kaleido-Process-Id`) is answered with Kaleido's 400. The correlation filter runs inside the endpoint, after the host's authentication, so identity headers are trusted against the authenticated caller. The host's own endpoints are untouched: no `X-Kaleido-*` headers, no Kaleido error bodies. The MCP endpoint (`MapKaleidoMcp`), mapped by the MCP SDK, sets up the same correlation itself (#295).

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

`requiredStep` is an object with the same fields as an `availableSteps` entry (`name`, `displayName`, `description`, `version`, `repeatable`, `isInformationStep`, `destructive`, `external`, `requiresConfirmation`, `authorization`, `executeUrl`), plus `informationRequest` when the process is `AwaitingInformation`. The questions arrive there; submit the answers as that step's payload. See [`docs/INFORMATION_REQUESTS.md`](../../docs/INFORMATION_REQUESTS.md). The state endpoint (`GET /{service}/processes/{processId}`) returns the same `requiredStep` with the pending request. A step that isn't a next step of the process is rejected (`pro_step_not_available`).

Steps without a typed handler result return the same fields without `result`. Handler-authored `businessMessages` remain available; `frameworkMessages` is an empty collection unless `AddHttp(o => o.IncludeFrameworkMessages = true)` enables diagnostics. Inspect `outcome` rather than interpreting HTTP 200 alone as step completion. Kaleido's HTTP JSON options use camelCase property names and string enum values; see the [JSON enum contract](../Kaleido.Http.Abstractions/README.md#json-enum-values) for canonical names and numeric-input rules.

---

## Idempotent execution (`Idempotency-Key`)

A process execute request - `POST /{service}/processes/execute` or a step call - may carry an `Idempotency-Key` header: a caller-chosen key for one logical call (e.g. a GUID), the same on every retry of that call.

The key is **admitted in the store before the step runs**, atomically (ADR 0019). What a request with a key gets:

| Situation | Answer |
|---|---|
| First request with the key | the step runs; a successful (2xx) answer is stored with the key |
| Same key and request, **while the first one still runs** | **409** `pro_idempotency_in_progress` with `Retry-After`; the step does **not** run. Retry after the delay |
| Same key and request, **after the first one answered** | the stored answer again, with `Idempotent-Replayed: true`; the step does not run again |
| Same key, the first one **never finished** (the service stopped while it ran; its lease, `KaleidoHttpOptions.IdempotencyLease`, default 5 minutes, passed) | **409** `pro_idempotency_outcome_unknown`: it may or may not have run, so it is not run again with this key. Read the process state (`GET /{service}/processes/{processId}`) and decide |
| Same key, a **different request** (other body, route or `X-Kaleido-Process-Id`) | **422** `pro_idempotency_key_reused`. A key identifies one call; use a new key for a new call |
| A key longer than 200 characters | **400** `pro_invalid_idempotency_key` |
| No header | nothing changes: every request runs |

What this guarantees, and what it does not:
- With a key, **a step runs at most once per key** while the key is kept, even when retries arrive while the first attempt still runs, or after its caller gave up (a client timeout): the answer is stored even when nobody is waiting for it.
- It does **not** make a handler's external work exactly-once. If the service stops inside a handler after it called another system and before the step was saved, that call happened and Kaleido never learned of it; the key is then answered `pro_idempotency_outcome_unknown`. Make such calls idempotent on the other system's side (e.g. pass it a stable id such as the process id and the step name).

Details:
- Keys are **scoped per service and caller**: two callers cannot see each other's answers, and the same key at two services is two keys. The caller is its stable subject id (`sub`, else `NameIdentifier`) when the principal has one - a renamed user keeps their keys and two users with the same name stay apart - else its name; anonymous callers share one scope.
- Answers are kept for `KaleidoHttpOptions.IdempotencyRetention` (default **24 hours**: `AddHttp(o => o.IdempotencyRetention = TimeSpan.FromHours(4))`) by the idempotency store (`IKaleidoIdempotencyStore.AdmitAsync`/`CompleteAsync`/`ReleaseAsync`) - the SQLite reference store keeps them in its own `IdempotentResults` table in the same database, so there is nothing extra to set up.
- Only successful answers are stored: a request that failed releases its key, so it may be retried with the same key.
- **Any HTTP client** gets this, not only Kaleido's: send `Idempotency-Key: <a new unique value>` with each logical call, re-send the **same key and the same request** on a retry, wait `Retry-After` seconds on `pro_idempotency_in_progress`, and read the process state on `pro_idempotency_outcome_unknown`. The Kaleido clients do all of it automatically ([`Kaleido.Http.Client`](../Kaleido.Http.Client/README.md#idempotent-execute-calls)).

## Concurrent calls (`409 pro_process_conflict`)

Every save of the process state is conditional on the version it was loaded with (optimistic concurrency, `ProcessorContext.Version`). When two requests for the same process run at the same time - a client retry after a timeout, a double submit, two agents on one case - the first save wins and the second is refused: **409** `pro_process_conflict`, and **nothing of the second request is saved**. The caller reads the process state again (`GET /{service}/processes/{processId}`) and decides on the next step; repeating the same call blindly would act on a state it has not seen.

## Error responses

Kaleido answers every error from its own endpoints itself, with `{ "errors": [{ "code", "message" }] }`: `KaleidoExceptionEndpointFilter` runs first on every Kaleido route group and maps the exception inside the endpoint (400 validation, 401/403 authorization, 409 conflict, 500 configuration/framework/unexpected). A host's own exception handling - the developer exception page `WebApplication` adds in Development, or `UseExceptionHandler` - therefore never turns a Kaleido validation error into a 500 or an HTML page. Correlation setup runs inside the endpoint too (after the exception filter), and request bodies are read by Kaleido itself, so no Kaleido failure happens outside the filters; errors from the host's own endpoints are the host's to answer. When the client has gone, the error is still logged, but no response is written.

## Authorization

`MapKaleidoHttp()` returns an `IEndpointConventionBuilder` covering every mapped endpoint, so conventions compose directly. Per-surface mapping is internal, so a host convention applies to all Kaleido endpoints; per-capability rules come from `[KaleidoAuthorization]` (below):

```csharp
// Require authorization on everything Kaleido publishes
app.MapKaleidoHttp().RequireAuthorization();

// No host-level convention: Kaleido's own mode decides (Authenticated unless the host chose otherwise)
app.MapKaleidoHttp();
```

Any `IEndpointConventionBuilder` extension (`RequireAuthorization`, `WithMetadata`, `RequireCors`, rate limiting, etc.) composes this way. To apply conventions to a broader surface, wrap in `MapGroup("")` as usual — an empty prefix adds no route prefix of its own.

### Per-capability authorization

Capability authorization is set in core with `AddKaleido(…, o => o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust)` (see [`docs/AUTHORIZATION.md`](../../docs/AUTHORIZATION.md)). With `Authenticated` (the default, ADR 0021), every capability requires an authenticated caller and undeclared capabilities are open to any of them; a host without an authentication scheme does not start. With `None`, which a host chooses explicitly, Kaleido attaches no auth metadata and enforces nothing, so hosts without authentication work. With `ZeroTrust`, a capability needs an explicit rule (its own `[KaleidoAuthorization]` or the service `DefaultAuthorization`) or it is not mapped or discovered at all. `[KaleidoAuthorization]` narrows or opens access:

```csharp
[ProcessStep(Version = "1.0.0", DisplayName = "Approve", Description = "...")]
[KaleidoAuthorization(Roles = "radiology")]       // or Policy = "named-policy", or AllowAnonymous = true
public sealed record ApproveStep : IProcessStep;
```

Enforcement works on two layers:

**Route-level gate** — `MapKaleidoHttp()` attaches metadata to each capability's execute/query endpoint at map time: `AllowAnonymous` → `AllowAnonymous()`; otherwise `RequireAuthorization()` plus a role requirement for declared `Roles` and the named ASP.NET policy for a declared `Policy` (ANDed). Evaluation is done by the host's `UseAuthorization()` middleware — the host must wire `AddAuthentication()`/`AddAuthorization()` and `app.UseAuthentication(); app.UseAuthorization();`. `MapKaleidoHttp()` throws `authentication_not_configured` at startup when enforcing without any authentication scheme.

**In-handler evaluation** — `IKaleidoAuthorizer` (registered scoped by `AddHttp()`) covers what per-route metadata cannot express:
- *Multi-step execute* — `ProcessExecutionService` checks every submitted step against its own declaration before anything runs, and the first denial rejects the whole request (401/403). Unknown step names and empty requests are checked as undeclared. At route level, `POST /processes/execute` and `GET /processes/{processId}` require an authenticated caller when enforcing, unless the processor has an `AllowAnonymous` step (then they stay open and the per-step checks decide).
- *No case-level guardrails* (ADR 0010) - resuming or reading a process needs only what the step or route declares; `CreatedBy`/`UpdatedBy` (and the step `StartedBy`/`LastUpdatedBy`) are tags, never enforced. A process id therefore works as a bearer handle; what it grants, where it is exposed and where a host enforces case access is in the [threat model](../../docs/AUTHORIZATION.md#threat-model-the-process-id-is-a-bearer-handle).
- *Filtered discovery* — the `/{service}/registry` endpoint stays open but scopes its payload to the caller: `FilterAsync` drops capabilities the caller can't access (including views inside sources and steps inside processors) and omits processors whose steps are all filtered out. The registry cache holds the unfiltered union; filtering is per-request with this host's `AuthorizationMode`, so routers must use `ZeroTrust` too.

Authorization failures are answered by the host's authentication scheme, as standard ASP.NET Core: endpoint-metadata denials use ASP.NET's default authorization result handler, and Kaleido's exception handling (`KaleidoExceptionEndpointFilter`, inside the endpoints) turns thrown `KaleidoAuthorizationException`s into `ChallengeAsync()` (401) or `ForbidAsync()` (403). Kaleido does not replace the host's authorization result handler, so consumer endpoints are unaffected.

A capability declaring `Policy` on a host with no `IAuthorizationService` fails closed — filtered out of discovery and denied at execution (with a warning log), rather than erroring the whole response.

---

## Registry endpoint

`MapKaleidoHttp()` always maps `GET /{service}/registry` when a runtime is present. The endpoint returns an `AggregatedRegistryResponse` (`Processes`, `Queryables`, `ClientErrors`) built from:
- the local processor's registry (`IProcessorRegistry`)
- the local queryable registry (`IQueryableRegistry`)
- when `AggregateRegistry` is set: every downstream client registered via `AddHttpClients()` — one `GET /{downstream}/registry` fetch per named client, deduplicated through a shared cache (`KaleidoRemoteRegistry` in `Kaleido.Http.Client`), so the Process and Queryable halves never issue separate calls

A client whose `RoutePrefix` equals this service's `ServiceName` is skipped (self-fetch would recurse). Aggregation without any registered clients throws a `KaleidoConfigurationException` at map time.

**Behavior and sensitivity:** each step carries `destructive`, `external` and `requiresConfirmation` as declared on `[ProcessStep]` (also on step summaries and the required step), and every field - step inputs and outputs, query fields, view outputs and parameters - carries `classifications` (e.g. `["PriorAuth:PHI"]`) from its `DataClassificationAttribute`s ([ADR 0015](../../docs/decisions/0015-declared-step-behavior.md)).

**Partial responses:** when an aggregated registry rebuild encounters an unreachable downstream, the endpoint returns HTTP **200** by default with `isPartial: true` and non-empty `clientErrors`. Append `?strict` for HTTP **502** with the **same partial body**; agents/gateways can still read both the working catalog and the error list. A healthy registry returns 200. `?strict` changes the failure status, not snapshot freshness: cached clean results may still be served, and a matching `If-None-Match` can return 304 before the strict check. Use `?refresh` when a fresh downstream probe is needed, subject to the refresh cooldown.

**Freshness contract:** every response carries `GeneratedAt` (snapshot build time - reflects data age even when served from cache), `Revision` (SHA-256 of the filtered payload, without the build time: a rebuilt but unchanged registry keeps its revision), an `ETag` header, and `Cache-Control: private` with the snapshot's `max-age` (private: the registry is filtered per caller, so a shared cache must not serve it to another caller). Clients may send `If-None-Match` to get a **304** when nothing changed. `Revision` is computed on the per-caller filtered payload, so ETags are correct per persona.

**Caching & TTL:** the built snapshot is stored under the canonical key `kaleido:{ServiceName}` in `IRegistrySnapshotStore` (in-memory by default; register a distributed implementation — e.g. Redis over `IDistributedCache` — on multi-replica aggregators so replicas share one snapshot and one downstream fan-out). Only fully-clean snapshots are committed: a partial result is served but never cached, so every request while degraded naturally re-probes the failed services. Options on `MapKaleidoHttp`:
- `RegistryCacheTtl` - how long the snapshot is served before the next request rebuilds it (default **30 seconds**; null = never on its own). On an aggregating host the rebuild checks every downstream with its ETag, so an unchanged downstream costs one small request with an empty 304; a deployed one is picked up. This is how a router - and the MCP tool list built from it - learns about deployments without a restart. It also bounds how long a clean snapshot can mask a newly-failing downstream.
- `RegistryRefreshCooldown` — minimum interval between honored `?refresh` requests (default 30s). Refreshes inside the window serve the cache instead of re-fanning-out; an honored refresh first invalidates each downstream client snapshot so the rebuild re-fetches.
- `RegistryDownstreamMaxParallelism` — maximum number of downstream registry fetches in flight at once during an aggregate build (default: unbounded). Set it on a router that aggregates many services.

In-host consumers (such as the MCP adapter) read the same caller-filtered payload without HTTP through `IKaleidoRegistryService`: `GetForCallerAsync()` returns it with its revision, and `IsMapped` reports whether `MapKaleidoHttp()` has run.

The route prefix is derived from `KaleidoServiceOptions.ServiceName` (bound from `Kaleido:ServiceName` configuration).

**Route-prefix contract:** every endpoint a Kaleido service publishes lives under `/{ServiceName}/...`. Downstream consumers address a service through a named client whose `RoutePrefix` must equal that service's `ServiceName`. `RoutePrefix` defaults to the client key lowercased — so a client named `"Member"` expects the Member service to have `Kaleido:ServiceName = "member"`. A mismatch produces silent 404s at call time, not startup errors. `ServiceName` must match `[a-z0-9][a-z0-9_-]*` (lowercase ASCII letters, digits, `-` and `_`, starting with a letter or digit); `AddKaleido` rejects any other value at startup (`invalid_service_name`) and KAL2005 reports constant values at build time.

---

## Registration requirement

`AddHttp()` registers the transport services (`ProcessExecutionService`, `ProcessStateService`, the exception handler) that `MapKaleidoHttp()` and its endpoint filters depend on. Call it on the `IKaleidoBuilder` before mapping endpoints.

---

## Where to look

- `QueryableEndpointRouteBuilderExtensions.cs` — Queryable route publication
- `ProcessorEndpointRouteBuilderExtensions.cs` — Process route publication
- `RegistryEndpointRouteBuilderExtensions.cs` — Registry route publication
- `../Kaleido.Http.Abstractions/Processor/ProcessRoutes.cs` — `ProcessContractUrls`, `ProcessRoutePaths`, `ProcessEndpointNames`
- `../Kaleido.Http.Abstractions/Queryable/QueryableRoutes.cs` — `QueryableContractUrls`
