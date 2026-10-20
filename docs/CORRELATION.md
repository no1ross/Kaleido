# Correlation context flow

Kaleido propagates a small correlation context across service hops so a single logical
operation can be traced through every service it touches — in logs, traces, and emitted
process events.

The context itself (`KaleidoCorrelationContext`) is **transport-agnostic** and lives in
`Kaleido` core. The HTTP wire names (`KaleidoCorrelationHeaders`) live in
`Kaleido.Http.Abstractions`. A future transport (e.g. gRPC) gets its own header/metadata
constants in its own project — the context shape does not change.

## Fields and headers

Correlation has three kinds of fields:

- **End-to-end:** set once, forwarded unchanged on every hop.
- **Per-hop:** set by the caller for *this* call, never forwarded.
- **Local:** never on the wire.

| Kind | Context field | HTTP header | Purpose |
|---|---|---|---|
| End-to-end | `RequestId` | `X-Kaleido-Request-Id` | The user action. Generated (new GUID) when the caller does not supply one — always non-empty after the inbound read. |
| End-to-end | `ProcessId` | `X-Kaleido-Process-Id` | The process instance the call belongs to. |
| Per-hop | `CallingProcessorName` | `X-Kaleido-Calling-Processor` | The processor whose **step** made this call. |
| Per-hop | `CallingStepName` | `X-Kaleido-Calling-Step` | The step in that processor that made this call. |
| Local | `ExecutingStepName` | *(none)* | The step this service is executing; set by the runtime for a step handler's scope and used to stamp outbound calls. |
| Local | `CallerName` / `CallerRoles` | *(none)* | Authenticated caller — from the request principal after authentication, never from headers. |

The processor **instance id** (`KaleidoServiceOptions.InstanceId`) is local too: each
service records its own instance id in its trace tags, events and logs. It is never sent,
read or echoed.

Example — a router forwards a user request to Intake, whose step calls Radiology, whose
step calls Configuration:

| Hop | RequestId / ProcessId | Calling processor / step seen by the receiver |
|---|---|---|
| Router → Intake | forwarded | *(none — the router forwards, it does not execute a step)* |
| Intake → Radiology | unchanged | `intake` / Intake's executing step |
| Radiology → Configuration | unchanged | `radiology` / Radiology's executing step |

## Inbound path (server)

```
HTTP request to a Kaleido endpoint (host middleware, incl. authentication, has run)
  └─ KaleidoExceptionEndpointFilter        (first filter on every Kaleido route group)
       └─ KaleidoCorrelationEndpointFilter
            └─ HttpCorrelationContextReader
                 reads + sanitizes RequestId, ProcessId, Calling-Processor, Calling-Step
                 └─ writes KaleidoCorrelationContext into
                    IKaleidoCorrelationContextAccessor (scoped)
            ├─ tags the current Activity (this service's own instance id + the fields above)
            └─ echoes RequestId and ProcessId on the response
```

Only Kaleido's endpoints get this; the host's own endpoints are untouched (no `X-Kaleido-*` headers).
The MCP endpoint (`MapKaleidoMcp`), mapped by the MCP SDK, runs the same correlation setup itself.

- String values pass through the internal `HttpHeaderSanitizerExtensions.Sanitize` (RFC 7230 — printable ASCII, max 256
  chars); unsanitizable content is dropped.
- `ProcessId` must parse as a GUID. A malformed value throws `BadHttpRequestException`,
  which `KaleidoExceptionEndpointFilter` maps to **HTTP 400** (`argument_error`) — see
  [`ERROR_CODES.md`](./ERROR_CODES.md).
- A missing `X-Kaleido-Request-Id` is replaced with a fresh GUID, so every request has an
  ID downstream. `KaleidoCorrelationContext.IsEmpty` reports whether any meaningful field
  was supplied.
- Responses echo only the end-to-end pair. Per-hop identity describes the request, not
  the response.

### Header trust

Identity-bearing headers (`RequestId`, `Calling-Processor`, `Calling-Step`) are honored
only for trusted callers — untrusted callers get a fresh `RequestId` and the per-hop fields
are dropped (the response echo reflects the resolved `RequestId`, so callers see exactly
what was used). `ProcessId` is always honored: it is a resumable process handle, not an
identity claim.

Trust is governed by `KaleidoHttpOptions.TrustCorrelationIdentity` (`Func<HttpContext, bool>`).
The default is adaptive:

- **No authentication infrastructure registered** (no `IAuthenticationSchemeProvider`) —
  headers are trusted; there is nothing to check a caller against. This preserves
  behavior on hosts that never wire auth.
- **Authentication registered** — headers are trusted only when
  `User.Identity.IsAuthenticated`.

The correlation filter runs inside the endpoint, after the host's authentication, so both the
default and a custom predicate see the authenticated caller; Kaleido does not authenticate requests
itself. (A host must authenticate before its endpoints, as ASP.NET Core does by default once
authentication is registered; otherwise every caller looks anonymous, and a handoff to a processor
that has not seen the process fails with `pro_process_not_found` because the calling processor is
dropped.)

Override the predicate to apply a custom policy (e.g. service-account-only trust):

```csharp
.AddHttp(o =>
    o.TrustCorrelationIdentity = ctx =>
        ctx.User.IsInRole("internal-service"));
```

### Caller identity

`CallerName`, `CallerId` and `CallerRoles` are **never** read from headers - they come from the
authenticated request principal, stamped by `KaleidoCallerContextEndpointFilter` (inside
endpoint execution, after the correlation filter and the host's authentication) on all Kaleido
endpoint groups:
- `CallerName` - `Identity.Name`;
- `CallerId` - the stable subject id: the `sub` claim, else `ClaimTypes.NameIdentifier`;
- `CallerRoles` - the role claims.

Anonymous requests get `CallerName = null`, `CallerId = null`, `CallerRoles = []`.
`KaleidoCorrelationContext.Caller` (`KaleidoCaller { Name, Id }`) is the caller as Kaleido tags it.

These fields feed capability authorization and the **caller tags** - see
[`AUTHORIZATION.md`](./AUTHORIZATION.md). They are **not** propagated outbound: each hop derives
its own caller identity from its own authenticated principal. Service-to-service calls must carry
credentials the next hop can authenticate (e.g. a bearer token) - the transport's job, not Kaleido's.

### Caller tags (tagging only)

Kaleido stamps `Caller` - exactly as the principal provides it, passed through, not interpreted -
onto what it already produces:
- **every published event**: `ProcessEventContext.Caller`, `QueryableEventContext.Caller`;
- **process state**: `CreatedBy` (the creating request) and `UpdatedBy` (the latest request that
  ran a step);
- **step state** (and the state response's step history): `StartedBy` + `FirstExecuted` (the
  first execution, kept), `LastUpdatedBy` next to `LastExecuted`.

Kaleido **only tags**: no access decisions beyond the declared step and query rules, no "who did
what" tracking, audit queries or history features - downstream systems build those from the tagged
events. The caller is **personal data** in the event stream; it is published unclassified, and a
consumer that must not publish it removes it in an `IKaleidoEventRedactor`.
## Step handlers

Each step handler runs in its own DI scope. Before invoking it, `ProcessorStepInvoker`
seeds that scope's correlation with the request's context plus the process's `ProcessId`
and the executing step (`ExecutingStepName`). Without this, the handler's scoped
correlation would be empty and its outbound calls would lose `RequestId`/`ProcessId`.

## Outbound path (client)

```
step handler / consumer code
  └─ IKaleidoProcessorClient / IKaleidoQueryableClient
       └─ CorrelationHeaderStamper.Stamp(request)
            reads IKaleidoCorrelationContextAccessor.Current
            ├─ forwards RequestId unchanged
            ├─ ProcessId: the IKaleidoProcessForwarding scope's, else the context's
            └─ inside a step only: stamps this service (ServiceName) and
               ExecutingStepName as Calling-Processor / Calling-Step
```

- **A front door whose callers cannot send headers** (the MCP adapter: the process id is a tool
  argument, and the `/mcp` request has no `X-Kaleido-Process-Id`) opens
  `IKaleidoProcessForwarding.ForwardProcess(processId)` around its calls, so queries an agent runs
  are recorded with their process like every step. An explicit `processId` on
  `ExecuteStepAsync` still wins over both.

- Inbound per-hop values are never forwarded: a call made outside a step (router
  forwarding, registry fan-out, a delegated query source) carries no calling processor or step.
- The stamper is scoped (`ICorrelationHeaderStamper`), so the stamped values always
  reflect the correlation context of the request being served — handlers never touch
  headers manually.
- Values are sanitized before stamping, so a caller-supplied context can never smuggle
  invalid header bytes.

## Observability correlation

- The inbound `KaleidoCorrelationEndpointFilter` and the runtime engines tag the `Activity` with
  `kaleido.request.id`, `kaleido.process.id` (when known), `kaleido.processor.instance_id` (own),
  `kaleido.calling.processor` and `kaleido.calling.step`, so distributed traces line up with the
  correlation headers.
- Emitted events carry the correlation (`RequestId`, `ProcessId`, `CallingProcessorName`,
  `CallingStepName`) plus the emitting service's own `ProcessorInstanceId`.

## What stays stable

- `KaleidoCorrelationContext` field names and `X-Kaleido-*` wire names are contract —
  renaming them breaks cross-service correlation silently.
- The single-correlation-signal rule applies: fields are read once at the transport
  boundary and flow inward through the accessor — inner layers never re-read HTTP.
