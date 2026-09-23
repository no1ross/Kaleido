# Kaleido.Http.Client

This project provides typed HTTP clients for consuming remote Kaleido Process and Queryable endpoints. It is used when one service needs to call another Kaleido service over HTTP.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Kaleido.Http/README.md`](../Kaleido.Http/README.md)
- [`../Kaleido.Http.Abstractions/README.md`](../Kaleido.Http.Abstractions/README.md)

---

## What lives here

### Process client
- `IKaleidoProcessClient` — typed interface for registry, step metadata, process state, and step execution
- `IKaleidoProcessClientFactory` — factory resolved by registered client name
- `KaleidoProcessClient` — concrete HTTP client implementation
- `KaleidoProcessClientException` — exception wrapping non-success HTTP responses
- `KaleidoProcessClientServiceCollectionExtensions` — internal `AddProcessClient(...)` builder extension (consumers register via `AddHttpClients`)

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

The granular `AddProcessClient`/`AddQueryableClient` builder extensions are internal — `AddHttpClients` is the consumer-facing registration seam.

---

## Usage

### Process client

Inject `IKaleidoProcessClientFactory` and resolve a client by name.

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
- lazily fetch and cache the remote registry for the lifetime of the client instance
- automatically forward Kaleido correlation headers on outbound requests
- throw their respective exception types (`KaleidoProcessClientException` / `KaleidoQueryableClientException`) on non-success HTTP responses

`KaleidoProcessClientException` is also thrown when the requested step name is not found in the cached registry (returns `NotFound` status code).

`KaleidoQueryableClientException` is also thrown when the requested context or view name is not found in the cached registry.

---

## What this project does NOT do

This project does not contain:
- server-side endpoint mapping (see [`Kaleido.Http`](../Kaleido.Http/README.md))
- runtime planning or execution logic (see [`Kaleido`](../Kaleido/README.md))
- process state management

---

## Where to look

- `Process/IKaleidoProcessClient.cs` — process client interface
- `Process/KaleidoProcessClient.cs` — process client implementation
- `KaleidoHttpClientsServiceCollectionExtensions.cs` — `AddHttpClients()` consumer registration
- `Process/KaleidoProcessClientServiceCollectionExtensions.cs` — internal `AddProcessClient(...)` registration
- `Queryable/IKaleidoQueryableClient.cs` — queryable client interface
- `Queryable/KaleidoQueryableClient.cs` — queryable client implementation
- `Queryable/KaleidoQueryableClientServiceCollectionExtensions.cs` — internal `AddQueryableClient(...)` registration
