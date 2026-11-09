# 0040. One authoritative registry publishes versioned logical routes

- **Status:** Accepted
- **Date:** 2026-11-09
- **Related:** #581, #577, #553, ADR 0037, ADR 0039

## Context

ADR 0037 introduced a standalone control registry, but the old service registries, client reads,
fan-out, per-service client addresses and host snapshot stores remained. A host could describe
capabilities fetched from a different collection of services than another host, and clients still
constructed capability URLs from `RoutePrefix`. A caller-filtered registry cannot safely be cached
once and reused to route calls for another caller. Versions also need distinct public paths: an
unversioned route to "current" could move an in-flight process to a host without its state.

## Decision

1. **One source of truth.** A service pushes its version, capabilities, transport bindings and local
   health to the control registry, renews its lease and deregisters. The registry's store is
   authoritative. It never polls services. The per-service `/{service}/registry` route, clients'
   registry reads, fan-out, client health probes and distributed host snapshots are removed.
2. **One gateway address.** Participating hosts configure `Kaleido:BaseUrl`, use the known `/registry`
   API to register and hydrate an unfiltered in-memory copy, and follow the registry's availability
   interval and conditional capability revision. `MapKaleidoHttp()` requires participation. The
   registry host itself needs no client. `AddHttpClients()` uses the same gateway address for one
   outbound HTTP client, not a `Kaleido:Clients` map of internal service addresses. MCP still requires
   the registry client, HTTP clients and an exchanged caller token.
3. **Registry-published paths.** Each service version declares its supported transports (`http` by
   default, later `grpc`) and each transport's public logical gateway path. The HTTP service maps
   only `/{service}/{version}/processes/...` and `/{service}/{version}/queryable/...`; no unversioned
   aliases. Processor-level execute/state-template and per-step/source/view URLs are published with
   the version. A client resolves the current version for new work, or an explicit version for a
   continuing process, from its hydrated **unfiltered** registry and sends the published URL. MCP
   tools and protected answer/form continuations retain their version. The caller-facing registry
   remains authorization-filtered for discovery; the called service enforces authorization.
4. **The gateway owns destinations.** Registrations contain logical relative paths, never origins or
   instance addresses. The gateway is deployed with each version and maps that version's route to
   its own host or pool; a host does not serve two versions. Registry registration rejects unsafe
   paths, and the HTTP client validates a published path beneath that version's route before
   sending credentials. Route metadata is part of a revision-keyed registration: a changed binding
   makes a new capabilities revision. During a rolling deployment only transports every live
   revision supports are advertised, using the oldest revision's binding until it leaves.
5. **Cache at the authority.** The control registry uses HybridCache for the short-lived merged view
   and revision-keyed capabilities. `Kaleido:Registry:Cache` configures the key prefix and separate
   overall/local lifetimes; `IDistributedCache` is optional. A cache miss or distributed-cache
   failure reads the store rather than becoming another source of truth. Hosts keep their own
   hydrated last-good copy and perform no downstream probes.

The neutral client selector defines a future gRPC preference when an adapter and binding are both
available, then HTTP. Cross-protocol fallback is permitted only before sending anything, for a read,
or when the request has an idempotency key. The selection and fallback policy have unit tests, but
the current HTTP-only client neither offers a gRPC adapter nor invokes the fallback path. The gRPC
wire implementation and its client integration are separate work.

## Alternatives considered

- **Keep service registries and aggregate them in each host.** Requires every address and gives each
  host a different answer. Rejected.
- **Store instance addresses or let registrants route traffic.** A registration could redirect
  credential-bearing calls; deployment already owns gateway mapping. Rejected.
- **Construct URLs from a configured route prefix, or route ongoing work to "current".** The first
  ignores published capabilities; the second can send a process to the wrong version. Rejected.
- **Share host snapshots through a public snapshot-store extension.** It duplicates an authority
  outside the control registry. Rejected in favor of the registry's store and HybridCache.

## Consequences

Every HTTP service supplies `ServiceVersion`, maps only versioned routes and needs central-registry
participation. Each deployment configures the gateway's mapping for its version. Consumers remove
`Kaleido:Clients`, `RoutePrefix`, per-service registry URLs, host fan-out/snapshot registration and
client health probes. HTTP client factories now take a published service name and optionally its
version. An old-version process needs its version to be supplied explicitly; process IDs alone do
not identify a version. Registry hosts may attach a distributed cache, but hosts that consume the
registry cannot configure the authority's cache. This record supersedes ADR 0037's earlier
implementation phases while retaining its push, trust, single-registry and gateway-boundary rules.
