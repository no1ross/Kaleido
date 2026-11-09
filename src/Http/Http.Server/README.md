# Kaleido.Http.Server

This project publishes a service's versioned Process and Queryable HTTP endpoints. Shared ASP.NET Core caller, authorization, correlation and filters live in `Kaleido.Server`; the separate `Kaleido.Registry.Http` project maps the control registry.

> How this project fits into a whole service, with tested examples: the [consumer guide](../../../docs/guide/README.md). This README is the reference for the project.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../../AGENTS.md`](../../AGENTS.md)
- [`../../Kaleido/README.md`](../../Kaleido/README.md)
- [`../../Platform/README.md`](../../Platform/README.md)

---

## What lives here

### Registration and shared endpoint filters
- `KaleidoHttpServiceCollectionExtensions` — `AddHttp()` entry point; registers routing, `IHttpContextAccessor` and HTTP execution services. It adds nothing to the host's middleware pipeline.
- Shared filters from `Kaleido.Server` on every route group, in order:
  - `KaleidoExceptionEndpointFilter` maps exceptions to JSON error responses (see [Error responses](#error-responses));
  - `KaleidoCorrelationEndpointFilter` reads the inbound correlation headers, populates `IKaleidoCorrelationContextAccessor`, tags the trace and echoes the correlation headers;
  - `KaleidoJsonEndpointFilter` writes results with `KaleidoJsonOptions`;
  - `KaleidoCallerContextEndpointFilter` sets the caller from the authenticated user.
- `HttpCorrelationContextReader` (in `Kaleido.Server`) — reads and sanitizes inbound HTTP headers

### Queryable endpoint mapping
- `QueryableEndpointRouteBuilderExtensions` — `MapQueryable()` (internal; mapped by `MapKaleidoHttp`)
  - `POST /{service}/{version}/queryable/{source}/query` — direct query of a query source (every source, local or delegated)
  - `POST /{service}/{version}/queryable/{source}/{view}/query` — query view of a local source

  Segments are the source and view type names, lowercased. All sources are mapped the same way; how a source is fulfilled is not visible to callers.

Queryable requests use an optional `query.page` object. An omitted page on a pageable source or view applies its default page; `totalCount` is always the number of matching rows. See [Queryable paging and totalCount](../../Platform/README.md#queryable-paging-and-totalcount) before implementing a non-.NET consumer.

### Process endpoint mapping
- `ProcessorEndpointRouteBuilderExtensions` — `MapProcessor()` (internal; mapped by `MapKaleidoHttp`)
  - `POST /{service}/{version}/processes/execute` — multi-step execute endpoint. Each submitted step is read into its step type here, with the same JSON rules as the per-step routes; a step that cannot be built (unknown name: `pro_unknown_step`; a value that does not fit or a `required` member left out: `argument_error` naming the step and the JSON path) rejects the whole request with 400 before anything runs, so no process is created
  - `GET /{service}/{version}/processes/{processId}` — process state
  - `POST /{service}/{version}/processes/steps/{step}` — per-step execute endpoint

  Step metadata (inputs, results, relationships, authorization and versioned execute URL) is registered with the central registry; there is no per-step metadata endpoint.

### Central registry participation
- `MapKaleidoHttp()` publishes capability endpoints but no registry endpoint. It requires an `IKaleidoRegistrySource`: an ordinary service provides it through `AddControlRegistryClient()` so its capabilities register centrally; the control-registry host can supply its own store-backed source via `AddControlRegistry()`. `Kaleido.Registry.Http` serves `GET /registry` through `MapKaleidoControlRegistry()`.

### URL and route helpers (internal)
- `KaleidoHttpRoutePrefix`, `ProcessContractUrls` / `ProcessRoutePaths` and `QueryableContractUrls` (in the shared `Kaleido.Http` project) produce this service's versioned routes and registry URLs. They are internal, as are endpoint-name constants. Clients consume the published `executeUrl`, `queryUrl` and `processStateUrlTemplate`, not a constructed route prefix.

---

## What this project is for

Reference this project when you need to:
- publish all Kaleido endpoints with `MapKaleidoHttp()` (it maps the service's capability endpoints; per-surface mapping is internal and the registry runs centrally)

## What this project is NOT for

This project does not contain:
- runtime business logic — that is [`Kaleido`](../../Kaleido/README.md)
- shared published contracts — that is [`Kaleido.Platform`](../../Platform/README.md)
- remote HTTP client consumption — that is [`Kaleido.Http.Client`](../Http.Client/README.md)

---

## Usage

Reference `Kaleido.Http.Client` for `AddControlRegistryClient()`. Configure `Kaleido:BaseUrl` with
the gateway that serves `/registry` and maps this service's `/myservice/1` route to this host. The
control registry runs separately.

```csharp
builder.Services.AddKaleido(builder.Configuration, new()
{
    ServiceName = "myservice", // lowercase letters, digits, '-' and '_' (KAL2005)
    ServiceVersion = "1",
    Assemblies = [typeof(Program).Assembly],
})
    .AddInMemoryStores() // development; deployed services choose durable stores (ADR 0028)
    .AddHttp()
    .AddControlRegistryClient();

var app = builder.Build();
app.MapKaleidoHttp(); // Process + Queryable, only under /myservice/1
```

Kaleido's HTTP behavior lives on Kaleido's own endpoints: `AddHttp()` adds nothing to the host's middleware pipeline. Every Kaleido route group runs the exception filter first, then the correlation filter, so a failure during correlation setup (e.g. a malformed `X-Kaleido-Process-Id`) is answered with Kaleido's 400. The correlation filter runs inside the endpoint, after the host's authentication, so identity headers are trusted against the authenticated caller. The host's own endpoints are untouched: no `X-Kaleido-*` headers, no Kaleido error bodies. The MCP endpoint (`MapKaleidoMcp`), mapped by the MCP SDK, sets up the same correlation itself (#295).

## Per-step Process execution contract

Each registered step publishes an `ExecuteUrl` with its service version in the central registry. There is no unversioned alias. POST to that URL with an `application/json` body containing exactly the step-input envelope; the fields inside `processStep` depend on the registered step:

```http
POST /{service}/{version}/processes/steps/{step}
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
  "status": "completed",
  "availableSteps": [],
  "businessMessages": [],
  "frameworkMessages": [],
  "result": { "value": "example" }
}
```

`requiredStep` is an object with the same fields as an `availableSteps` entry (`name`, `displayName`, `description`, `repeatable`, `isInformationStep`, `destructive`, `external`, `requiresConfirmation`, `authorization`, `executeUrl`), plus `informationRequest` when the process is `AwaitingInformation`. The questions arrive there; submit the answers as that step's payload. See [`docs/INFORMATION_REQUESTS.md`](../../../docs/INFORMATION_REQUESTS.md). The state endpoint (`GET /{service}/{version}/processes/{processId}`) returns the same `requiredStep` with the pending request. A step that isn't a next step of the process is rejected (`pro_step_not_available`).

Steps without a typed handler result return the same fields without `result`. Handler-authored `businessMessages` remain available; `frameworkMessages` is an empty collection unless `AddHttp(new() { IncludeFrameworkMessages = true })` enables diagnostics. Inspect `status` (`completed`, `rejected`, `validationFailed`, `failed`, `exception`, `canceled`, or `pending` for a step that did not run) rather than interpreting HTTP 200 alone as step completion. Kaleido's HTTP JSON options use camelCase property names and string enum values; see the [JSON enum contract](../../Platform/README.md#json-enum-values) for canonical names and numeric-input rules.

---

## Idempotent execution (`Idempotency-Key`)

A process execute request - `POST /{service}/{version}/processes/execute` or a step call - may carry an `Idempotency-Key` header: a caller-chosen key for one logical call (e.g. a GUID), the same on every retry of that call.

The key is **admitted in the store before the step runs**, atomically (ADR 0019). What a request with a key gets:

| Situation | Answer |
|---|---|
| First request with the key | the step runs; a successful (2xx) answer is stored with the key |
| Same key and request, **while the first one still runs** | **409** `pro_idempotency_in_progress` with `Retry-After`; the step does **not** run. Retry after the delay |
| Same key and request, **after the first one answered** | the stored answer again, with `Idempotent-Replayed: true` and the `X-Kaleido-Process-Id` header of the answer; the step does not run again |
| Same key and request, the first one **did not finish with an answer**: it never finished (the service stopped; its lease, `KaleidoHttpOptions.IdempotencyLease`, default 5 minutes, passed), or it failed after its step may have run (a 5xx, an unexpected error, a cancellation, or a step that ran but whose outcome was not saved - `pro_outcome_*`, a conflict after the handler included) | **409** `pro_idempotency_outcome_unknown`, at once: it may or may not have run, so it is not run again with this key until `KaleidoHttpOptions.IdempotencyUnknownOutcomeRetention` (default **7 days**) passes. Read the process state (`GET /{service}/{version}/processes/{processId}`) and decide; continue with a new key |
| Same key, a **different request** (other body, route or `X-Kaleido-Process-Id`) | **422** `pro_idempotency_key_reused`. A key identifies one call; use a new key for a new call |
| A key longer than 200 characters, or - from a caller without an identity (no subject id, no name) - shorter than 16 characters | **400** `pro_invalid_idempotency_key` |
| No header | nothing changes: every request runs |

What this guarantees, and what it does not:
- With a key, **a step runs at most once per key** while the key is kept, even when retries arrive while the first attempt still runs, or after its caller gave up (a client timeout): the answer is stored even when nobody is waiting for it.
- It does **not** make a handler's external work exactly-once. If the service stops inside a handler after it called another system and before the step was saved, that call happened and Kaleido never learned of it; the key is then answered `pro_idempotency_outcome_unknown`. Make such calls idempotent on the other system's side (e.g. pass it a stable id such as the process id and the step name).

Details:
- Keys are **scoped per service, service version and caller**: two callers cannot see each other's answers, and the same key at two services, or two versions of one (`ServiceVersion`, #552), is two keys. The caller is its stable subject id (`sub`, else `NameIdentifier`) when the principal has one - a renamed user keeps their keys and two users with the same name stay apart - else its name. The scope is the tuple {service, service version, identity kind, identity, key}, each part length-framed and the whole hashed (`v2:` + SHA-256), so no separator in a name or a key can make two callers' keys meet (#514); the logs name both the key and the stored record's key. **Callers without an identity share one scope**, so their keys must be random - a GUID, never a counter or a word: a key under 16 characters from such a caller is refused.
- Answers are kept for `KaleidoHttpOptions.IdempotencyRetention` (default **24 hours**, independent of the unknown-outcome retention: `AddHttp(new() { IdempotencyRetention = TimeSpan.FromHours(4) })`) by the idempotency store (`IKaleidoIdempotencyStore.AdmitAsync`/`CompleteAsync`/`ReleaseAsync`) - the SQLite reference store keeps them in its own `IdempotentResults` table in the same database, so there is nothing extra to set up.
- Each admission has its own id, and only that attempt can finalize it (#441): a successful (2xx) answer is stored; a **4xx** - the request was refused before anything was committed (validation, authorization, a concurrent change) - releases the key, so it may be retried with the same key; anything else marks the outcome unknown. An attempt that outlived its record (it expired and the key was admitted again) cannot overwrite or delete the newer record. Storing the outcome never changes the request's own answer: a store failure is logged at Error.
- To resolve an unknown outcome sooner than its retention, an operator reads the process state and deletes the key's record from the idempotency store.
- **Any HTTP client** gets this, not only Kaleido's: send `Idempotency-Key: <a new unique value>` with each logical call, re-send the **same key and the same request** on a retry, wait `Retry-After` seconds on `pro_idempotency_in_progress`, and read the process state on `pro_idempotency_outcome_unknown`. The Kaleido clients do all of it automatically ([`Kaleido.Http.Client`](../Http.Client/README.md#idempotent-execute-calls)).

## Concurrent calls (`409 pro_process_conflict`)

Every save of the process state is conditional on the version it was loaded with (optimistic concurrency, `ProcessorContext.Version`). When two requests for the same process run at the same time - a client retry after a timeout, a double submit, two agents on one case - the first save wins and the second is refused: **409** `pro_process_conflict`, and **nothing of the second request is saved**. The caller reads the process state again (`GET /{service}/{version}/processes/{processId}`) and decides on the next step; repeating the same call blindly would act on a state it has not seen.

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

Capability authorization is set in core with `AddKaleido(…, new() { AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust, … })` (see [`docs/AUTHORIZATION.md`](../../../docs/AUTHORIZATION.md)). With `Authenticated` (the default, ADR 0021), every capability requires an authenticated caller and undeclared capabilities are open to any of them; a host without an authentication scheme does not start. With `None`, which a host chooses explicitly, Kaleido attaches no auth metadata and enforces nothing, so hosts without authentication work. With `ZeroTrust`, a capability needs an explicit rule (its own `[KaleidoAuthorization]` or the service `DefaultAuthorization`) or it is not mapped or discovered at all. `[KaleidoAuthorization]` narrows or opens access:

```csharp
[ProcessStep(DisplayName = "Approve", Description = "...")]
[KaleidoAuthorization(Roles = "radiology")]       // or Policy = "named-policy", or AllowAnonymous = true
public sealed record ApproveStep : IProcessStep;
```

Enforcement works on two layers:

**Route-level gate** — `MapKaleidoHttp()` attaches metadata to each capability's execute/query endpoint at map time: `AllowAnonymous` → `AllowAnonymous()`; otherwise `RequireAuthorization()` plus a role requirement for declared `Roles` and the named ASP.NET policy for a declared `Policy` (ANDed). Evaluation is done by the host's `UseAuthorization()` middleware — the host must wire `AddAuthentication()`/`AddAuthorization()` and `app.UseAuthentication(); app.UseAuthorization();`. `MapKaleidoHttp()` throws `authentication_not_configured` at startup when enforcing without any authentication scheme.

**In-handler evaluation** — `IKaleidoAuthorizer` (registered scoped by `AddHttp()`) covers what per-route metadata cannot express:
- *Multi-step execute* — `ProcessExecutionService` checks every submitted step against its own declaration before anything runs, and the first denial rejects the whole request (401/403). Unknown step names and empty requests are checked as undeclared. At route level, `POST /processes/execute` and `GET /processes/{processId}` require an authenticated caller when enforcing, unless the processor has an `AllowAnonymous` step (then they stay open and the per-step checks decide).
- *No case-level guardrails* (ADR 0010) - resuming or reading a process needs only what the step or route declares; `CreatedBy`/`UpdatedBy` (and the step `StartedBy`/`LastUpdatedBy`) are tags, never enforced. A process id therefore works as a bearer handle; what it grants, where it is exposed and where a host enforces case access is in the [threat model](../../../docs/AUTHORIZATION.md#threat-model-the-process-id-is-a-bearer-handle).
- *Filtered discovery* — central `GET /registry` and the in-host `IKaleidoRegistryService` filter Process steps, query views and routes for each caller, omitting processors whose steps are all hidden. The host's hydrated unfiltered registry is used only for resolving calls; filtering never becomes a shared client's route cache. Execution still checks authorization at the service.

Authorization failures are answered by the host's authentication scheme, as standard ASP.NET Core: endpoint-metadata denials use ASP.NET's default authorization result handler, and Kaleido's exception handling (`KaleidoExceptionEndpointFilter`, inside the endpoints) turns thrown `KaleidoAuthorizationException`s into `ChallengeAsync()` (401) or `ForbidAsync()` (403). Kaleido does not replace the host's authorization result handler, so consumer endpoints are unaffected.

A capability declaring `Policy` on a host with no `IAuthorizationService` fails closed — filtered out of discovery and denied at execution (with a warning log), rather than erroring the whole response.

---

## Central registry participation

`MapKaleidoHttp()` maps the service's Process and Queryable endpoints, not a per-service registry endpoint. An ordinary service host publishing capabilities configures `Kaleido:BaseUrl` with its gateway and calls `AddControlRegistryClient()` before mapping; registration and heartbeat then keep its capabilities in the central registry. The registry host instead supplies its own source with `AddControlRegistry()`.

**Behavior and sensitivity:** each step carries `destructive`, `external` and `requiresConfirmation` as declared on `[ProcessStep]` (also on step summaries and the required step), and every field - step inputs and outputs, query fields, view outputs and parameters - carries `classifications` (e.g. `["PriorAuth:PHI"]`) from its `DataClassificationAttribute`s ([ADR 0015](../../../docs/decisions/0015-declared-step-behavior.md)).

The separate registry host maps `GET /registry`, `/registry/availability`, `/registry/capabilities`, and `/registry/status` with `MapKaleidoControlRegistry()`. It filters the main read per caller, including authorization; the availability and capabilities documents carry separate ETags for conditional hydration. Each participant holds the full central view in memory, and in-host consumers such as MCP use `IKaleidoRegistryService` to filter that view for their caller. There is no downstream fan-out, per-service cache or `?strict` probe on a service endpoint.

The route prefix is derived from `KaleidoServiceOptions.ServiceName` and `ServiceVersion`:
`/{ServiceName}/{ServiceVersion}`. `ServiceVersion` is required for HTTP mapping
(`invalid_service_version`); `ServiceName` must match `[a-z0-9][a-z0-9_-]*` or `AddKaleido()`
refuses it (`invalid_service_name`, KAL2005). Downstream callers use a published URL through the
gateway, not a per-service `RoutePrefix` setting or an instance address.

The registry host owns service registrations and caches merged views with HybridCache, optionally
using its own distributed cache. Each participant hydrates a full local copy; a failed refresh
keeps its last good copy marked partial without changing readiness after initial registration.

---

## Registration requirement

`AddHttp()` registers the transport services (`ProcessExecutionService`, `ProcessStateService`, the exception handler) that `MapKaleidoHttp()` and its endpoint filters depend on. Call it on the `IKaleidoBuilder` before mapping endpoints.

---

## Where to look

- `QueryableEndpointRouteBuilderExtensions.cs` — Queryable route publication
- `ProcessorEndpointRouteBuilderExtensions.cs` — Process route publication
- [`../../Registry/Registry.Http/README.md`](../../Registry/Registry.Http/README.md) — control-registry HTTP API on its own host
- `../Http/Processor/ProcessRoutes.cs` — shared `ProcessContractUrls`, `ProcessRoutePaths`, `ProcessEndpointNames`
- `../Http/Queryable/QueryableRoutes.cs` — shared `QueryableContractUrls`
