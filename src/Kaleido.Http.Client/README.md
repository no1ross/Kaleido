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
- `KaleidoRemoteRegistry` — singleton that fetches `GET /{routePrefix}/registry` once per named client and caches the combined `AggregatedRegistryResponse` under the canonical key `kaleido:{routePrefix}` in `IRegistrySnapshotStore`; both typed clients project their half (`Processes` / `Queryables`) from it, so a service's registry is only ever fetched once regardless of how many client types consume it. `InvalidateRegistry()` drops the snapshot — with a distributed store this propagates across all instances sharing it.

### Process client
- `IKaleidoProcessorClient` — typed interface for registry, step metadata, process state, and step execution
- `IKaleidoProcessorClientFactory` — factory resolved by registered client name
- `KaleidoProcessorClient` — concrete HTTP client implementation
- `KaleidoHttpClientException` — exception wrapping non-success HTTP responses
- `KaleidoClientServiceCollectionExtensions` — internal `AddProcessorClient(...)` builder extension (consumers register via `AddHttpClients`)

### Queryable client
- `IKaleidoQueryableClient` — typed interface for registry, context metadata, view queries, and direct context queries
- `IKaleidoQueryableClientFactory` — factory resolved by registered client name
- `KaleidoQueryableClient` — concrete HTTP client implementation
- `KaleidoQueryableClientException` — exception wrapping non-success HTTP responses
- `KaleidoQueryableClientServiceCollectionExtensions` — internal `AddQueryableClient(...)` builder extension (consumers register via `AddHttpClients`)

---

## When to use this project

Use this project when a service needs to:
- invoke process steps on a remote processor over HTTP
- check the state of a remote process instance
- query a remote Queryable context or view
- fetch registry or metadata from a remote Kaleido service

Typical scenarios:
- one processor's handler must signal a required step on another processor
- a process handler needs reference data from a remote Queryable service before executing
- an orchestrator or gateway needs to drive remote process steps
- a delegated view implementation calls a downstream queryable service

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

**Registry freshness:** cached registry snapshots are keyed `kaleido:{RoutePrefix}` — the same key the downstream writes its own snapshot under — so a shared `IRegistrySnapshotStore` (e.g. Redis) lets a router skip the HTTP call entirely on a fresh hit, and each service's TTL refreshes only its own entry. Options, settable per client or as `Kaleido:RegistryTtl` / `Kaleido:StrictRegistryProbe` defaults:
- `RegistryTtl` — max age of the cached snapshot (age measured from the remote's `GeneratedAt`). Default null = cached until `InvalidateRegistry()`. Set it on long-running consumers so downstream redeploys become visible without a restart.
- `StrictRegistryProbe` — the `kaleido-{name}` health check probes `/{prefix}/registry?strict` so a partially-degraded aggregating downstream reports `Unhealthy`.

Failed fetches are never cached — the next call retries naturally. For in-request retry/backoff/circuit-breaking, attach `AddStandardResilienceHandler()` (or Polly) to the named client via `configureClient` — that's the seam for transient-failure policy.

---

## Usage

### Process client

Inject `IKaleidoProcessorClientFactory` and resolve a client by name.

```csharp
// Get the full registry (lazily fetched and cached per client instance)
var registry = await clientFactory
    .GetClient("RemoteProcessor")
    .GetRegistryAsync(cancellationToken);

// Get metadata for a single step
var metadata = await clientFactory
    .GetClient("RemoteProcessor")
    .GetStepMetadataAsync("CaptureMriInfo", cancellationToken);

// Get process state (returns null on 404)
var state = await clientFactory
    .GetClient("RemoteProcessor")
    .GetProcessStateAsync(processId, cancellationToken);

// Execute a step (untyped)
var response = await clientFactory
    .GetClient("RemoteProcessor")
    .ExecuteStepAsync(new MyRemoteStep { ... }, processId: existingId);

// Execute a step (typed result)
var response = await clientFactory
    .GetClient("RemoteProcessor")
    .ExecuteStepAsync<MyRemoteStep, MyRemoteResult>(
        new MyRemoteStep { ... },
        processId: existingId);
```

### Queryable client

Inject `IKaleidoQueryableClientFactory` and resolve a client by name.

```csharp
// Get the full registry (lazily fetched and cached per client instance)
var registry = await clientFactory
    .GetClient("MemberService")
    .GetRegistryAsync(cancellationToken);

// Get metadata for a single context
var metadata = await clientFactory
    .GetClient("MemberService")
    .GetContextMetadataAsync("Members", cancellationToken);

// View query with typed parameters
var result = await clientFactory
    .GetClient("MemberService")
    .QueryViewAsync<MemberDetailsParameters, MemberDetailsView>(
        "Members", "MemberDetails", request, cancellationToken);

// Direct context query
var result = await clientFactory
    .GetClient("CodeSet")
    .QueryContextAsync<ProcedureCodeView>("ProcedureCodes", request, cancellationToken);
```

---

## Client behavior

Both clients:
- share `KaleidoRemoteRegistry` — one `GET /{routePrefix}/registry` call per named client, cached as `AggregatedRegistryResponse`; each client projects its half
- a non-success registry response (including 404 — every Kaleido service is expected to publish `/{service}/registry`) throws `KaleidoHttpClientException`
- automatically forward Kaleido correlation headers on outbound requests
- throw their respective exception types (`KaleidoHttpClientException` / `KaleidoQueryableClientException`) on non-success HTTP responses

`KaleidoHttpClientException` is also thrown when the requested step name is not found in the cached registry (returns `NotFound` status code).

`KaleidoQueryableClientException` is also thrown when the requested context or view name is not found in the cached registry.

---

## What this project does NOT do

This project does not contain:
- server-side endpoint mapping (see [`Kaleido.Http`](../Kaleido.Http/README.md))
- runtime planning or execution logic (see [`Kaleido`](../Kaleido/README.md))
- process state management

---

## Where to look

- `Processor/IKaleidoProcessorClient.cs` — process client interface
- `Processor/KaleidoProcessorClient.cs` — process client implementation
- `KaleidoHttpClientsServiceCollectionExtensions.cs` — `AddHttpClients()` consumer registration
- `Processor/KaleidoClientServiceCollectionExtensions.cs` — internal `AddProcessorClient(...)` registration
- `Queryable/IKaleidoQueryableClient.cs` — queryable client interface
- `Queryable/KaleidoQueryableClient.cs` — queryable client implementation
- `Queryable/KaleidoQueryableClientServiceCollectionExtensions.cs` — internal `AddQueryableClient(...)` registration
