# Kaleido source contributor guide

This is the contributor guide for `src/`. Read it before making changes to any Kaleido source project.

See also:
- [architecture map](../docs/architecture/README.md) — canonical design by processed area (the two legacy ARCHITECTURE.md files retire as their remaining sections migrate)
- [root AGENTS.md](../AGENTS.md) — repository-wide contributor rules

---

## Scope and boundaries

What each project owns is in the root [project boundaries](../AGENTS.md#project-boundaries); a project README explains its software and [focused architecture](../docs/architecture/README.md) explains its internals. These rules apply across `src/`:

- **Project references point one way.** A new reference is an architecture change: the [processed dependency graph](../docs/architecture/dependencies.md) is canonical for foundation, Eventing, Process and Queryable. `Kaleido` has no upper-layer Kaleido reference (its analyzer tooling reference carries analyzer assets only); Eventing references Kaleido and the capabilities reference both. Verify actual `.csproj` references of later, unmigrated projects rather than assuming the proposed graph is already implemented. Do not create cycles.
- **Project-wide imports:** put `<Using Include="..." />` in each `.csproj`, not in `GlobalUsings.cs`; migrate projects as they are touched.
- **Namespace/path alignment:** default to the project's name as its root namespace and mirror project-relative directories in declared namespaces. A moved type changes namespace with its folder; update active references, fixtures and PublicAPI together. Reconcile temporary compatibility RootNamespace overrides when their projects are processed rather than leaving a mismatch.
- **One type with behavior per file.** A type with hand-written logic (a method, operator or computed property; records included) is declared in a file named after it - a generic twin by its arity, `ProcessStepHandlerResult<T>` in `ProcessStepHandlerResultOfT.cs` - and has its own unit-test class (KAL0019, KAL1009, [types with behavior](../docs/ANALYZERS.md#types-with-behavior)). Data-only records, an interface with its implementation (KAL0014), exceptions and attributes may share a file. `[ExcludeFromCodeCoverage]` only on data types (KAL0020).
- **Public means consumer API.** Transports, front doors and providers are built into the solution, so seams shared between Kaleido's own projects are internal and shared through `InternalsVisibleTo`: always the MSBuild item (`<InternalsVisibleTo Include="..." />`), never the attribute. `src/Directory.Build.props` grants every package's own `<Package>.UnitTests` and Moq's `DynamicProxyGenAssembly2`; any other friend assembly goes in the package's `.csproj`, with a comment saying why. A type is public only when a consumer must name, implement or call it, and then it carries full XML documentation (CS1591 is enforced) and a line in its package's `PublicAPI.Unshipped.txt` (RS0016/RS0017 are enforced).

---

## Processed areas

Rules unique to an already-processed area live in that area's child AGENTS file: [foundation](Core/AGENTS.md), [capabilities](Capabilities/AGENTS.md) ([Process](Capabilities/Process/AGENTS.md), [Queryable](Capabilities/Queryable/AGENTS.md)) and [Eventing](Events/AGENTS.md). Read those files only when changing their subtree; design rationale belongs in [focused architecture](../docs/architecture/README.md), not repeated here.

---

### Telemetry conventions
Kaleido emits telemetry only for what only Kaleido can observe: the Process and Queryable runtimes,
the event outbox and relay, store providers and the MCP adapter. Host and transport statistics
(request rates, status codes, latencies) come from the host's own instrumentation - do not add a
Kaleido metric that duplicates them (ADR 0014).

Instrument names and tag keys live in the `*Telemetry` constants classes (`ProcessorTelemetry`,
`QueryableTelemetry`, `KaleidoEventTelemetry`, `SqliteTelemetry`, `McpTelemetry`) - constants only,
no `Meter`/`Counter` instances. Each emitting class owns its instruments as
`private static readonly Meter`/`Counter`/`Histogram` fields created with the shared `MeterName`;
when two classes emit on the same meter (e.g. the event outbox and the relay), each declares its own
instruments and the meter-name subscription merges them.

`Kaleido.Observability.OpenTelemetry` is not yet migrated: its old core-only reference needs review now that capabilities and Eventing own their instrumentation constants. Do not claim copied names are the final owner. A new meter or activity source must be registered in `AddKaleidoInstrumentation()` with tests when its owning project is processed.
## Transport and HTTP rules

### Kaleido.Platform stability
This is a shared contract boundary. Treat it like a public API:
- prefer additive changes (new optional fields) over breaking changes
- when a contract must change, update the matching endpoint, client method, and tests together

### Kaleido.Http.Server should stay thin
Middleware and endpoint mapping code should adapt contracts and wire the runtime. It should not reimplement runtime planning or execution logic that belongs in `Kaleido`.

### Transport contract boundary
A query request is never read as a core type (`QueryBody`, `QueryFilterNode`, `FilterOperator`, `SortDirection`, `LogicalOperator`): `QueryApiBody` and its sub-types in `Kaleido.Platform` are the request wire contract — string enums, `JsonElement` values. `QueryBodyResolver` (in `Kaleido.Http.Server`) converts between them before handing to core. Some core shapes do travel as they are in responses and registry metadata (`InformationRequest`, `ProcessMessage`, `KaleidoCaller`, the Process state enums, `AuthorizationMetadata`, `FilterOperator`/`MatchMode` in field metadata, `QueryResult<T>`): they are supported wire contracts, changed additively only after 1.0 and pinned by snapshot tests ([ADR 0017 amendment](../docs/decisions/0017-transport-contract-placement.md#amendment-2026-10-26-448-shared-core-shapes-and-the-behavior-abstractions-holds)).

Process never parses transport input. The transport reads each step into its type (`ProcessExecutionService` uses `KaleidoJsonOptions` and returns 400 for invalid input); the runtime receives typed steps only. `Kaleido.Process` must not depend on `System.Text.Json`; replacement build-time enforcement is tracked in #591. The event outbox uses JSON for its own redacted storage format (ADR 0013, #344).

`KaleidoJsonEndpointFilter` is applied to all Kaleido route groups — it wraps `IValueHttpResult` responses in `Results.Json(..., KaleidoJsonOptions.Options)` so enums serialize as strings. Do not call `ConfigureHttpJsonOptions` — it affects all endpoints in the host app, not just Kaleido's.

`KaleidoJsonOptions.Options` (in `Kaleido.Http`) is the single shared `JsonSerializerOptions` for all Kaleido HTTP serialization. Every `ReadFromJsonAsync`/`JsonContent.Create` call in Kaleido code must pass it explicitly.

When a delegated query source (`IDelegatedQuerySource`) receives a `QueryBody` and needs to forward it over HTTP, use `query.ToApiBody()` from `Kaleido.Platform` — samples and consumers should only reference `Kaleido.Platform`, not `Kaleido.Http.Server` (which carries ASP.NET Core dependencies).

---

## Correlation rules
`KaleidoCorrelationContext` fields are transport-agnostic foundation types; HTTP header names live in the transport/shared wire contract. Treat changes as broad-impact across transports, capability contexts and event payloads; consult the [correlation flow](../docs/architecture/correlation.md) before editing either side.

---

## What not to change casually

The processed foundation/Process/Queryable/Eventing invariants are in their child AGENTS files above. Shared HTTP JSON/header/error behavior belongs to `Kaleido.Http` without Platform, ASP.NET or capability/Registry references. The remaining Platform capability/Registry wire shapes are not migrated yet; coordinate their future owner moves with server/client adapters.

---

## Common pitfalls
- Moving runtime logic into `Kaleido.Http.Server` (endpoint mapping and middleware only)
- Changing `Kaleido.Platform` types without updating both the server endpoint and client
- Adding gRPC as a dependency of `Kaleido.Http.Server` — each protocol owns its own adapter and wire types (`Kaleido.Grpc*` placeholders exist but implement nothing yet)

---

## Verification

Follow the root [quality gate and #585 active-unit exception](../AGENTS.md#per-commit-quality-gate), plus the touched area's child AGENTS and corresponding test project. Run the full solution/test script after unmigrated dependents have been updated. Do not treat a stale or zero-test output as a passing gate; use the documented [test-output recovery](../docs/CONVENTIONS.md#when-a-test-project-fails-to-run-or-reports-zero-tests).
