# 0017. Transport contracts live in Kaleido.Http.Abstractions under the Kaleido.Http namespace; the registry snapshot store stays a public async seam

- **Status:** Accepted
- **Date:** 2026-10-19
- **Related:** #298, #293, #275, #356, #313

## Context
The pre-1.0 review (CLN-014, CLN-009) found three placement questions on the HTTP contract boundary:

- The HTTP error body (`KaleidoErrorResponse`, `KaleidoError`) and its shared codes
  (`KaleidoErrorCodes`: `argument_error`, `framework_error`) were declared in transport-free core,
  although core never used them. Their users are the HTTP transport, the HTTP client and the MCP
  adapter.
- `Kaleido.Http.Abstractions` (the wire contracts shared by server and client) uses the root
  namespace `Kaleido.Http`, the same as the server package `Kaleido.Http`.
- `IRegistrySnapshotStore` is a public interface in `Kaleido.Http.Abstractions`. The sources
  disagreed: keep it as an asynchronous extension seam, delete it and internalize the cache, or
  move it. #275 plans a distributed store (`IDistributedCache`) on this interface.

## Decision
1. **HTTP contract types live in `Kaleido.Http.Abstractions`, never in core.** The error body and
   its codes move there (namespace `Kaleido.Http`); the JSON and the code strings are unchanged.
   An architecture test keeps core free of HTTP contract types (no type named `*Response`, no
   `KaleidoError`/`KaleidoErrorCodes`).
2. **`Kaleido.Http.Abstractions` keeps the `Kaleido.Http` namespace** (with `Kaleido.Http.Processor`,
   `Kaleido.Http.Queryable` and `Kaleido.Http.Registry`). The namespace names the transport, not the
   package: a client-only consumer references `Kaleido.Http.Client` (which brings
   `Kaleido.Http.Abstractions`) and sees the contracts under `Kaleido.Http.*` without the server
   package. The client interfaces (`IKaleidoProcessorClient`, `IKaleidoQueryableClient` and their
   factories) live there too, so server-side code and other front doors (the MCP adapter) depend on
   the contract, not on the client implementation.
3. **`IRegistrySnapshotStore` stays public, asynchronous, in `Kaleido.Http.Abstractions`.** It is the
   seam a host replaces to share registry snapshots between replicas (both the server's registry
   cache and the client's registry copies use it; each registers the internal in-memory default with
   `TryAdd`). Every call is awaited with the caller's cancellation token, never blocked on. A store
   failure is the caller's failure; a store that can be unavailable handles that itself (the
   distributed store of #275 falls back to memory).

## Alternatives considered
- **Keep the error body in core.** Core would keep owning an HTTP shape it never produces, and every
  future transport would inherit it. Rejected.
- **A distinct contracts namespace (e.g. `Kaleido.Http.Contracts`).** Renames about 45 public types for
  every consumer and gains nothing: no consumer has both a contract type and a same-named server
  type to disambiguate. Rejected.
- **Delete `IRegistrySnapshotStore` and internalize the cache.** #275 would have to re-add the same
  public seam; removing and redesigning the same seam is two breaking changes for one feature.
  Rejected.
- **Move the store into each package (server and client).** Both caches use one store contract;
  splitting it would mean two interfaces for one extension point. Rejected.

## Consequences
- Code that referenced `Kaleido.KaleidoErrorResponse`, `KaleidoError` or `KaleidoErrorCodes` now uses
  the `Kaleido.Http` namespace (breaking for .NET code, not for the wire). Code inside
  `Kaleido.Http.*` namespaces compiles unchanged.
- The public API baseline (#313) records `IRegistrySnapshotStore` and the moved error types in
  `Kaleido.Http.Abstractions`.
- #275 implements a distributed store on the existing interface without changing it.
