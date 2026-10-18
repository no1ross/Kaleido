# Kaleido.Http.Client

This project provides typed HTTP clients for consuming remote Kaleido Process and Queryable endpoints. It is used when one service needs to call another Kaleido service over HTTP.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Kaleido.Http/README.md`](../Kaleido.Http/README.md)
- [`../Kaleido.Http.Abstractions/README.md`](../Kaleido.Http.Abstractions/README.md)

---

## What lives here

### Shared registry fetch
- `KaleidoRemoteRegistry` — singleton that fetches `GET /{routePrefix}/registry` once per named client and caches the combined `AggregatedRegistryResponse` under the client's own key `kaleido-client:{clientName}` in `IRegistrySnapshotStore`; both typed clients project their half (`Processes` / `Queryables`) from it for discovery (`GetRegistryAsync`, a router's aggregation), so a service's registry is only ever fetched once regardless of how many client types consume it. Capability calls never use it: they go to the contract URL built from the route prefix. `InvalidateRegistryAsync()` drops the snapshot — with a distributed store this propagates across all instances sharing it.

### Process client
- `IKaleidoProcessorClient` (in `Kaleido.Http.Abstractions`) — typed interface for the registry, process state, and step execution
- `IKaleidoProcessorClientFactory` (in `Kaleido.Http.Abstractions`) — resolves a client by its registered name, or by the downstream's service name (`TryGetClientForService`)
- `KaleidoProcessorClient` — concrete HTTP client implementation

### Queryable client
- `IKaleidoQueryableClient` (in `Kaleido.Http.Abstractions`) — interface for the registry, view queries, and direct source queries (sources and views are addressed by their published names)
- `IKaleidoQueryableClientFactory` (in `Kaleido.Http.Abstractions`) — resolves a client by its registered name, or by the downstream's service name (`TryGetClientForService`)
- `KaleidoQueryableClient` — concrete HTTP client implementation

### Shared
- `KaleidoHttpClientException` — the one exception both clients throw: non-success responses (`StatusCode`, the service's `Errors`) and registry lookup failures, with an `httpclient_*` `Code`
- `KaleidoClientServiceCollectionExtensions` — internal `AddProcessorClient(...)`/`AddQueryableClient(...)` builder extensions (consumers register via `AddHttpClients`)

---

## When to use this project

Use this project when a service needs to:
- invoke process steps on a remote processor over HTTP
- check the state of a remote process instance
- query a remote query source or view
- fetch the registry (step and query metadata) of a remote Kaleido service

Typical scenarios:
- one processor's handler must signal a required step on another processor
- a process handler needs reference data from a remote Queryable service before executing
- an orchestrator or gateway needs to drive remote process steps
- a delegated query source calls a downstream queryable service

---

## Registration

Register all downstream clients from configuration with `AddHttpClients()` — each named client gets **both** a Process client and a Queryable client:

```csharp
builder.Services.AddKaleido(builder.Configuration)
    .AddHttpClients();
```

`AddHttpClients` binds the `Kaleido:Clients` section. Each entry is a named downstream service; `BaseUrl` falls back to the shared `Kaleido:BaseUrl`, and `RoutePrefix` defaults to the key lowercased:

```json
{
  "Kaleido": {
    "BaseUrl": "http://router:8080",
    "Clients": {
      "Member":    { },
      "CodeSet":   { },
      "Radiology": { "BaseUrl": "https://radiology-service-host", "RoutePrefix": "radiology" }
    }
  }
}
```

The granular `AddProcessorClient`/`AddQueryableClient` builder extensions are internal — `AddHttpClients` is the consumer-facing registration seam.

**Route-prefix contract:** `RoutePrefix` must equal the downstream service's `Kaleido:ServiceName` — every endpoint a Kaleido service publishes lives under `/{ServiceName}/...`. The default (client key lowercased) works when the service sets `ServiceName` to the same lowercase name. A mismatch produces 404s at call time, not a startup error.

**Registry freshness:** a service's registry is filtered for the caller that fetches it, so the cached copy is the view of the identity the client's requests carry (a router's registry clients use its service identity). It is keyed `kaleido-client:{clientName}`, never the service's own `kaleido:{ServiceName}` (where the service keeps its unfiltered snapshot), so with a shared `IRegistrySnapshotStore` (e.g. Redis) replicas of the consuming host share one copy per client without ever overwriting a service's snapshot. Step and query calls do not read it, so one caller's view never decides what another caller can call. Options, settable per client or as `Kaleido:RegistryTtl` / `Kaleido:StrictRegistryProbe` defaults:
- `RegistryTtl` - how often the client checks whether the remote registry changed (default **30 seconds**). A check sends the known revision as `If-None-Match`; an unchanged registry answers an empty **304** and the snapshot is kept, so a short interval is cheap. A capability call answered **404** (the service was deployed without that step, source or view) drops the snapshot at once, so the next read loads the current registry. Null = never checked again until `InvalidateRegistryAsync()` or a 404.
- `StrictRegistryProbe` — the `kaleido-{name}` health check probes `/{prefix}/registry?strict` so a partially-degraded aggregating downstream reports `Unhealthy`.

Failed fetches are never cached - the next call retries naturally.

---

## Resilience

Every call a Kaleido client makes - registry reads, queries, process execution - runs through a resilience pipeline built on `Microsoft.Extensions.Http.Resilience` (Polly). **It is on by default**; nothing needs configuring.

The pipeline, outermost first:

| Stage | Default | What it does |
|---|---|---|
| Total timeout | 30 s | Bounds a whole call, all attempts and delays included. |
| Retry | 3 retries, 500 ms base delay, exponential with jitter | Retries **safe** requests after a transient failure: 5xx, 408, 429, a timeout, a connection error. |
| Circuit breaker | opens at 50 % failures (at least 10 calls within 30 s), stays open 15 s | Stops calling a service that keeps failing; calls fail at once until it tries again. |
| Attempt timeout | 10 s | Bounds one attempt. |

**Only safe requests are retried:**
- reads (`GET`, `HEAD`): registry, process state;
- requests with an `Idempotency-Key` header. The process client sends a new key with every execute call (see below), so execute calls are retried too: a retry the service has already answered gets the stored answer, the step does not run twice.

A request with neither is never retried. Retries re-send the same request, so the correlation headers (the same `X-Kaleido-Request-Id`) and the idempotency key stay the same; the resilience events (retries, an opening circuit) appear in the logs and metrics of `Microsoft.Extensions.Resilience`.

**Configuration - three layers.** The framework defaults; `Kaleido:Resilience` for all clients; `Kaleido:Clients:{name}:Resilience` for one client, overriding **only the settings it names** (the rest come from the shared section):

```json
{
  "Kaleido": {
    "Resilience": {
      "MaxRetryAttempts": 2,
      "AttemptTimeout": "00:00:05"
    },
    "Clients": {
      "member":    { "BaseUrl": "http://member:8080" },
      "radiology": { "BaseUrl": "http://radiology:8080", "Resilience": { "AttemptTimeout": "00:00:20" } },
      "legacy":    { "BaseUrl": "http://legacy:8080",    "Resilience": { "Enabled": false } }
    }
  }
}
```

Here `member` retries twice with 5 s per attempt (shared), `radiology` retries twice with 20 s per attempt (its own timeout), and `legacy` has no resilience at all.

The timeout, retry and circuit-breaker settings are those of `KaleidoResilienceSettings` (core), the same vocabulary the event relay uses under `Kaleido:Events:Resilience`; the two sections are bound independently and have their own defaults, so a client's `MaxRetryAttempts` never changes how often events are retried.

| Setting | Default | |
|---|---|---|
| `Enabled` | `true` | `false` turns resilience off. |
| `TotalTimeout` | `00:00:30` | A whole call, all attempts included. |
| `AttemptTimeout` | `00:00:10` | One attempt. |
| `MaxRetryAttempts` | `3` | Retries of a safe request; `0` = none. |
| `RetryDelay` | `00:00:00.5` | Base delay; grows exponentially, with jitter. |
| `CircuitBreakerFailureRatio` | `0.5` | Share of failed calls that opens the circuit. |
| `CircuitBreakerMinimumThroughput` | `10` | Calls needed within the sampling duration before the circuit can open. |
| `CircuitBreakerSamplingDuration` | `00:00:30` | Window the failure ratio is measured over. |
| `CircuitBreakerBreakDuration` | `00:00:15` | How long an open circuit fails calls at once. |
| `HealthCheckTimeout` | `00:00:05` | Timeout of the client health check. |

**Health checks** (`kaleido-{name}`) are never retried and have their own short timeout (`HealthCheckTimeout`), so health reporting stays fast and honest: a service that does not answer in time, or whose circuit is open, reports `Unhealthy` right away.

---

## Idempotent execute calls

The process client sends an `Idempotency-Key` (a new GUID) with **every execute call** - the generic execute and every step call. A retry of that call (by the resilience pipeline) re-sends the same key, and the service answers it with the result it stored for the first attempt: the step never runs twice because a response was lost on the way back. The MCP adapter calls services through these clients, so its tool calls get the same protection. The service side is described in [`Kaleido.Http`](../Kaleido.Http/README.md#idempotent-execution-idempotency-key).

**When another call changed the process first** (two calls for the same process at the same time - a double submit, two agents on one case), the service refuses the second save with **409** `pro_process_conflict`; the client throws `KaleidoHttpClientException` with `Code = httpclient_conflict` and the service's errors. Nothing of that call was saved. Read the process state again and decide on the next step - do not repeat the call blindly.

---

## Usage

### Process client

Inject `IKaleidoProcessorClientFactory` and resolve a client by name (`GetClient`; a name not configured under `Kaleido:Clients` throws a `KaleidoConfigurationException`, `httpclient_unknown_client`, listing the configured clients), or by the downstream's service name when you start from a registry entry (`TryGetClientForService(serviceName, out var client)`, as gateways and the MCP adapter do).

```csharp
// Get the full registry (lazily fetched and cached per client instance)
var registry = await clientFactory
    .GetClient("RemoteProcessor")
    .GetRegistryAsync(cancellationToken);

// Get process state (returns null on 404)
var state = await clientFactory
    .GetClient("RemoteProcessor")
    .GetProcessStateAsync(processId, cancellationToken);

// Execute a step — the step name is the payload's type name ("CaptureMriInfoStep")
var response = await clientFactory
    .GetClient("RemoteProcessor")
    .ExecuteStepAsync(new CaptureMriInfoStep { ... }, cancellationToken);

// Execute a step (typed result)
var response = await clientFactory
    .GetClient("RemoteProcessor")
    .ExecuteStepAsync<CaptureMriInfoStep, CaptureMriInfoResult>(
        new CaptureMriInfoStep { ... },
        cancellationToken);

// Execute a step discovered at runtime (no compile-time step type), continuing a given process
var response = await clientFactory
    .GetClient("RemoteProcessor")
    .ExecuteStepAsync("CaptureMriInfoStep", JsonSerializer.SerializeToElement(input), processId, cancellationToken);
```

The typed `ExecuteStepAsync` identifies the remote step by the payload's type name (`typeof(TStep).Name`), which must match a step name in the remote registry — the same name the remote service derives from its own step type. Use the remote step type itself, or a local mirror record with the **same type name**. Mirror records should not implement `IProcessStep`, so the calling service's own discovery does not register them as local steps. The process to continue travels in the `X-Kaleido-Process-Id` correlation header (propagated automatically from the current correlation context; the untyped overload's `processId` argument overrides it).

### Queryable client

Inject `IKaleidoQueryableClientFactory` and resolve a client by name.

```csharp
// Get the full registry (lazily fetched and cached per client instance)
var registry = await clientFactory
    .GetClient("MemberService")
    .GetRegistryAsync(cancellationToken);

// View query with typed parameters: source name, then view name
var result = await clientFactory
    .GetClient("MemberService")
    .QueryViewAsync<MemberDetailsParameters, MemberDetailsView>(
        "MemberQueryContextSource", "MemberDetailsViewSource", request, cancellationToken);

// Direct source query
var result = await clientFactory
    .GetClient("CodeSet")
    .QuerySourceAsync<ProcedureCodeView>("ProcedureCodeQueryContextSource", request, cancellationToken);

// Direct source query with parameters (e.g. a delegated source scoped to a process)
var result = await clientFactory
    .GetClient("Radiology")
    .QuerySourceAsync<ProviderSearchParameters, ProviderSearchResult>(
        "RequestingProviderSearchSource", request, cancellationToken);
```

Sources and views are addressed by their **published names**, the remote type names, as strings. The calling service cannot reference the remote types (they live in another service and are often `internal`). The parameter and result types can be local mirror records. The query goes to `/{routePrefix}/queryable/{source}[/{view}]/query` without consulting the registry; an unknown name, or one the caller may not use, gets the service's own 404 or 403 (`KaleidoHttpClientException`, `httpclient_request_failed`).

---

## Client behavior

Both clients:
- share `KaleidoRemoteRegistry` — one `GET /{routePrefix}/registry` call per named client, cached as `AggregatedRegistryResponse`; each client projects its half for discovery
- send step and query calls to the contract URL built from the route prefix (`/{routePrefix}/processes/steps/{step}`, `/{routePrefix}/queryable/{source}[/{view}]/query`), never to a URL read from the registry
- a non-success registry response (including 404 — every Kaleido service is expected to publish `/{service}/registry`) throws `KaleidoHttpClientException`
- automatically forward Kaleido correlation headers on outbound requests
- throw `KaleidoHttpClientException` on non-success HTTP responses (a 409 conflict: `httpclient_conflict`)
- drop the cached registry when a capability call answers **404** (the service does not have it, perhaps deployed without it), so the next discovery read loads the current one
- run every call through the resilience pipeline (see [Resilience](#resilience))

A step, source or view the service does not have, or the caller may not use, is the service's own answer: `KaleidoHttpClientException` with `httpclient_request_failed` and status 404 or 403.

### Front doors: calling as the caller, for a process

A front door calls services on behalf of its callers. Front doors are built into the solution (the
MCP adapter), so the two per-call scopes are internal, shared with `Kaleido.Http.Mcp` through
`InternalsVisibleTo`. Both are opt-in and flow with the async call only (both services are
registered by `AddHttpClients()` when at least one client is configured):

- `IKaleidoCallerForwarding.ForwardCaller(authorization)`: calls inside it send the caller's
  `Authorization` instead of the host's configured credentials.
- `IKaleidoProcessForwarding.ForwardProcess(processId)`: calls inside it send
  `X-Kaleido-Process-Id: {processId}`, ahead of the request's correlation context. Use it when the
  process id arrives as data rather than as a header (an MCP tool argument), so the called service
  records the call with its process. An explicit `processId` passed to
  `ExecuteStepAsync(stepName, step, processId)` still wins.

---

## What this project does NOT do

This project does not contain:
- server-side endpoint mapping (see [`Kaleido.Http`](../Kaleido.Http/README.md))
- runtime planning or execution logic (see [`Kaleido`](../Kaleido/README.md))
- process state management

---

## Where to look

- `../Kaleido.Http.Abstractions/Processor/IKaleidoProcessorClient.cs` — process client interface
- `Processor/KaleidoProcessorClient.cs` — process client implementation
- `../Kaleido.Http.Abstractions/Queryable/IKaleidoQueryableClient.cs` — queryable client interface
- `Queryable/KaleidoQueryableClient.cs` — queryable client implementation
- `KaleidoHttpClientsServiceCollectionExtensions.cs` — `AddHttpClients()` consumer registration
- `KaleidoClientServiceCollectionExtensions.cs` — internal `AddProcessorClient(...)`/`AddQueryableClient(...)` registration
- `KaleidoClientException.cs` — `KaleidoHttpClientException` and `HttpClientErrorCodes`
