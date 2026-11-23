# Kaleido.Registry.Http.Server

The central Registry's ASP.NET Core listener adapts [`Kaleido.Registry.Server`](../Kaleido.Registry.Server/README.md) to fixed HTTP endpoints. A Registry host calls `AddKaleido(...).AddServer(...).AddHttp().AddControlRegistry().AddInMemoryRegistryStore()` (or chooses a durable provider), then `AddRegistryHttpServer()` and `MapKaleidoControlRegistry()`. The authority host needs **no Registry client**. `AddKaleidoHttpClientPipeline()` and participant registration do not belong on this host.

| Endpoint | Purpose |
| --- | --- |
| `POST /registry/instances` | Validate and register one service/version/build/instance, return its lease. |
| `PUT /registry/instances/{instanceId}/heartbeat` | Renew the lease and record local health. |
| `DELETE /registry/instances/{instanceId}` | Remove the instance on shutdown. |
| `GET /registry` | Caller-filtered discovery; unfiltered for a host satisfying `ReaderPolicy`. `?strict` returns 502 for a partial result even if `If-None-Match` matches. |
| `GET /registry/capabilities` | Capability descriptions and versioned transport bindings without availability states. |
| `GET /registry/availability` | Small per-version status, current marker, published transports, check interval and capabilities version. |
| `GET /registry/status` | Per-version status and instances for operators/host readers. |

The listener uses generic Kaleido HTTP filters and `KaleidoJsonOptions`, not host-wide JSON configuration. Unfiltered reads expose the authority's semantic snapshot versions as **weak ETags**, with `If-None-Match`/`304` (including weak comparison against a strong request validator). A user-facing filtered `/registry` response gets a validator from its authorized view and authenticated issuer/subject, so two authenticated callers cannot reuse each other's ETag even if they see the same view. A principal without a stable subject receives a fresh validator; anonymous callers have no identity to scope. Responses remain private. This filtered-view computation is HTTP-specific until a transport-neutral authorization projection is owned. In ZeroTrust the caller-filtered Registry omits undeclared steps even for a caller with every declared role, as verified by the direct TestServer suite. Responses remain private to the caller, and a called service enforces its own authorization independently of Registry discovery.

The gateway owns actual destinations. Registration accepts only safe non-null relative routes/bindings under the declared service version (invalid bindings answer `registry_invalid_registration` with 400); no caller supplies an instance address or origin. Direct TestServer tests exercise this listener with an explicit in-memory store **without** a participant client. The SQLite provider is not wired into this checkpoint. The explicit in-memory store is for development and tests; a future durable provider must implement the typed Registry store contract before production use.
