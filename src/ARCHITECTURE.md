# Kaleido Source Architecture

This legacy document retains only `src/` architecture sections not yet moved during #585. [The focused architecture map](../docs/architecture/README.md) is the new entry point; remove this file when the remaining sections have migrated.

---

## Terminology

The canonical vocabulary moved to [principles and terminology](../docs/architecture/principles.md); retain this anchor only while the other legacy sections migrate.

---

## Project dependency graph

The processed edges and old sketch's unverified remaining edges now live in [dependencies](../docs/architecture/dependencies.md); each project's `.csproj` is authoritative until it is migrated. The gRPC and registry-gRPC projects remain non-packable placeholders.

---

## 1. Foundation, Eventing, Process and Queryable (#585)

These reached areas have moved to [foundation](../docs/architecture/core.md), [Eventing](../docs/architecture/events.md), [Process](../docs/architecture/capabilities/process.md) and [Queryable](../docs/architecture/capabilities/queryable.md). Their dependency rules are in [dependencies](../docs/architecture/dependencies.md). This legacy file retains only architecture sections for areas still awaiting migration.

The not-yet-processed OpenTelemetry provider supplies `AddOpenTelemetry()` or custom `AddKaleidoInstrumentation()` calls; core instrumentation is BCL `ActivitySource`/`Meter`. Its full ownership moves with that project.

---

## 2. Kaleido.Server and Kaleido.Http.Server

### Server and HTTP endpoint structure

#### Registration and pipeline

- Transport-neutral `Kaleido.Server` owns authorization mode and service defaults. Generic `Kaleido.Http.Server` owns the ASP.NET caller, authorization, correlation, endpoint filters and generic error handling; its filters on Kaleido route groups are `KaleidoExceptionEndpointFilter`, `KaleidoCorrelationEndpointFilter`, `KaleidoJsonEndpointFilter` and `KaleidoCallerContextEndpointFilter`.
- `KaleidoHttpServerServiceCollectionExtensions` in `Kaleido.Http.Server` provides `AddHttp()`; it registers routing, `IHttpContextAccessor` and generic HTTP services, not capability execution or central Registry participation. The host configures its own middleware pipeline (#295).
- `HttpCorrelationContextReader` in `Kaleido.Http.Server` sanitizes inbound HTTP headers into `KaleidoCorrelationContext`.

#### HTTP transport services

- `ProcessExecutionService` — translates HTTP execute requests into runtime `ProcessRequest` values and writes the resolved `ProcessId` into the response headers
- `ProcessStateService` — reads durable process state and maps it to the HTTP response contract

#### HTTP transport conversion

- `QueryBodyResolver` — converts `QueryApiBody` (HTTP transport shape, string enums + `JsonElement` values) to `QueryBody` (runtime shape, typed CLR values + enums) before handing to core. Uses `QueryableFieldDescriptor.FieldType` for value resolution.
- `KaleidoJsonEndpointFilter` — `IEndpointFilter` applied to all Kaleido route groups; wraps `IValueHttpResult` responses in `Results.Json(..., KaleidoJsonOptions.Options)` so enums serialize as strings without touching global `JsonOptions`.
- `KaleidoJsonOptions.Options` — shared `JsonSerializerOptions` in `Kaleido.Platform`, used by both server filter and client.

#### Queryable endpoint mapping (`QueryableEndpointRouteBuilderExtensions`)

- `POST /{service}/{version}/queryable/{source}/query` — direct query of a local or delegated source
- `POST /{service}/{version}/queryable/{source}/{view}/query` — view query

#### Process endpoint mapping (`ProcessorEndpointRouteBuilderExtensions`)

- `POST /{service}/{version}/processes/execute` — multi-step execute
- `GET /{service}/{version}/processes/{processId}` — process state
- `POST /{service}/{version}/processes/steps/{step}` — per-step execute
- The central `GET /registry` publishes these URLs and the process-state URL template; there is no per-step metadata endpoint.

#### Control registry and participation

- `Kaleido.Registry` owns registration, leases, store-backed merged views, transport bindings and HybridCache (`Kaleido:Registry:Cache`). `Kaleido.Registry.Http` maps its fixed `/registry` API through `ControlRegistryEndpointRouteBuilderExtensions`.
- Services register through `POST /registry/instances`, renew leases and deregister. `GET /registry` filters for ordinary callers; participants hydrate unfiltered `GET /registry/availability` and revisioned `GET /registry/capabilities` conditionally. `GET /registry/status` exposes operations status.
- `MapKaleidoHttp()` maps only this service's capability endpoints and requires an `IKaleidoRegistrySource`: business hosts use `AddControlRegistryClient()`, while a registry host has its own store-backed source. `CentralRegistrySource` in `Kaleido.Client` keeps participating hosts' unfiltered last-good copy; `KaleidoRegistryService` in `Kaleido.Server` filters discovery per caller. Neither polls another service.

#### HTTP URL generation

- `KaleidoHttpRoutePrefix`, `ProcessContractUrls` / `ProcessRoutePaths` and `QueryableContractUrls` live in `Kaleido.Http` (`src/Http/Http`); only the server publishes capability URLs. Clients use the published ones.

### Server and HTTP endpoint invariants

- Endpoints adapt contracts and publish routes. They do not reimplement runtime planning or business execution.
- The only service route prefix is `/{ServiceName}/{ServiceVersion}` from `KaleidoServiceOptions`; `ServiceVersion` is required for every service, not only HTTP mapping. The gateway maps that logical public route to the one host serving the version.
- Registry endpoint returns 200 by default. Partial responses are signalled through `Errors`/`IsPartial`; `?strict` opts into a 502 when partial.
- `ConfigureHttpJsonOptions` is never called — Kaleido JSON options are scoped to Kaleido endpoints via `KaleidoJsonEndpointFilter`. Consumer endpoints retain their own serialization behavior.

---

## 3. Kaleido.Platform and Kaleido.Http

### Platform contract structure

#### Process contracts

- Request: `ExecuteProcessRequest`, `ExecuteStepRequest<TStep>`
- Response: `ProcessExecutionResponse`, `ProcessExecutionStepResponse`, `StepExecutionResponse`, `StepExecutionResponse<TResult>`, `ProcessStateResponse`
- Registry discovery: `ProcessRegistryItem`, `ProcessStepSummary`, `ProcessStepResponse`; runtime continuations still use `ProcessRequiredStep`

#### Queryable contracts

- Request: `QueryApiRequest`, `QueryApiRequest<TParameters>` — accepts `QueryApiBody` (transport shape)
- Transport body: `QueryApiBody`, `QueryApiFilterNode`, `QueryApiFilterCondition`, `QueryApiFilterGroup`, `QueryApiSort`, `QueryApiPage` — string enums, raw `JsonElement` filter values
- Registry publication: `QueryableRegistryItem` carries `ServiceName`, `ServiceVersion`, portable `QueryableRegistryField`/`QueryableRegistryViewItem` descriptions and nested transport `Bindings`; errors use `KaleidoErrorResponse` from `Kaleido.Http`. HTTP query responses use the Queryable-owned result model.
- `QueryApiBodyExtensions.ToApiBody()` — converts runtime `QueryBody` → `QueryApiBody` for core callers forwarding over HTTP (e.g. delegated query sources)
- `KaleidoJsonOptions.Options` — shared `JsonSerializerOptions` with `JsonStringEnumConverter` for all Kaleido HTTP serialization

### Platform contract invariants

- `Kaleido.Platform` owns the supported model and wire contract surface shared by `Kaleido.Http.Server`, `Kaleido.Http.Client` and future protocols; `Kaleido.Http` holds only shared HTTP routes.
- Treat the published model as a public API boundary. Prefer additive changes.
- `QueryApiBody` and its sub-types are the wire shape — string enums, `JsonElement` values. Core `QueryBody`/`QueryFilterNode`/`FilterOperator` are never serialized to HTTP directly.
- `KaleidoJsonOptions.Options` is the single `JsonSerializerOptions` for all Kaleido HTTP serialization — server responses and client read/write. All `ReadFromJsonAsync`/`JsonContent.Create` calls in Kaleido code must pass it explicitly.

---

## 4. Kaleido.Client and Kaleido.Http.Client

### Client and HTTP adapter structure

#### Process client

- `IKaleidoProcessorClient` in `Kaleido.Client` is the neutral contract for process state and step execution. `KaleidoProcessorClient` in `Kaleido.Http.Client` sends the URLs the unfiltered central registry publishes; correlation and idempotency are stamped by the HTTP transport. Failed HTTP responses raise `KaleidoHttpClientException`.

#### Queryable client

- `IKaleidoQueryableClient` in `Kaleido.Client` queries sources and views by published name; `KaleidoQueryableClient` sends their published `QueryUrl` through the one gateway client.

#### Client registry participation

- `Kaleido.Client` owns `IKaleidoRegistryClient`, the registration/heartbeat loop (`ControlRegistryRegistrar`), the hydrated copy (`CentralRegistrySource` and `CentralRegistrySync`) and its readiness check (`RegistrationHealthCheck`). None of these orchestrators knows HTTP routes or response types.
- `Kaleido.Http.Client` implements the wire client as `KaleidoHttpRegistryClient` and wires it with `AddControlRegistryClient()`, using the gateway address in `Kaleido:BaseUrl`. Registry call failures cross the neutral seam as `KaleidoRegistryClientException`.

#### Shared client services (#498)

Every piece is built by the container; no service creates another.

- `KaleidoRegistryTargetResolver` reads the hydrated unfiltered registry and selects the current service version, or the explicit version for an existing process. `KaleidoTransportSelector` represents selection and safe-fallback policy, but only HTTP is currently supplied to the resolver; the client send path does not yet invoke cross-protocol fallback.
- `AddHttpClients()` registers one named gateway `HttpClient` using `Kaleido:BaseUrl`. `IKaleidoProcessorClientFactory` and `IKaleidoQueryableClientFactory` create clients by service name, optionally pinned to a version; keyed injection also resolves by service name without an endpoint list.
- `KaleidoOnBehalfOfGuard` enforces what an outbound call carries; `KaleidoClientHandlerFilter` adds the host's `AddKaleidoClientHandler<T>()` handlers to the gateway and registry clients. `IKaleidoClientTransport` sends through the gateway with correlation headers, idempotency, logging, failure mapping and deserialization.

### Client and HTTP adapter invariants

- Clients do not discover or cache registries independently. The host's one central-registry copy supplies the published routes for Process and Queryable calls and MCP; the HTTP adapter validates each relative URL under its version's logical gateway route before sending.
- Both clients automatically forward Kaleido correlation headers on outbound requests.
- A service maps only `/{service}/{version}/...`; the router maps each versioned public route to that version's host, without putting instance addresses in the registry.

---

## 5. Kaleido.Observability.OpenTelemetry

### OpenTelemetry provider structure

- `KaleidoObservabilityOpenTelemetryExtensions` — `AddOpenTelemetry()` on `IKaleidoBuilder` (logging + tracing + metrics + OTLP) and `AddKaleidoInstrumentation()` on `TracerProviderBuilder`/`MeterProviderBuilder`
- Process and Queryable own their BCL `ActivitySource`/`Meter` emitters; this not-yet-migrated project wires the OTel SDK and must reconcile their names in its own unit

### OpenTelemetry provider invariants

- Core is observability-provider-agnostic; this is one possible `Kaleido.Observability.<Technology>` provider.

---

## 6. Kaleido.Mcp

### MCP adapter structure

- `KaleidoMcpServiceCollectionExtensions` — `AddMcp()`: the MCP server (stateless Streamable HTTP), `KaleidoMcpOptions` (configuration over code, ADR 0030), the tool catalog and the tool invoker; startup fails without `AddHttp()`, `AddControlRegistryClient()`, `AddHttpClients()` or `Kaleido:OnBehalfOfCalls = Exchange`
- `KaleidoMcpEndpointRouteBuilderExtensions` — `MapKaleidoMcp(pattern)`; the endpoint runs the same correlation setup as Kaleido's HTTP endpoints
- `KaleidoMcpToolHandler` — the server's hooks: `tools/list` returns the caller's tools, `tools/call` runs one
- Tool catalog: `ToolCatalogProvider` (the caller's tools from the central registry's per-caller filtered view), `ToolCatalogBuilder` (registry to tools, incl. the front-door menu), `ToolCatalogCache` (translated catalogs by registry revision, per registration, #464), `ToolNameBuilder` (`{service}_{capability}`, independent of what the caller sees, #435), `ToolSchemaBuilder` (input metadata to JSON Schema)
- `KaleidoToolInvoker` — one tool call: a query or a step through the Kaleido clients; its parts handle the confirmation round (#439), the front door (`search_tools`, `describe_tool`, `next`) and guided lookups
- `InformationRequestElicitor` — information requests as MCP forms (elicitation, or an MRTR round) and the answers back
- `McpStateProtector` / `McpKeyRingCheck` — every state round-tripped through the client (guided lookup, answers form, answersToken, confirmation) is protected with Data Protection, one purpose per kind (#516); a startup warning when the key ring would not be shared across instances (#452, #484)
- `McpObservability` — the adapter's traces, metrics and boundary logs (`McpTelemetry` names)

### MCP adapter invariants

- Tools are generated from the registry, never declared: a capability is a tool because the service publishes it, and the list is the caller's (a tool not in it is `mcp_unknown_tool`, and no service is called).
- Every tool call goes through the Kaleido clients **as the caller**: it carries the token the host's handler exchanged for that caller (`Kaleido:OnBehalfOfCalls = Exchange`, required), never the agent's token and never the host's own identity (#574).
- The agent and the MCP client are untrusted input: authorization, validation and confirmation are decided on the server ([trust boundary](./Mcp/Mcp/README.md#trust-boundary)).
- The adapter has no wire client logic of its own; it references `Kaleido.Http.Server` and `Kaleido.Http.Client`. Its tool calls use the client's registry-selected service version.

---

## Cross-cutting concerns

### Correlation identity

`KaleidoCorrelationContext` flows through all layers:

- initialized from HTTP headers by `HttpCorrelationContextReader` / `KaleidoCorrelationEndpointFilter` on Kaleido's endpoints (in `Kaleido.Server`; the MCP adapter runs the same setup for its endpoint)
- accessed via the scoped `IKaleidoCorrelationContextAccessor` (registered by `AddKaleido()` in `Kaleido`)
- forwarded by HTTP clients as outbound headers (in `Kaleido.Http.Client`)
- included as tags on observability activities (in `Kaleido`)

#### Correlation context invariants

These are framework invariants — code that violates them produces split traces:

- **A runtime's own scope starts from the request's correlation** — a step handler
  (`ProcessorStepInvoker`) and a query's sources and views (`QueryableService`) each run in their
  own DI scope, initialized with the request's `KaleidoCorrelationContext` (the handler's with its
  process id and executing step). An empty scope would lose the caller, the request id and the
  process id: no record-level filtering by caller, no forwarding of the ids downstream (#413).
  The scope also gets the request's **inbound caller** (`IKaleidoInboundCallerAccessor`: principal
  and presented credential), so a call the handler or source makes is made for that caller
  (`OnBehalfOfCaller`, ADR 0022). Without it every such call goes out as the service's own: an
  anonymous caller would reach the downstream with the service's credential, and a step that
  requires a user behind the call would refuse it (#428). Outside a request (the event relay, a
  hosted service) there is no inbound caller, and the calls stay the service's own.
- **Exactly one RequestId per HTTP request** — `KaleidoCorrelationEndpointFilter` echoes the inbound
  `X-Kaleido-Request-Id` if present, else generates one. Handlers and downstream calls must
  propagate the ambient `RequestId`, never mint a second one.
- **Echo-or-generate** — every Kaleido response echoes the end-to-end pair (`RequestId`,
  `ProcessId`). An absent `RequestId` is generated, not silently dropped. Per-hop headers
  are never echoed.
- **End-to-end vs per-hop** — `RequestId`/`ProcessId` are forwarded unchanged on every hop.
  `X-Kaleido-Calling-Processor`/`-Calling-Step` are set by the caller only when the call is
  made from inside a step, and never forwarded. The processor instance id is never on the
  wire. See [`../docs/architecture/correlation.md`](../docs/architecture/correlation.md).
- **Outbound propagation is automatic** — `KaleidoProcessorClient`/`KaleidoQueryableClient`
  stamp headers via `CorrelationHeaderStamper`. Do not set `X-Kaleido-*` headers manually
  on outbound requests.
- **Scoped identity, ambient read** — resolve `IKaleidoCorrelationContextAccessor` (scoped);
  treat the context as read-only after request init. Mutating it mid-request splits the
  trace. The one sanctioned write is `ProcessorStepInvoker` seeding a step handler's child
  scope (request context + `ProcessId` + `ExecutingStepName`).
- **`X-Kaleido-Process-Id` is a handle, not identity** — it resumes a durable process; it
  is honored unconditionally and is not part of the identity-trust gate
  (`TrustCorrelationIdentity`).

### Observability

Both Queryable and Process publish observability through activity sources and meters:

- Queryable: `Kaleido.Queryable` activity source and meter
- Process: `Kaleido.Processor` activity source and meter (names defined in `ProcessorTelemetry`)

### Event publishing

Moved to [Eventing and the outbox](../docs/architecture/events.md); keep this anchor only while the remaining legacy architecture sections migrate.

### Discoverability

Metadata is derived from CLR types at startup:

- `TypeDescriber` (internal) converts CLR property types to `DataTypeDescriptor` values
- `ConstraintMapper` (internal) converts `ValidationAttribute` usage to `ConstraintContract` values
- Both are used by the Queryable and Process runtimes when building discovery metadata for HTTP endpoints
- The runtime registry descriptors (`DataTypeDescriptor`, `ConstraintContract`, the `Kaleido.Process.Registry` and `Kaleido.Queryable.Metadata` types) remain private. Generic Process and Queryable each project once into Registry-owned `DataTypeMetadata`, `ConstraintMetadata`, `ProcessFieldLookup` and `QueryableRegistryPaging`; installed HTTP adapters add only callable operation bindings. `Kaleido.Registry.Server` never interprets CLR types, and query execution still uses its local catalog.
