# Correlation context flow

Kaleido propagates a small correlation context across service hops so a single logical
operation can be traced through every service it touches — in logs, traces, and emitted
process events.

The context itself (`KaleidoCorrelationContext`) is **transport-agnostic** and lives in
`Kaleido` core. The HTTP wire names (`KaleidoCorrelationHeaders`) live in
`Kaleido.Http.Abstractions`. A future transport (e.g. gRPC) gets its own header/metadata
constants in its own project — the context shape does not change.

## Fields and headers

| Context field | HTTP header | Type | Purpose |
|---|---|---|---|
| `RequestId` | `X-Kaleido-Request-Id` | string | Unique ID of the originating request. Generated (new GUID) when the caller does not supply one — always non-empty after the inbound read. |
| `ProcessId` | `X-Kaleido-Process-Id` | GUID | The active process instance this call belongs to. |
| `ProcessorInstanceId` | `X-Kaleido-Processor-Instance-Id` | GUID | The running instance of the processor that handled the request (`KaleidoServiceOptions.InstanceId`). |
| `SourceProcessorName` | `X-Kaleido-Source-Processor` | string | Service name of the processor that originated the call. |
| `StepName` | `X-Kaleido-Step-Name` | string | The process step making the inter-service call — lets a queryable service see which step asked. |
| `CallerName` | *(none)* | string | Authenticated caller name — populated from the request principal after authentication, never from headers. |
| `CallerRoles` | *(none)* | string[] | Authenticated caller roles — populated alongside `CallerName`. |

## Inbound path (server)

```
HTTP request
  └─ ObservabilityMiddleware        (registered by KaleidoStartupFilter via AddHttp())
       └─ HttpCorrelationContextReader
            reads + sanitizes the five X-Kaleido-* headers
            └─ writes KaleidoCorrelationContext into
               IKaleidoCorrelationContextAccessor (scoped)
       ├─ tags the current Activity with the correlation fields
       └─ echoes the correlation headers on the response
```

- String values pass through `HttpHeaderSanitizer` (RFC 7230 — printable ASCII, max 256
  chars); unsanitizable content is dropped.
- `ProcessId` / `ProcessorInstanceId` must parse as GUIDs. A malformed GUID header throws
  `BadHttpRequestException`, which `ExceptionMiddleware` maps to **HTTP 400**
  (`argument_error`) — see [`ERROR_CODES.md`](./ERROR_CODES.md).
- A missing `X-Kaleido-Request-Id` is replaced with a fresh GUID, so every request has an
  ID downstream. `KaleidoCorrelationContext.IsEmpty` reports whether any meaningful field
  was supplied.

### Header trust

Identity-bearing headers (`RequestId`, `SourceProcessor`, `StepName`,
`ProcessorInstanceId`) are honored only for trusted callers — untrusted callers get a
fresh `RequestId` and the remaining identity fields are dropped (the response echo still
reflects the resolved context, so callers see exactly what was used). `ProcessId` is
always honored: it is a resumable process handle, not an identity claim.

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

## Outbound path (client)

```
step handler / consumer code
  └─ IKaleidoProcessorClient / IKaleidoQueryableClient
       └─ CorrelationHeaderStamper.Stamp(request)
            reads IKaleidoCorrelationContextAccessor.Current
            └─ stamps the five X-Kaleido-* headers on every
               outbound HttpRequestMessage (registry fetch included)
```

- The stamper is scoped (`ICorrelationHeaderStamper`), so the stamped values always
  reflect the correlation context of the request being served — correlation flows
  hop-by-hop automatically; handlers never touch headers manually.
- Values are sanitized before stamping, so a caller-supplied context can never smuggle
  invalid header bytes.

## Observability correlation

- The inbound `ObservabilityMiddleware` and the runtime engines tag the `Activity` with
  the same fields, so distributed traces line up with the correlation headers.
- Emitted process events carry the correlation context, making every step event joinable
  to its request and process IDs.

## What stays stable

- `KaleidoCorrelationContext` field names and `X-Kaleido-*` wire names are contract —
  renaming them breaks cross-service correlation silently.
- The single-correlation-signal rule applies: fields are read once at the transport
  boundary and flow inward through the accessor — inner layers never re-read HTTP.
