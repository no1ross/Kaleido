# Processed project dependencies

The graph below states actual directly relevant project references in the currently processed/lower areas of #585. An arrow points **from a project to a project it references**; the old Platform/Client/SQLite-dependent projects remain separate migration units:

```text
Kaleido.Registry ──► Kaleido (Core)
Kaleido.Eventing ──► Kaleido
Kaleido.Process ──► Kaleido, Kaleido.Eventing, Kaleido.Registry
Kaleido.Process.Client ──► Kaleido.Process (neutral outbound contracts; no transport or Registry client)
Kaleido.Queryable ──► Kaleido, Kaleido.Eventing, Kaleido.Registry
Kaleido.Queryable.Client ──► Kaleido.Queryable (neutral request/result contracts; no transport)
Kaleido.Process.Server ──► Kaleido.Process, Kaleido
Kaleido.Server ──► Kaleido
Kaleido.Registry.Server ──► Kaleido.Registry, Kaleido.Server
Kaleido.Registry.Client ──► Kaleido.Registry
Kaleido.Http (shared HTTP JSON/headers/errors/route prefix; no project references)
Kaleido.Http.Client (common named-client/caller pipeline) ──► Kaleido, Kaleido.Http
Kaleido.Http.Server ──► Kaleido.Server, Kaleido.Http
Kaleido.Process.Http ──► Kaleido.Process, Kaleido.Registry, Kaleido.Http
Kaleido.Process.Http.Server ──► Kaleido.Process, Kaleido.Process.Server, Kaleido.Process.Http, Kaleido.Http.Server
Kaleido.Process.Http.Client ──► Kaleido.Process.Client, Kaleido.Process.Http, Kaleido.Registry.Client, Kaleido.Http.Client
Kaleido.Queryable.Http ──► Kaleido.Queryable, Kaleido.Http
Kaleido.Queryable.Http.Server ──► Kaleido.Queryable, Kaleido.Queryable.Http, Kaleido.Http.Server
Kaleido.Queryable.Http.Client ──► Kaleido.Queryable.Client, Kaleido.Queryable.Http, Kaleido.Registry.Client, Kaleido.Http.Client
Kaleido.Registry.Http (fixed Registry URL contract; no project references)
Kaleido.Registry.Http.Server ──► Kaleido.Registry, Kaleido.Registry.Server, Kaleido.Registry.Http, Kaleido.Http.Server
Kaleido.Registry.Http.Client ──► Kaleido.Registry.Client, Kaleido.Registry.Http, Kaleido.Http.Client
Kaleido.Hosting ──► Kaleido.Server, Kaleido.Eventing, Kaleido.Process, Kaleido.Queryable, Kaleido
Kaleido.Provider ──► Kaleido.Process.Server, Kaleido.Process, Kaleido.Eventing, Kaleido (store-contract dependency bundle)
Kaleido.Provider.InMemory ──► Kaleido.Provider (explicit in-memory implementations/registration)
Kaleido.Provider.SQLite ──► Kaleido.Provider, Kaleido.Registry.Server (one SQLite provider for all four stores)
```

`Kaleido.Http` owns only shared HTTP wire mechanics; its source project no longer references Platform, Process, Queryable or Registry. Generic `Kaleido.Http.Server` owns ASP.NET body binding, filters, correlation, exception mapping and authorization; it depends only on neutral Server and shared Http (plus transitive Core), not Platform, capability runtimes or the central Registry. Queryable HTTP request envelopes, body conversion and versioned URL helpers now build in `Kaleido.Queryable.Http` without ASP.NET or central Registry. `Kaleido.Queryable.Http.Server` adapts those bodies to locally registered Queryable sources/views over ASP.NET; neither it nor generic Http.Server has a central Registry project reference or requires central participation to serve local routes. `Kaleido.Process.Http` and `Kaleido.Registry.Http` build versioned/fixed URL contracts; `Kaleido.Process.Http.Server` adapts local Process execution/state, authorization and HTTP-owned idempotency over neutral Process.Server admission without central Registry participation. `Kaleido.Process.Client` now owns only transport-neutral outbound invocation/factory contracts over generic Process; `Kaleido.Process.Http.Client` now owns published Process binding selection from the hydrated Registry, neutral-to-HTTP conversion, caller/per-hop header stamping and only keyed transient POST retries (#622); the neutral client remains independent of it. `Kaleido.Process.Server` owns transport-neutral idempotency admission/replay and references generic Process and foundation, never HTTP. `Kaleido` must not reference either capability or Eventing. Eventing provides generic envelopes, outbox/writer/relay/redaction seams and does not reference Process or Queryable; each capability owns its own payloads, stable discriminators and typed dispatch registration. Both capabilities reference foundation for identity/CLR metadata/authorization declarations and the Core-only Registry contracts for portable Process/Queryable publication; neither depends on the central authority or participant client. HTTP capability adapters contribute only callable binding data, and shared `Kaleido.Http.Client` has no dynamic Process/Queryable route-selection implementation. The temporary Registry.Next shell was removed by owner decision (#586, superseding #595). The Eventing placement and its compatibility `<RootNamespace>Kaleido</RootNamespace>` are **temporary shallow-extraction choices**, not the final package design (#592). Moving a single Eventing assembly above Process and Queryable without moving its generic contracts would create a cycle; revisit that design in #592 rather than introducing one now.

Foundation owns no store choice, contributor or in-memory default. `Kaleido.Provider` bundles the lower store-contract dependencies; `Kaleido.Provider.InMemory` implements Process state, Process.Server idempotency and Eventing outbox as explicit singleton choices (#559). The SQLite package implements the same stores durably and Registry.Server's separate store (#623). Different providers for one store conflict in either registration order (ADR 0049); no lower capability references a provider. Hosting checks **presence** of required registrations, not implementation technology (#597). These owners reference lower projects; foundation references none of them. Cross-assembly internal seams use `.csproj` `<InternalsVisibleTo>` items, and project-wide usings use `.csproj` `<Using>` items.

## Legacy edges awaiting migration

These lower-layer edges were described in the old root/source architecture sketches. They are retained here so deleting duplicate sketches does **not** discard information; this is not a fresh audit or a claim that these project boundaries are final. Each project must verify and revise its own row when reached:

| Unmigrated project | Previously documented lower references |
| --- | --- |
| `Kaleido.Platform` | `Kaleido` |
| Old `src/Client/Kaleido.Client.csproj` | `Kaleido.Platform` (remote Process/Queryable client interfaces still await migration; shared HTTP call context moved separately) |
| `Kaleido.Mcp` | `Kaleido.Http.Server`, `Kaleido.Http.Client` |
| `Kaleido.Observability.OpenTelemetry` | `Kaleido` |

The other projects have **not** completed their migration. Their actual `ProjectReference` declarations, not this old sketch or a proposed diagram, are authoritative until they are processed. The deleted assembly architecture tests leave a temporary enforcement gap: #591 must replace them with build-time source analyzers before epic completion; do not recreate an architecture-test suite or relax KAL fixture rules.
