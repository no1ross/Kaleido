# Kaleido.Registry

`Kaleido.Registry` owns only the transport-neutral **contracts** for publishing and reading Registry capabilities. It depends on the [`Kaleido` foundation](../../../Core/Kaleido/README.md), not on Process, Queryable, ASP.NET, a Registry client or the central authority. Its models contain no CLR execution `Type` or runtime object.

A service publishes `ProcessRegistryItem` and/or `QueryableRegistryItem` descriptions with shared `DataTypeMetadata`, constraints and authorization metadata. Generic Process and Queryable project their own private catalogs into these items once. Public enum-like values (such as filter operators, match modes and availability) are stable machine-readable strings. An installed transport contributes **only its own** nested `RegistryTransportBindings` operations; an HTTP binding contains its method and versioned logical gateway path, never an origin or instance address. Future gRPC bindings belong in the same structured contract when a real adapter exists.

The `RegistryInstanceRegistration` sent by an instance distinguishes:

- `ServiceName + ServiceVersion`: the service's logical route and durable Process-state identity.
- `BuildVersion`: the deployed **service application's** Product/assembly informational version before `+commit` metadata, such as `1.0.0-preview.616`. A new build may publish changed compatible metadata without moving running processes to a new route. It is not `Kaleido.dll`'s version or a four-part FileVersion.
- `InstanceId`: the running instance whose health and lease the authority tracks.

`KaleidoRegistry`, `KaleidoRegistryAvailability` and `KaleidoRegistryStatus` are read models. Their `Revision` values are opaque semantic snapshot versions supplied by the authority; HTTP exposes them as **weak ETags**, not JSON/storage hashes. The Registry store, leases, cache, snapshot version tracking and health belong to [`Kaleido.Registry.Server`](../Kaleido.Registry.Server/README.md). ASP.NET endpoints and caller-filtered discovery belong to [`Kaleido.Registry.Http.Server`](../Kaleido.Registry.Http.Server/README.md). Explicit participation and fixed HTTP calls belong to [`Kaleido.Registry.Client`](../Kaleido.Registry.Client/README.md) and [`Kaleido.Registry.Http.Client`](../Kaleido.Registry.Http.Client/README.md).

The internal Registry-source, local-publication and binding-contribution interfaces let framework projects cooperate through `InternalsVisibleTo`; they are not an application-facing execution API. Local Process/Queryable execution does not need Registry participation.
