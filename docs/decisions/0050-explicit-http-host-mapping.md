# 0050. HTTP host composition maps explicit capabilities or the central Registry

- **Status:** Accepted; supersedes only ADR 0040's `MapKaleidoHttp()`-requires-participation clause
- **Date:** 2026-11-19
- **Related:** #585, #624, #610, #611, #609, #618, #582; ADR 0040, ADR 0047

## Context

The old combined `MapKaleidoHttp()` automatically found local Process/Queryable catalogs and conflated serving local endpoints with joining the central Registry. The #585 project split gives each capability HTTP server a public local mapper and gives the Registry authority a separate listener. A service that must be discovered by other services still publishes to, and hydrates from, the one central Registry through an explicit participant registration. A control Registry host has an authority store but does not register itself as its own participant. Requiring a Registry participant before *any* combined HTTP mapping prevents the authority host from using the same composition entry point and hides which endpoints a host chose to expose.

## Decision

1. `Kaleido.Hosting.Http` offers `MapKaleido(this IEndpointRouteBuilder)` returning `IKaleidoHttpEndpointBuilder : IEndpointConventionBuilder`. The root maps **nothing** by itself. Extension methods `MapProcess()`, `MapQueryable()` and `MapCentralRegistry()` on that interface call the existing `MapProcessor()`, `MapQueryable()` and `MapKaleidoControlRegistry()` adapters, respectively, returning the same composition root rather than nesting one capability's routes under another. A host can still call any individual mapper directly. Host endpoint conventions applied before or after a selection reach only selected Kaleido endpoint groups, not unrelated ASP.NET routes. A capability/authority cannot be mapped twice through one root, and a Registry-authority role is not mixed with local service capabilities on that root.
2. Each capability server remains the only owner of its route bindings, typed input, ZeroTrust authorization declarations, errors and execution. The Registry authority owns its fixed API and reader/registrant policies. Hosting.Http only checks the HTTP service's shared authentication-scheme prerequisite and reports when authorization was explicitly disabled; it does not read internal catalogs, replicate capability rules, install a store or register a client.
3. Distributed **routing and discovery** still require the one authoritative central Registry. A publishing or outbound-calling service explicitly opts into `AddRegistryHttpParticipation()` and contributes the HTTP bindings of installed capabilities; it uses the hydrated unfiltered copy to select remote routes. Mapping a local route alone does **not** register it centrally or make it remotely discoverable. An authority host maps `MapCentralRegistry()` against its chosen authority store, without becoming a Registry participant. A local-only service can execute its own registered steps/queries without asking the central authority, as ADR 0047 already allowed.

## Alternatives considered

- Retain auto-detecting combined mapping: it hides capabilities, mixes authority/participant roles, and the old convention builder is no longer a valid ASP.NET builder.
- Require central participation inside `MapKaleido()`: the authority would have to become its own client, while local serving would trigger background networking just to map its own routes.
- Make the composition reimplement Process/Queryable authorization, validation or endpoint logic: the capability adapters already own those decisions; a second policy path can drift.
- Defer the composition to a second PR: the parked mapper and its tests would remain a known broken owner in the single #585 migration work package.

## Consequences

This is a pre-1.0 host API change: callers must name the endpoints they map. The old `MapKaleidoHttp()` no-argument auto-map behavior is gone; consumers explicitly choose `app.MapKaleido().MapQueryable().MapProcess()` or `app.MapKaleido().MapCentralRegistry()`, while the three existing direct owner mappers remain. ADR 0040's central source of truth, versioned published routes, last-good hydrated copy and gateway destination rules are unchanged; only its sentence requiring Registry participation in the combined mapper is superseded. No gRPC mapping is pretended to work and no PR or release readiness is claimed by this owner checkpoint.
