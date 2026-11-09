# Kaleido Architecture

This is the top-level map of the current framework. Project READMEs name the types each project
owns; [`src/ARCHITECTURE.md`](./src/ARCHITECTURE.md) describes the implementation. Kaleido is a
metadata-driven Process and Queryable engine whose capabilities are published by one control
registry and reached through a gateway.

## 1. Architectural overview

Sixteen source projects occupy four layers (twelve shipped packages and four non-packable gRPC
placeholders):

| Layer | Projects | Responsibility |
| --- | --- | --- |
| Runtime | [`Kaleido`](./src/Kaleido/README.md) | Transport-free Process and Queryable runtimes, events and store contracts |
| Shared bases | [`Kaleido.Platform`](./src/Platform/README.md), [`Kaleido.Server`](./src/Server/README.md), [`Kaleido.Client`](./src/Client/README.md) | Published contracts; inbound authorization/correlation; neutral clients and central-registry participation |
| HTTP | [`Kaleido.Http`](./src/Http/Http/README.md), [`Kaleido.Http.Server`](./src/Http/Http.Server/README.md), [`Kaleido.Http.Client`](./src/Http/Http.Client/README.md) | Shared HTTP route shapes, service endpoints, and the one outbound gateway client |
| Registry and MCP | [`Kaleido.Registry`](./src/Registry/Registry/README.md), [`Kaleido.Registry.Http`](./src/Registry/Registry.Http/README.md), [`Kaleido.Mcp`](./src/Mcp/Mcp/README.md) | Authoritative store-backed registry, its HTTP API, and caller-visible MCP tools |
| Providers | [`Kaleido.Provider.SQLite`](./src/Providers/Provider.SQLite/README.md), [`Kaleido.Observability.OpenTelemetry`](./src/Observability/Observability.OpenTelemetry/README.md) | Development/reference persistence and opt-in telemetry export |
| Future wire adapters | [`Kaleido.Grpc`](./src/Grpc/Grpc/README.md), [`Kaleido.Grpc.Server`](./src/Grpc/Grpc.Server/README.md), [`Kaleido.Grpc.Client`](./src/Grpc/Grpc.Client/README.md), [`Kaleido.Registry.Grpc`](./src/Registry/Registry.Grpc/README.md) | Project placeholders only; gRPC is not implemented or packed yet |

Dependencies point from adapters to bases, never from one protocol to another:

```text
Kaleido ─► Kaleido.Platform ─► Kaleido.Server, Kaleido.Client, Kaleido.Http
Kaleido.Server + Kaleido.Http ─► Kaleido.Http.Server
Kaleido.Client + Kaleido.Http ─► Kaleido.Http.Client
Kaleido.Server ─► Kaleido.Registry
Kaleido.Registry + Kaleido.Http.Server ─► Kaleido.Registry.Http
Kaleido.Http.Server + Kaleido.Http.Client ─► Kaleido.Mcp
Kaleido + Kaleido.Registry ─► Kaleido.Provider.SQLite
Kaleido ─► Kaleido.Observability.OpenTelemetry
```

The arrows in this sketch mean "is referenced by"; actual `ProjectReference` edges are listed in
the projects. The gRPC placeholders mirror the HTTP family without an implementation ([ADR 0039](./docs/decisions/0039-protocol-neutral-package-boundaries.md)).

## 2. Top-level design principles

### A process engine, never a decision maker

Kaleido applies declared relationships, validates inputs and records outcomes; the implementer
owns business policy, questions, access and wording ([ADR 0001](./docs/decisions/0001-process-engine-not-decision-maker.md)).

### Metadata first

Steps, sources and views are described by registrations and projected into registry contracts, not
by per-caller guesses about another service's endpoints.

### Explicit registration

Assemblies, durable stores, transports, the gateway and central-registry participation are chosen
by the host. There is no per-service registry or network fan-out.

### Strongly typed internals

Process and Queryable runtime planning uses CLR types and internal contracts; a wire adapter
converts requests and projects the response.

### Thin transport layers

`Kaleido.Http.Server` and `Kaleido.Http.Client` adapt the runtime and neutral client interfaces;
they do not implement business rules or introduce a dependency from the core onto ASP.NET Core.

### Clear project boundaries

`Kaleido.Platform` contains published contracts shared across transports; `Kaleido.Server` and
`Kaleido.Client` own inbound and outbound mechanisms. Protocol projects supply only their wire
shapes and adapters. The registry owns state and cache, not destination addresses.

## 3. Repository structure

### Root-level docs

- [`README.md`](./README.md) — overview, quick start and packages
- [`AGENTS.md`](./AGENTS.md) — contributor and per-commit gate rules
- [`src/ARCHITECTURE.md`](./src/ARCHITECTURE.md) — source-level internals
- [`docs/decisions`](./docs/decisions/README.md) — decisions and their supersession

### Source projects

The project links and ownership table in [Architectural overview](#1-architectural-overview)
are authoritative. A project README describes its own public entry points, not another project's.

### Tests

[`tests/AGENTS.md`](./tests/AGENTS.md) maps the 18 test projects to their source projects. The
functional suites run service, registry and MCP hosts separately where the boundary matters.
The consumer guide's [compiled examples](./docs/guide/examples) are exercised by
`Kaleido.IntegrationTests`; the external samples have their own release cycle.

### Examples and samples

- [Consumer guide](./docs/guide/README.md) — compiled orders-service examples and integration tests
- [Prior authorization sample](https://github.com/no1ross/Kaleido-Samples-PriorAuth) and
  [e-commerce sample](https://github.com/no1ross/Kaleido-Samples-ECommerce) — separate repositories

## 4. Registration model

1. `AddKaleido(configuration, options)` binds configuration over explicit service options, validates
   the assembly list and registers the core Process and Queryable runtimes. Choose in-memory stores
   deliberately for local work or register durable store implementations.
2. An HTTP service calls `AddHttp()` and `AddControlRegistryClient()` with `Kaleido:BaseUrl` set to
   the gateway and a `ServiceVersion`. `MapKaleidoHttp()` maps only that host's Process and Queryable
   endpoints at `/{service}/{version}/...`. There is no unversioned alias or service registry route.
3. `AddHttpClients()` registers one HTTP client at the gateway; its Process and Queryable factories
   resolve a published service name and optional version from the host's unfiltered hydrated
   central-registry copy. `AddMcp()` also requires these services, `AddHttp()` and exchanged caller
   credentials. `MapKaleidoMcp(pattern)` maps its separate MCP endpoint.
4. The registry host calls `AddControlRegistry()` with a registry store and maps
   `MapKaleidoControlRegistry()`. It does not need a participant client. Each service registers,
   heartbeats and deregisters against its fixed `/registry` API.

## 5. Metadata and discoverability

The central `GET /registry` describes Process and Queryable capabilities, their service versions,
logical gateway routes and status. Reads for ordinary callers are filtered by published
capability authorization; each participating host maintains a full unfiltered copy for internal
resolution. Availability and revisioned capabilities are fetched conditionally at the registry's
pace; the store is authoritative. The registry uses HybridCache for its merged view and immutable
capability revisions, with an optional distributed tier. No registry entry exposes an instance
address; the gateway's deployment configuration maps each versioned public route to its host
([ADR 0040](./docs/decisions/0040-central-registry-only-and-versioned-routes.md)).

## 6. Transport model

`Kaleido.Platform` holds the supported wire contracts and shared JSON/header conventions.
`Kaleido.Http` supplies route shapes; `Kaleido.Http.Server` publishes them, and
`Kaleido.Http.Client` sends registry-published relative URLs beneath the matching logical gateway
route. The neutral `Kaleido.Client` selector models gRPC preference and safe fallback when a
gRPC adapter exists, but the shipped client supports HTTP only: it does not invoke fallback yet.
The gRPC wire implementation remains future work.

## 7. Contributor guidance

Keep runtime decisions in `Kaleido`, registry ownership in `Kaleido.Registry`, and wire adaptation
in the relevant protocol project. Changes to `Kaleido.Platform`'s published shapes affect both
server and client. Follow the [per-commit gate](./AGENTS.md#per-commit-quality-gate), including
local Markdown links and heading fragments.

## 8. Where to look next

- [Source architecture](./src/ARCHITECTURE.md) and [project boundaries](./AGENTS.md#project-boundaries)
- [Registry-only decision](./docs/decisions/0040-central-registry-only-and-versioned-routes.md)
- [Package boundary decision](./docs/decisions/0039-protocol-neutral-package-boundaries.md)
- [Consumer guide](./docs/guide/README.md) for tested usage
