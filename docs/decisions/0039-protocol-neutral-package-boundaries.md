# 0039. Package boundaries separate the platform from protocol adapters

- **Status:** Accepted
- **Date:** 2026-11-09
- **Related:** #581, #577, #553, ADR 0017

## Context

The historical HTTP packages combined the runtime, shared contracts, registration, clients and
ASP.NET endpoints. Registry participation lived in the HTTP client even though other transports and
hosts need it. `Kaleido.Http.Abstractions` also held runtime-independent contracts, a shared cache
extension point and transport behavior in one package. A gRPC adapter, a console host and a consumer
that needs only the model could not depend on the appropriate pieces independently. The registry
snapshot store has since been replaced by the central registry's store and HybridCache (ADR 0040).

## Decision

Separate dependencies by responsibility; namespaces follow their owning packages:

- `Kaleido` remains the transport-free Process and Queryable engine and its store contracts.
- `Kaleido.Platform` (on `Kaleido`) owns the shared registry, Process and Queryable contracts, JSON
  conventions and HTTP header names that the server and clients exchange. These published shapes are
  supported contracts, including the core types carried as they are (the ADR 0017 amendment).
- `Kaleido.Client` (on `Kaleido.Platform`) owns neutral client interfaces, call context, registration,
  heartbeats, the hydrated central-registry copy, readiness and transport selection. It has no ASP.NET
  Core dependency. The installed protocol adapters implement the actual calls.
- `Kaleido.Server` (on `Kaleido.Platform`) owns the shared ASP.NET Core caller, authorization,
  correlation, filters, error handling and registry filtering.
- `Kaleido.Http` (on `Kaleido.Platform`) holds the shared HTTP route and URL vocabulary, not endpoint
  mapping. `Kaleido.Http.Server` (on `Kaleido.Server` and `Kaleido.Http`) maps the service's HTTP
  capabilities. `Kaleido.Http.Client` (on `Kaleido.Client` and `Kaleido.Http`) performs HTTP calls.
- `Kaleido.Registry` (on `Kaleido.Server`) owns the authoritative control registry and its store;
  `Kaleido.Registry.Http` (on it and `Kaleido.Http.Server`) maps its HTTP API. `Kaleido.Mcp` (on
  `Kaleido.Http.Server` and `Kaleido.Http.Client`) adapts registry capabilities to MCP tools.
- `Kaleido.Provider.SQLite` implements the core and registry store contracts; the OpenTelemetry
  provider references core only.

`Kaleido.Grpc`, `Kaleido.Grpc.Server`, `Kaleido.Grpc.Client` and `Kaleido.Registry.Grpc` reserve
those same boundaries but contain no gRPC implementation, packages or tests yet. They are in the
solution and are not packable. Hosts choose the protocols they offer; libraries depend only on the
bases they need. Tests mirror this layout.

## Alternatives considered

- **Retain the old packages as facades.** That keeps duplicate ownership and leaves registration and
  shared contracts tied to HTTP. Rejected for preview packages; this is a deliberate breaking move.
- **Move every shared type into core.** It would make the engine own wire and ASP.NET concerns. Rejected.
- **Create a dependency from one protocol adapter to another.** Mixed-protocol hosts need independent
  implementations. Rejected; an adapter depends on the appropriate base and its own wire project.

## Consequences

Consumers update package references, namespaces, DI extensions and public API references together.
`Kaleido.Http.Abstractions`, the old server `Kaleido.Http`, `Kaleido.Http.Mcp` and
`Kaleido.Http.Registry` package roles are replaced by the projects above. The public registry
snapshot seam from ADR 0017 does not survive: there is one registry, and the cache is its own
implementation detail (ADR 0040). JSON compatibility and shared core-shape guarantees continue
at the `Kaleido.Platform` boundary. This record supersedes ADR 0017's placement and snapshot
store decisions without rewriting that historical record.
