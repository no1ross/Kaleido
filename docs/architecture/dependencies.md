# Processed project dependencies

The graph below states actual, directly relevant references for the projects already processed under #585. An arrow points **from a project to a project it references**:

```text
Kaleido.Process   ──► Kaleido.Eventing ──► Kaleido
                ╰────────────────────────► Kaleido
Kaleido.Queryable ──► Kaleido.Eventing ──► Kaleido
                ╰────────────────────────► Kaleido
```

`Kaleido` must not reference either capability or Eventing. Eventing provides generic envelopes, outbox/writer/relay/redaction seams and does not reference Process or Queryable; each capability owns its own payloads, stable discriminators and typed dispatch registration. Both capabilities currently reference foundation directly for identity, metadata and correlation. The Eventing placement and compatibility `<RootNamespace>Kaleido</RootNamespace>` are **temporary shallow-extraction choices**, not the final package design (#592). Moving a single Eventing assembly above Process and Queryable without moving its generic contracts would create a cycle; revisit that design in #592 rather than introducing one now.

Foundation owns a generic contributor-based in-memory store-choice check; Process contributes state/idempotency only when used, Eventing contributes the outbox when registered. This prevents foundation from knowing concrete upper-layer store types. `AddInMemoryStores()` remains a shared transitional choice until provider ownership is processed. Cross-assembly internal seams are granted by explicit `.csproj` `<InternalsVisibleTo>` items; project-wide usings live in `.csproj` `<Using>` items, not `GlobalUsings.cs`.

## Legacy edges awaiting migration

These lower-layer edges were described in the old root/source architecture sketches. They are retained here so deleting duplicate sketches does **not** discard information; this is not a fresh audit or a claim that these project boundaries are final. Each project must verify and revise its own row when reached:

| Unmigrated project | Previously documented lower references |
| --- | --- |
| `Kaleido.Platform` | `Kaleido` |
| `Kaleido.Server`, `Kaleido.Client`, `Kaleido.Http` | `Kaleido.Platform` |
| `Kaleido.Http.Server` | `Kaleido.Server`, `Kaleido.Http` |
| `Kaleido.Http.Client` | `Kaleido.Client`, `Kaleido.Http` |
| `Kaleido.Registry` | `Kaleido.Server` |
| `Kaleido.Registry.Http` | `Kaleido.Registry`, `Kaleido.Http.Server` |
| `Kaleido.Mcp` | `Kaleido.Http.Server`, `Kaleido.Http.Client` |
| `Kaleido.Provider.SQLite` | `Kaleido`, `Kaleido.Registry` |
| `Kaleido.Observability.OpenTelemetry` | `Kaleido` |

The other projects have **not** completed their migration. Their actual `ProjectReference` declarations, not this old sketch or a proposed diagram, are authoritative until they are processed. The deleted assembly architecture tests leave a temporary enforcement gap: #591 must replace them with build-time source analyzers before epic completion; do not recreate an architecture-test suite or relax KAL fixture rules.
