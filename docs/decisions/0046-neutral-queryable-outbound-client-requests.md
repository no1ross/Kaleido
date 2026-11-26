# 0046. Queryable outbound client requests stay transport-neutral

- **Status:** Accepted
- **Date:** 2026-11-16
- **Related:** #585, #615, #616, #577, #530, ADR 0040, ADR 0041, ADR 0045

## Context

The old combined client exposed `IKaleidoQueryableClient` with HTTP `QueryApiRequest` parameters, originally from Platform and now owned by `Kaleido.Queryable.Http`. Moving that unchanged into the proposed neutral `Kaleido.Queryable.Client` would introduce a neutral-client → HTTP wire reference. Generic Queryable already owns `QueryRequest`, `QueryRequest<TParameters>`, `QueryBody` and `QueryResult<T>` for its local/delegated query contract. The now explicit Registry participant hydrates an unfiltered global Registry in memory; a remote client should resolve a published Queryable binding from that copy rather than the local catalog or an invented route prefix.

## Decision

`Kaleido.Queryable.Client` owns the public outbound `IKaleidoQueryableClient` and service-name/version-pinned factory contracts. Their source/view query methods take generic Queryable's neutral `QueryRequest` / `QueryRequest<TParameters>` and return `QueryResult<T>`; they do not depend on HTTP, Registry.Server, a local catalog or Platform. This is an explicit preview API change, advancing the typed/neutral request direction previously deferred in #530. The neutral contracts package does not install a client or perform a network call.

The subsequent `Kaleido.Queryable.Http.Client` owner converts neutral requests to `QueryApiRequest` through the existing `Queryable.Http` CLR-to-wire conversion. Its factory resolves the requested service's current or pinned version and the source/view's declared `query` HTTP operation from the participant's **hydrated unfiltered Registry**, via Registry.Client's internal target resolver. It validates the published relative path beneath the selected version's gateway route before sending through the shared named `IHttpClientFactory` credential/correlation pipeline. Neither local `AddQueryable()` nor client creation reads a central Registry per query; a host opts into Registry hydration separately.

## Alternatives considered

- **Keep `QueryApiRequest` on the neutral interface.** Rejected: the transport-neutral package would depend upward on HTTP and force future gRPC callers to construct an HTTP JSON body.
- **Expose only an HTTP-specific public interface for now.** Rejected for the chosen #585 boundary: it defers the already-existing neutral request/result contract and leaves the proposed neutral client owner empty. The earlier post-1.0 timing in #530 is superseded for these preview signatures; its unrelated EF executor recipe remains separate.
- **Build URLs from service name or `RoutePrefix`, or read the authority on every query.** Rejected by #577/ADR 0040: the host's unfiltered hydrated Registry already has the version and transport operation path.

## Consequences

Existing pre-release callers of the old HTTP-wire interface must pass `QueryRequest` (and typed parameters) and update the package/namespace after the HTTP adapter moves; the HTTP wire request stays an adapter-only representation. Source and view names remain strings, and local runtime registration/execution are unaffected. Queryable.Http.Client, Process clients, MCP and guide/sample migrations retain their own #585 gates; do not create a no-op Queryable.Client implementation or imply that a gRPC transport already exists.
