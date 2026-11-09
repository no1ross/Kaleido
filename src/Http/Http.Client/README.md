# Kaleido.Http.Client

This project adapts neutral Process, Queryable and registry clients to HTTP. It sends published versioned capability URLs through one gateway HTTP client, not through individually configured service addresses.

> How this project fits into a whole service, with tested examples: the [consumer guide](../../../docs/guide/README.md). This README is the reference for the project.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../../AGENTS.md`](../../AGENTS.md)
- [`../Http.Server/README.md`](../Http.Server/README.md)
- [`../../Platform/README.md`](../../Platform/README.md)

---

## What lives here

### Central registry HTTP client
- `KaleidoHttpRegistryClient` implements the neutral `IKaleidoRegistryClient` from `Kaleido.Client`: registration, heartbeat, deregistration and ETag-aware reads use the gateway's fixed `/registry` paths. The neutral `ControlRegistryRegistrar` and `CentralRegistrySource` own registration and the hydrated in-host copy. Process and Queryable clients resolve calls from the in-host unfiltered copy, never a per-service registry.

### Process client
- `IKaleidoProcessorClient` (in `Kaleido.Client`) — typed interface for process state and step execution
- `IKaleidoProcessorClientFactory` (in `Kaleido.Client`) — `GetClient(serviceName[, serviceVersion])` chooses the registry's current version or pins an ongoing process to its version
- `KaleidoProcessorClient` — concrete HTTP client implementation

### Queryable client
- `IKaleidoQueryableClient` (in `Kaleido.Client`) — interface for view and source queries (addressed by their published names)
- `IKaleidoQueryableClientFactory` (in `Kaleido.Client`) — resolves a client by published service name, optionally pinned to a version
- `KaleidoQueryableClient` — concrete HTTP client implementation

### Shared
- `KaleidoHttpClientException` — reports non-success Process and Queryable responses (`StatusCode` and the service's `Errors`); registry calls raise the neutral `KaleidoRegistryClientException`.
- `KaleidoHttpClientsServiceCollectionExtensions` — registers one gateway HTTP client via `AddHttpClients()`; Process and Queryable factories resolve the service and version from the central registry, with keyed injection available by service name

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

`AddHttpClients()` registers one named HTTP client (`kaleido-gateway`) at `Kaleido:BaseUrl`.
`AddControlRegistryClient()` registers and hydrates the central registry; every host mapping Kaleido
HTTP capabilities or MCP needs it. Choose stores appropriate to the host:

```csharp
builder.Services.AddKaleido(builder.Configuration, new()
    {
        ServiceName = "orders",
        ServiceVersion = "1",
        Assemblies = [typeof(Program).Assembly],
    })
    .AddInMemoryStores() // local evaluation only
    .AddHttp()
    .AddHttpClients()
    .AddControlRegistryClient();
```

```json
{
  "Kaleido": {
    "BaseUrl": "https://gateway.internal"
  }
}
```

The gateway maps `/registry` to the control registry and each `/{service}/{version}` route to
that version's host. `BaseUrl` must be an absolute `http` or `https` gateway URL without embedded
credentials (`httpclient_missing_base_url` or `httpclient_invalid_options` at startup). A
nonempty `Kaleido:Clients` section is rejected by `AddHttpClients()` at registration, not used
as a per-service fallback. Factories accept the
published service name, optionally with its version; keyed injection uses that service name too
(`[FromKeyedServices("radiology")] IKaleidoProcessorClient`).

**Registry freshness:** no client reads a per-service registry or probes its health. The host
hydrates `/registry/availability` and only fetches `/registry/capabilities` when the revision
changes, using separate ETags (unchanged documents return **304**). On read failure, it retains
the last good copy marked partial. Internal resolution uses that unfiltered copy; discovery for
a user is filtered for that caller, never shared as the route cache.

---

## Taking part in the control registry

With `Kaleido.Registry` and its `Kaleido.Registry.Http` API (#553, #581), each instance
registers itself; a host never builds its own registry from downstream services:

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
  `HeartbeatInterval`), following a change at its next check. It reads capabilities when the
  availability names a revision different from the held one (`CapabilitiesRevision`), or names no
  revision; unchanged known revisions need no new capability read. A deployment reaches each host
  at its next check. Until it has read the registry, and after a failed call, it retries at 1 s
  doubling, at most 30 s.
- **Read in-process.** The host's full unfiltered registry is the call resolver's source; in-host
  lookups and MCP produce caller-filtered discovery separately. The revision follows both documents
  at the registry's pace. `MapKaleidoHttp()` never serves a per-service registry endpoint.
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

**Configuration:** framework defaults, overridden setting by setting by `Kaleido:Resilience`,
apply to the gateway client and the control-registry HTTP client. There are no per-service
resilience settings:

```json
{
  "Kaleido": {
    "BaseUrl": "https://gateway.internal",
    "Resilience": {
      "MaxRetryAttempts": 2,
      "AttemptTimeout": "00:00:05"
    }
  }
}
```

This configuration retries safe gateway and registry calls twice with a five-second attempt
timeout. `Kaleido:Clients:{name}:Resilience` is no longer accepted.

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

There are no downstream client health probes. `kaleido-registration` reports whether this host
registered and read its first central view; dependency status comes from registry heartbeats.

---

## Idempotent execute calls

The process client sends an `Idempotency-Key` (a new GUID) with **every execute call** - the generic execute and every step call. A retry of that call (by the resilience pipeline) re-sends the same key. The service admits the key before the step runs, so:
- a retry after a lost response, or after an attempt timed out while the step still ran, gets the first attempt's answer (`Idempotent-Replayed: true`); the step runs at most once;
- a retry while the first attempt still runs is answered 409 with `Retry-After`, and the client waits and asks again within its total timeout;
- if the first attempt did not finish with an answer (the service stopped while it ran, or it failed after its step may have run), the call fails with `KaleidoHttpClientException` (`Code = httpclient_conflict`, service error `pro_idempotency_outcome_unknown`): it may or may not have run, so read the process state and decide.

**Your own retries.** A new call is a new request with a new key, so calling again after a timeout, a crash or a lost answer would run the step again. To make the *logical* operation safe to repeat, pass your own key: `ExecuteAsync(request, idempotencyKey, ct)` and the by-name `ExecuteStepAsync(stepName, step, processId, idempotencyKey, ct)` send it instead of a new GUID, so repeating the call with the same key and the same request gets the stored answer (`null` keeps a new key per call). Keys are scoped per service version and caller, up to 200 characters; the same key with a different request is refused (`pro_idempotency_key_reused`). The typed `ExecuteStepAsync<TStep>` overloads always send a new key.

This is not exactly-once for a handler's own external calls: a service that stops inside a handler after calling another system may have done that work (see [`Kaleido.Http`](../Http.Server/README.md#idempotent-execution-idempotency-key)). The MCP adapter calls services through these clients and derives one key per logical tool call ([MCP retries](../../Mcp/Mcp/README.md)).

**When another call changed the process first** (two calls for the same process at the same time - a double submit, two agents on one case), the service refuses the second save with **409** `pro_process_conflict`; the client throws `KaleidoHttpClientException` with `Code = httpclient_conflict` and the service's errors. Nothing of that call was saved. Read the process state again and decide on the next step - do not repeat the call blindly.

---

## Usage

### Process client

Inject `IKaleidoProcessorClientFactory` and use the **published service name**, not a configured
client alias. Without a version `GetClient(serviceName)` resolves the registry's current version;
for an existing process use `GetClient(serviceName, serviceVersion)` to stay on the version that
created it. An unpublished service, version or step fails before sending with
`registryclient_route_unavailable`.

```csharp
// A new process starts on the current radiology version.
var started = await clientFactory.GetClient("radiology")
    .ExecuteStepAsync(new CaptureMriInfoStep { /* ... */ }, cancellationToken);

// Pin subsequent calls, including state reads, to that process's service version.
var radiology = clientFactory.GetClient("radiology", "2026-11");
var state = await radiology.GetProcessStateAsync(started.ProcessId, cancellationToken); // null on 404
var response = await radiology.ExecuteStepAsync<CaptureMriInfoStep, CaptureMriInfoResult>(
    new CaptureMriInfoStep { /* ... */ }, cancellationToken);

// Runtime-discovered step, with an explicit process id and logical-call idempotency key.
var next = await radiology.ExecuteStepAsync(
    "CaptureMriInfoStep", JsonSerializer.SerializeToElement(input), started.ProcessId,
    idempotencyKey, cancellationToken);
```

Keep the service version with each process id: the id alone cannot select a version. The typed `ExecuteStepAsync` identifies a published step by the payload's type name (`typeof(TStep).Name`), which must match the step name in the unfiltered central registry — the same name the remote service derives from its own step type. Use the remote step type itself, or a local mirror record with the **same type name**. Mirror records should not implement `IProcessStep`, so the calling service's own discovery does not register them as local steps. The process to continue travels in the `X-Kaleido-Process-Id` correlation header (propagated automatically from the current correlation context; the untyped overload's `processId` argument overrides it).

### Queryable client

Inject `IKaleidoQueryableClientFactory`; query a published service name, optionally pinning its
version when a query belongs to an older process.

```csharp
// View query with typed parameters: source name, then view name
var result = await clientFactory
    .GetClient("member")
    .QueryViewAsync<MemberDetailsParameters, MemberDetailsView>(
        "MemberQueryContextSource", "MemberDetailsViewSource", request, cancellationToken);

// Direct source query
var result = await clientFactory
    .GetClient("code-sets")
    .QuerySourceAsync<ProcedureCodeView>("ProcedureCodeQueryContextSource", request, cancellationToken);

// Direct source query with parameters (e.g. a delegated source scoped to a process)
var result = await clientFactory
    .GetClient("radiology", "2026-11")
    .QuerySourceAsync<ProviderSearchParameters, ProviderSearchResult>(
        "RequestingProviderSearchSource", request, cancellationToken);
```

Sources and views are addressed by their **published names**, the remote type names, as strings. The calling service cannot reference the remote types (they live in another service and are often `internal`). The parameter and result types can be local mirror records. The query uses that source's or view's published `QueryUrl` (`/{service}/{version}/queryable/...`) from the unfiltered central registry. An unpublished name fails locally with `registryclient_route_unavailable`; a query the caller may not use is refused by the service with 403 (`KaleidoHttpClientException`, `httpclient_request_failed`).

---

## Client behavior

Both clients:
- select a service's current or explicitly requested version and call its published step/query URL through the gateway; no route is constructed from a client prefix
- validate that the URL is a safe relative path beneath that version's published logical route before carrying credentials
- use the full hydrated central registry (not one filtered for another caller); no per-downstream registry cache or health probe exists
- automatically forward Kaleido correlation headers on outbound requests
- throw `KaleidoHttpClientException` on non-success capability responses (a 409 conflict: `httpclient_conflict`)
- run every call through the resilience pipeline (see [Resilience](#resilience))

A step, source, view, version or transport absent from the registry fails before the HTTP request (`KaleidoRegistryClientException`, `registryclient_route_unavailable`). Once sent, a service's own 404/403 response becomes `KaleidoHttpClientException` with `httpclient_request_failed`.

### What a call carries: the caller's token, or the service's own (#574)

Every Kaleido client call - a step handler's handoff or lookup, a delegated query, an MCP tool call,
a registry fetch, a heartbeat - is one of two kinds, decided by Kaleido (`KaleidoCallPurpose`):

| `KaleidoCallPurpose` | When | It carries |
|---|---|---|
| `OnBehalfOfCaller` | the call is made while handling an inbound request (an MCP tool call, a step handler's call) | per `Kaleido:OnBehalfOfCalls`: the caller's token unchanged (`Forward`, the default), or the token the host's handler exchanged for the caller (`Exchange`) |
| `ServiceOwn` | a registry fetch or refresh (shared by every caller), a heartbeat, or any call outside an inbound request | what the host's handlers set: typically the service's app-only token (client credentials) |

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
builder.Services.AddKaleido(builder.Configuration, new()
    {
        ServiceName = "orders",
        ServiceVersion = "1",
        Assemblies = [typeof(Program).Assembly],
    })
    .AddInMemoryStores() // local evaluation only
    .AddHttp()
    .AddHttpClients()
    .AddControlRegistryClient()
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
- **Use an `https` gateway.** A non-loopback `Kaleido:BaseUrl` without HTTPS warns at startup:
  credential-bearing calls would use clear text. URLs with embedded credentials are refused.
- **Never trust a published absolute URL.** The registry rejects unsafe paths and the HTTP client
  checks each URL against its service version's logical route before sending caller credentials.

### Front doors: calling for a process

`IKaleidoProcessForwarding.ForwardProcess(processId)` (internal, shared with `Kaleido.Mcp`
through `InternalsVisibleTo`; registered by `AddHttpClients()`): calls
inside it send `X-Kaleido-Process-Id: {processId}`, ahead of the request's correlation context. Use
it when the process id arrives as data rather than as a header (an MCP tool argument), so the called
service records the call with its process. An explicit `processId` passed to
`ExecuteStepAsync(stepName, step, processId)` still wins. The scope flows with the async call only.
## What this project does NOT do

This project does not contain:
- server-side endpoint mapping (see [`Kaleido.Http.Server`](../Http.Server/README.md))
- runtime planning or execution logic (see [`Kaleido`](../../Kaleido/README.md))
- process state management

---

## Where to look

- `../../Client/Registry/ControlRegistryRegistrar.cs` and `../../Client/Registry/CentralRegistrySource.cs` — protocol-neutral registration and hydration in `Kaleido.Client`
- `../../Client/Processor/IKaleidoProcessorClient.cs` and `../../Client/Queryable/IKaleidoQueryableClient.cs` — neutral client interfaces
- `Registry/KaleidoControlRegistryClientServiceCollectionExtensions.cs` — HTTP registry participation through the gateway
- `Registration/KaleidoHttpClientsServiceCollectionExtensions.cs` — one named gateway client, shared resilience and keyed service-name resolution
- `Processor/KaleidoProcessorClient.cs`, `Queryable/KaleidoQueryableClient.cs` — capability implementations using published URLs
- `Transport/KaleidoClientTransport.cs`, `Transport/KaleidoPublishedHttpRoute.cs` — sending, validation and response mapping
- `Transport/KaleidoClientException.cs` — `KaleidoHttpClientException` and `HttpClientErrorCodes`
