# Kaleido.Http.Client

This project provides typed HTTP clients for consuming remote Kaleido Process and Queryable endpoints. It is used when one service needs to call another Kaleido service over HTTP.

> How this project fits into a whole service, with tested examples: the [consumer guide](../../docs/guide/README.md). This README is the reference for the project.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Kaleido.Http/README.md`](../Kaleido.Http/README.md)
- [`../Kaleido.Http.Abstractions/README.md`](../Kaleido.Http.Abstractions/README.md)

---

## What lives here

### Central registry HTTP client
- `KaleidoHttpRegistryClient` implements the neutral `IKaleidoRegistryClient` from `Kaleido.Client`: registration, heartbeat, deregistration and ETag-aware reads use the gateway's fixed `/registry` paths. The neutral `ControlRegistryRegistrar` and `CentralRegistrySource` own registration and the hydrated in-host copy. Named Process and Queryable clients do not fetch per-service registries.

### Process client
- `IKaleidoProcessorClient` (in `Kaleido.Client`) — typed interface for process state and step execution
- `IKaleidoProcessorClientFactory` (in `Kaleido.Client`) — resolves a client by its registered name, or by the downstream's service name (`TryGetClientForService`)
- `KaleidoProcessorClient` — concrete HTTP client implementation

### Queryable client
- `IKaleidoQueryableClient` (in `Kaleido.Client`) — interface for view and source queries (addressed by their published names)
- `IKaleidoQueryableClientFactory` (in `Kaleido.Client`) — resolves a client by its registered name, or by the downstream's service name (`TryGetClientForService`)
- `KaleidoQueryableClient` — concrete HTTP client implementation

### Shared
- `KaleidoHttpClientException` — reports non-success Process and Queryable responses (`StatusCode` and the service's `Errors`); registry calls raise the neutral `KaleidoRegistryClientException`.
- `KaleidoClientServiceCollectionExtensions` — internal `AddKaleidoClient(...)` builder extension (consumers register via `AddHttpClients`): each configured client's Process and Queryable clients are keyed services under its name, built by the container (#498)

---

## When to use this project

Use this project when a service needs to:
- invoke process steps on a remote processor over HTTP
- check the state of a remote process instance
- query a remote query source or view
- read the central registry through the gateway or hydrate an in-host copy of its metadata

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

`AddHttpClients` binds the `Kaleido:Clients` section. Each entry is a named downstream service; `BaseUrl` falls back to the shared `Kaleido:BaseUrl`, and `RoutePrefix` defaults to the key lowercased. A `BaseUrl` must be an absolute `http` or `https` URL: anything else fails at startup with `httpclient_invalid_options`, naming the client and the key (#521).

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

The per-client `AddKaleidoClient` builder extension is internal — `AddHttpClients` is the consumer-facing registration seam. Resolve a client through its factory (`GetClient("Radiology")`) when the name is chosen at runtime, or inject it as a keyed service under its configured name when it is fixed: `[FromKeyedServices("Radiology")] IKaleidoProcessorClient`.

**Route-prefix contract:** `RoutePrefix` must equal the downstream service's `Kaleido:ServiceName` — every endpoint a Kaleido service publishes lives under `/{ServiceName}/...`. The default (client key lowercased) works when the service sets `ServiceName` to the same lowercase name. A mismatch produces 404s at call time, not a startup error.

**Registry freshness:** there is no per-client registry cache or remote registry health probe. Each participating host hydrates the central registry from `/registry/availability` and `/registry/capabilities` using separate conditional ETags. The registry gives the heartbeat and availability intervals; unchanged documents return **304**. A failed read leaves the host's last good in-memory copy marked partial and is retried, without probing every configured client.

---

## Taking part in the control registry

With a control registry (`Kaleido.Http.Registry`, #553), each instance registers itself, and a host's
registry is the control registry's, not one it builds:

```csharp
builder.Services.AddKaleido(builder.Configuration, new() { /* ... */ })
    .AddHttp()
    .AddHttpClients()             // still how this host calls other services
    .AddControlRegistryClient();  // Kaleido:BaseUrl = "https://gateway.internal"
```

- **Registering.** At startup the instance registers (its `ServiceName`, `ServiceVersion` - required -,
  `InstanceId` and its own capabilities; never where it runs), retrying with jittered backoff
  (1 s doubling, at most 30 s) until it succeeds - a registration counts only with a lease naming the
  interval - then reads the registry at once, so the host has it before its first check. It heartbeats at the interval the lease names (+-10%, so a
  fleet spreads), with its health from local checks only
  (never one tagged `remote`); answered 404 `registry_instance_unknown`, it registers again; at shutdown
  it deregisters.
- **Ready.** `kaleido-registration` (tagged `ready`) is unhealthy until the instance has registered and
  read the registry once; a later failure (a heartbeat, a read) never makes it not ready, so a registry
  outage never pulls services out of their load balancers.
- **One registry, in memory.** `CentralRegistrySource` keeps the control registry's capabilities
  (`GET /registry/capabilities`) and each version's availability (`GET /registry/availability`), each with
  its ETag, and composes the one `KaleidoRegistry` every reader on this host gets: each capability with its
  overall status, never an instance. Reads never call out.
- **The registry sets the pace.** The host configures no interval: it heartbeats at the interval each lease
  names, and checks the availability at the interval the availability names (the registry's
  `HeartbeatInterval`), following a change at its next check. The capabilities are read exactly when the
  availability names other capabilities than those held (`CapabilitiesRevision`): a deployment shows on
  every host, capabilities and status together, within one check, and otherwise they are never read
  again. Until it has read the registry, and after a failed call, it retries at 1 s doubling, at most 30 s.
- **Read in-process.** The full registry remains in the host's memory; in-host lookups, hand-offs and MCP
  filter it for their caller. The revision follows both documents, so readers observe either change at
  the registry's pace. `MapKaleidoHttp()` no longer serves a per-service registry endpoint.
- **When the registry cannot be read,** the last good copy is served, partial (a `RegistryReadError` for
  `registry`), until a read succeeds.
- **Its calls are the service's own** (named client `kaleido-registry`, purpose `ServiceOwn`): they carry
  what the host's handlers set (`AddKaleidoClientHandler<T>()`), typically its app-only token, which must
  satisfy the registry's registrant and reader policies.

Settings: `Kaleido:BaseUrl` is the required gateway address, and `ServiceVersion` identifies this
instance's registration. Every interval is the registry's; the router owns where a version runs.

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
- requests with an `Idempotency-Key` header. The process client sends a new key with every execute call (see below), so execute calls are retried too: the service runs the step at most once per key, and a retry gets the first attempt's answer. A keyed call answered **409 with `Retry-After`** (`pro_idempotency_in_progress`: the first attempt still runs) is retried after that delay; any other 409 is not.

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

The process client sends an `Idempotency-Key` (a new GUID) with **every execute call** - the generic execute and every step call. A retry of that call (by the resilience pipeline) re-sends the same key. The service admits the key before the step runs, so:
- a retry after a lost response, or after an attempt timed out while the step still ran, gets the first attempt's answer (`Idempotent-Replayed: true`); the step runs at most once;
- a retry while the first attempt still runs is answered 409 with `Retry-After`, and the client waits and asks again within its total timeout;
- if the first attempt did not finish with an answer (the service stopped while it ran, or it failed after its step may have run), the call fails with `KaleidoHttpClientException` (`Code = httpclient_conflict`, service error `pro_idempotency_outcome_unknown`): it may or may not have run, so read the process state and decide.

**Your own retries.** A new call is a new request with a new key, so calling again after a timeout, a crash or a lost answer would run the step again. To make the *logical* operation safe to repeat, pass your own key: `ExecuteAsync(request, idempotencyKey, ct)` and the by-name `ExecuteStepAsync(stepName, step, processId, idempotencyKey, ct)` send it instead of a new GUID, so repeating the call with the same key and the same request gets the stored answer (`null` keeps a new key per call). Keys are scoped per service and caller, up to 200 characters; the same key with a different request is refused (`pro_idempotency_key_reused`). The typed `ExecuteStepAsync<TStep>` overloads always send a new key.

This is not exactly-once for a handler's own external calls: a service that stops inside a handler after calling another system may have done that work (see [`Kaleido.Http`](../Kaleido.Http/README.md#idempotent-execution-idempotency-key)). The MCP adapter calls services through these clients and derives one key per logical tool call ([MCP retries](../Kaleido.Http.Mcp/README.md)).

**When another call changed the process first** (two calls for the same process at the same time - a double submit, two agents on one case), the service refuses the second save with **409** `pro_process_conflict`; the client throws `KaleidoHttpClientException` with `Code = httpclient_conflict` and the service's errors. Nothing of that call was saved. Read the process state again and decide on the next step - do not repeat the call blindly.

---

## Usage

### Process client

Inject `IKaleidoProcessorClientFactory` and resolve a client by name (`GetClient`; a name not configured under `Kaleido:Clients` throws a `KaleidoConfigurationException`, `httpclient_unknown_client`, listing the configured clients), or by the downstream's service name when you start from a registry entry (`TryGetClientForService(serviceName, out var client)`, as gateways and the MCP adapter do).

```csharp
// Get the remote registry (fetched on first use, cached per configured client)
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
// Get the remote registry (fetched on first use, cached per configured client)
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
- send step and query calls to the contract URL built from the configured route prefix (`/{routePrefix}/processes/steps/{step}`, `/{routePrefix}/queryable/{source}[/{view}]/query`); published capability routes will replace that configuration in the next change
- do not fetch or cache a registry per downstream: the central registry is read by `KaleidoHttpRegistryClient` and hydrated once per participating host
- automatically forward Kaleido correlation headers on outbound requests
- throw `KaleidoHttpClientException` on non-success capability responses (a 409 conflict: `httpclient_conflict`)
- run every call through the resilience pipeline (see [Resilience](#resilience))

A step, source or view the service does not have, or the caller may not use, is the service's own answer: `KaleidoHttpClientException` with `httpclient_request_failed` and status 404 or 403.

### What a call carries: the caller's token, or the service's own (#574)

Every Kaleido client call - a step handler's handoff or lookup, a delegated query, an MCP tool call,
a registry fetch, a heartbeat - is one of two kinds, decided by Kaleido (`KaleidoCallPurpose`):

| `KaleidoCallPurpose` | When | It carries |
|---|---|---|
| `OnBehalfOfCaller` | the call is made while handling an inbound request (an MCP tool call, a step handler's call) | per `Kaleido:OnBehalfOfCalls`: the caller's token unchanged (`Forward`, the default), or the token the host's handler exchanged for the caller (`Exchange`) |
| `ServiceOwn` | a registry fetch or refresh (shared by every caller), a heartbeat, a health probe, any call outside an inbound request | what the host's handlers set: typically the service's app-only token (client credentials) |

`Kaleido:OnBehalfOfCalls` (`KaleidoClientOptions.OnBehalfOfCalls`) is a setting, read once at startup,
not a token: the tokens come from each request, or from the identity provider through the host's handler,
at the time of the call.

**The host's handlers** are standard `DelegatingHandler`s added to every Kaleido client, and to no other
`HttpClient` of the host, with `AddKaleidoClientHandler<THandler>()`, from any library (Duende,
IdentityModel, Microsoft.Identity.Web). `KaleidoCallContext.Of(request)` tells a handler which call it is
(`Purpose`), for whom (`Caller`) and the caller's token to exchange (`CallerAuthorization`; a credential:
never log it).

**Kaleido's guard runs last, whatever the handlers set:**
- an anonymous caller's call carries nothing;
- with `Forward`, a caller's call carries exactly the caller's token;
- with `Exchange`, a caller's call must carry a token a handler set that is not the inbound one, or it is
  refused before it is sent (`httpclient_caller_token_not_exchanged`);
- the service's own calls keep what the handlers set, and never carry a caller's token.

```csharp
builder.Services.AddKaleido(builder.Configuration, new())
    .AddHttp()
    .AddHttpClients()
    .AddKaleidoClientHandler<ServiceTokenHandler>()   // the service's own calls
    .AddKaleidoClientHandler<OnBehalfOfHandler>();    // with Kaleido:OnBehalfOfCalls = Exchange

// The service's own calls: its app-only token (client credentials, Entra ID here).
public sealed class ServiceTokenHandler(IConfidentialClientApplication app) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (KaleidoCallContext.Of(request).Purpose == KaleidoCallPurpose.ServiceOwn)
        {
            var own = await app.AcquireTokenForClient(["api://kaleido-platform/.default"]).ExecuteAsync(cancellationToken);
            request.Headers.Authorization = new("Bearer", own.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}

// A caller's calls: the caller's token exchanged (Entra ID on-behalf-of here; RFC 8693 token exchange elsewhere).
public sealed class OnBehalfOfHandler(IConfidentialClientApplication app) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (KaleidoCallContext.Of(request) is { Purpose: KaleidoCallPurpose.OnBehalfOfCaller, CallerAuthorization: { } inbound })
        {
            var exchanged = await app.AcquireTokenOnBehalfOf(["api://kaleido-platform/.default"], new UserAssertion(inbound["Bearer ".Length..]))
                .ExecuteAsync(cancellationToken);
            request.Headers.Authorization = new("Bearer", exchanged.AccessToken);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
```

- **`Forward` (the default)** suits services that all accept the same token (one platform audience).
  **An MCP host requires `Exchange`**: the MCP authorization specification forbids passing the token issued
  for the MCP server through to other APIs, so `AddMcp()` fails at startup otherwise
  (`mcp_on_behalf_of_calls_not_exchanged`).
- Handlers run in the order added, after the handlers configured on the client itself, and are created per
  client pipeline (transient): keep tokens in a cache they depend on, not in their own fields.
- **Use `https` base URLs.** A client whose `BaseUrl` is neither `https` nor loopback is logged as a Warning
  at startup: it would send a caller's credential in clear text.
- **`RoutePrefix` is a service name** (`[a-z0-9][a-z0-9_-]*`, the downstream `ServiceName`):
  `AddHttpClients()` refuses any other value at startup (`httpclient_invalid_route_prefix`).

### Front doors: calling for a process

`IKaleidoProcessForwarding.ForwardProcess(processId)` (internal, shared with `Kaleido.Http.Mcp`
through `InternalsVisibleTo`; registered by `AddHttpClients()` when a client is configured): calls
inside it send `X-Kaleido-Process-Id: {processId}`, ahead of the request's correlation context. Use
it when the process id arrives as data rather than as a header (an MCP tool argument), so the called
service records the call with its process. An explicit `processId` passed to
`ExecuteStepAsync(stepName, step, processId)` still wins. The scope flows with the async call only.
## What this project does NOT do

This project does not contain:
- server-side endpoint mapping (see [`Kaleido.Http`](../Kaleido.Http/README.md))
- runtime planning or execution logic (see [`Kaleido`](../Kaleido/README.md))
- process state management

---

## Where to look

- `ControlRegistryRegistrar.cs`, `RegistrationHealthCheck.cs` - registering, heartbeats, readiness
- `CentralRegistrySource.cs`, `CentralRegistrySync.cs` - the host's copy of the control registry, and keeping it fresh
- `../Kaleido.Http.Abstractions/Processor/IKaleidoProcessorClient.cs` — process client interface
- `Processor/KaleidoProcessorClient.cs` — process client implementation
- `../Kaleido.Http.Abstractions/Queryable/IKaleidoQueryableClient.cs` — queryable client interface
- `Queryable/KaleidoQueryableClient.cs` — queryable client implementation
- `KaleidoHttpClientsServiceCollectionExtensions.cs` — `AddHttpClients()` consumer registration
- `KaleidoClientServiceCollectionExtensions.cs` — internal `AddKaleidoClient(...)` registration: one client's definition, named `HttpClient` and keyed services
- `../Kaleido.Http.Abstractions/KaleidoClientCatalog.cs` — the configured clients by name
- `KaleidoClientTransport.cs` — how every client call is sent and its answer read
- `KaleidoClientException.cs` — `KaleidoHttpClientException` and `HttpClientErrorCodes`
