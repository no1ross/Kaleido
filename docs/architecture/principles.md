# Principles and terminology

Kaleido is a process engine: it executes declared mechanisms but does not make application business decisions ([ADR 0001](../decisions/0001-process-engine-not-decision-maker.md)). Behavioral owners define and register their own capabilities; lower layers must not reference higher ones ([ADR 0041](../decisions/0041-capability-owned-project-boundaries.md)). Transport adapters publish or invoke capabilities without reimplementing their runtime decisions. Package boundaries under #585 are migrating; the current processed graph is in [dependencies](dependencies.md).

| Term | Meaning |
| --- | --- |
| **Service** | A deployed Kaleido host with `Kaleido:ServiceName` and required `ServiceVersion` (from code or configuration), published at `/{service}/{version}` by HTTP. |
| **Processor** | A service's step-hosting runtime/DI identity (`Kaleido.Process` and its folder-aligned namespaces), registered with `AddProcess()`. |
| **Process** | One executing workflow instance (`ProcessId`, `ProcessExecutionState`, process events and `/processes` routes). |
| **Step** | A unit of work in a Processor (`IProcessStep`, `[ProcessStep]`), identified by its type name; the step marks work, not the executor. |
| **Source / View / Query context** | A Queryable source (`IQuerySource<T>`, `[QuerySource]`) is the published capability; a view (`IQueryViewSource<TSource,…>`, `[QueryView]`) projects it; a context (`IQueryContext`) describes how it is queried. Names are type names. |
| **Delegated** | A source fulfilled downstream (`IDelegatedQuerySource`, `DelegatedQuerySourceEngine`); that fulfillment detail is not visible to consumers. |
| **Central registry** | Service-version registration and discovery (`KaleidoRegistry`); participating hosts hydrate a full copy for internal resolution. The Registry area has not yet been migrated. |

## Capability identity and service versions

A capability has no independent version attribute; its type name identifies it inside a **service version** ([ADR 0029](../decisions/0029-capabilities-have-no-version.md), [ADR 0040](../decisions/0040-central-registry-only-and-versioned-routes.md)). Several service versions may publish the same name concurrently; a changed capability or logical route changes the registry revision and participating hosts hydrate it at the registry's pace. Keep a capability's name for compatible wording/new optional inputs/new output fields. Removing, renaming or requiring an input, changing a type/value's meaning, or changing relationships that would strand saved processes requires a new name (e.g. `CaptureMemberStepV2`) or a new service version. In-flight processes remain pinned to their service version and route: retain that host/gateway path until they finish; close an old step with `[AvailableUntil]` before removal.

## Transport-neutral response semantics

Define response meaning before choosing JSON, gRPC or another wire shape. Keep handler-authored `BusinessMessages` distinct from framework `FrameworkMessages`; callers may present the former and treat the latter as diagnostics. An option can include framework messages, but when disabled the collection is **empty**, not absent by a JSON-only rule. Repeated fields have the same empty-collection semantics over future transports. Adapters decide which messages to populate; serializer-specific omission does not define the contract or belong in core runtime types.

Use `Processor*` for what hosts/runs work (runtime, registries, planner, executor, telemetry identity); use `Process*` for one executing instance and its request, response, state, storage contracts and events. A published HTTP resource can still be `processes` even where `Kaleido.Platform.Processor`, `Kaleido.Http.Server.Processor` and `Kaleido.Http.Client.Processor` adapt its contracts. More detailed capability behavior belongs in [Process](capabilities/process.md) and [Queryable](capabilities/queryable.md), not this vocabulary page.
