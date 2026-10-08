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
HTTP request
  └─ ObservabilityMiddleware        (registered by KaleidoStartupFilter via AddHttp())
       └─ HttpCorrelationContextReader
            reads + sanitizes RequestId, ProcessId, Calling-Processor, Calling-Step
            └─ writes KaleidoCorrelationContext into
               IKaleidoCorrelationContextAccessor (scoped)
       ├─ tags the current Activity (this service's own instance id + the fields above)
       └─ echoes RequestId and ProcessId on the response
```

- String values pass through `HttpHeaderSanitizer` (RFC 7230 — printable ASCII, max 256
  chars); unsanitizable content is dropped.
- `ProcessId` must parse as a GUID. A malformed value throws `BadHttpRequestException`,
  which `ExceptionMiddleware` maps to **HTTP 400** (`argument_error`) — see
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

Override the predicate to apply a custom policy (e.g. service-account-only trust):

```csharp
.AddHttp(o =>
    o.TrustCorrelationIdentity = ctx =>
        ctx.User.IsInRole("internal-service"));
```

### Caller identity

`CallerName` and `CallerRoles` are **never** read from headers — they come from the
authenticated request principal. The `ObservabilityMiddleware` runs before the host's
`UseAuthentication`, so they are stamped later by `KaleidoCallerContextEndpointFilter`
(runs inside endpoint execution, always after auth middleware) on all Kaleido endpoint
groups. Anonymous requests get `CallerName = null`, `CallerRoles = []`.

These fields feed process ownership (`ProcessorContext.Owner`/`OwnerRoles`) and
capability authorization — see [`AUTHORIZATION.md`](./AUTHORIZATION.md). They are
**not** propagated outbound: each hop derives its own caller identity from its own
authenticated principal. Service-to-service calls must carry credentials the next hop
can authenticate (e.g. a bearer token) — the transport's job, not Kaleido's.

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
            ├─ forwards RequestId and ProcessId unchanged
            └─ inside a step only: stamps this service (ServiceName) and
               ExecutingStepName as Calling-Processor / Calling-Step
```

- Inbound per-hop values are never forwarded: a call made outside a step (router
  forwarding, registry fan-out, a delegated query source) carries no calling processor or step.
- The stamper is scoped (`ICorrelationHeaderStamper`), so the stamped values always
  reflect the correlation context of the request being served — handlers never touch
  headers manually.
- Values are sanitized before stamping, so a caller-supplied context can never smuggle
  invalid header bytes.

## Observability correlation

- The inbound `ObservabilityMiddleware` and the runtime engines tag the `Activity` with
  `kaleido.request.id`, `kaleido.processor.instance_id` (own), `kaleido.calling.processor`
  and `kaleido.calling.step`, so distributed traces line up with the correlation headers.
- Emitted events carry the correlation (`RequestId`, `ProcessId`, `CallingProcessorName`,
  `CallingStepName`) plus the emitting service's own `ProcessorInstanceId`.

## What stays stable

- `KaleidoCorrelationContext` field names and `X-Kaleido-*` wire names are contract —
  renaming them breaks cross-service correlation silently.
- The single-correlation-signal rule applies: fields are read once at the transport
  boundary and flow inward through the accessor — inner layers never re-read HTTP.
