# Kaleido Code Organization & Discoverability Review

> Initial findings and recommendations. Implementation status noted inline.

---

## Executive Summary

Kaleido's source is well-structured at the project boundary level. The six-project split (`Kaleido`, `Kaleido.Http`, `Kaleido.Http.Abstractions`, `Kaleido.Http.Client`, `Kaleido.Observability.OpenTelemetry`, `Kaleido.Provider.SQLite`) is logical and clean. The problems exist _within_ those projects: excessive fragmentation into one-type-per-file, internal interfaces separated from their only implementation, enumerations scattered across three `Enumerations.cs` files, and records that belong together split across many files. A new maintainer reading `src/Kaleido` must navigate ~110 files to understand two features (Process and Queryable). That is the core discoverability problem.

**Overall scores:**
- Organization: **5/10** — correct at the project level; fragmented within projects
- Discoverability: **4/10** — a newcomer must open many files to understand even a single concept
- Maintainability: **6/10** — naming is clear and consistent; cognitive load is the drag

**Estimated file reduction with full recommendations applied: ~45 files (from ~196 to ~151)**

---

## Part 1 — Fragmentation Audit

### Process (src/Kaleido)

| Area | Files Today | Estimated Files After |
|---|---|---|
| Attributes | 5 separate files | 1 (`ProcessStepAttributes.cs`) |
| Enumerations | 3 separate `Enumerations.cs` files | 1 (`ProcessEnumerations.cs`) |
| Eventing | 7 files | 2–3 files |
| Execution | 10 files | 6 files |
| Planning | 6 files | 3 files |
| Registry | 7 files | 3–4 files |
| Context | 2 files | 1 file |
| Root (`Process/`) | 4 files | 2 files |

**Concept:** Process runtime  
**Files involved:** 44 files across `Process/`, `Process/Attributes/`, `Process/Context/`, `Process/Eventing/`, `Process/Execution/`, `Process/Planning/`, `Process/Registry/`, `Process/Observability/`  
**Estimated cognitive load:** High. A newcomer must open 10+ files to understand what a step handler result is, what states exist, how planning works, and how execution evaluates decisions.  
**Recommendation:** Consolidate by sub-concept. See detailed recommendations below.

---

### Queryable (src/Kaleido)

| Area | Files Today | Estimated Files After |
|---|---|---|
| Attributes | 6 separate files | 1 (`QueryableAttributes.cs`) |
| Query interfaces | 4 separate interface files | 1 (`QuerySources.cs`) |
| Records/Registries | 6 files | 3 files |
| Metadata | 1 file (good — 185 lines) | keep |
| Runtime | 2 files | 1 file |
| Root (`Queryable/`) | 5 files | 3 files |

**Concept:** Queryable runtime  
**Files involved:** ~25 files  
**Estimated cognitive load:** Medium-high. The `Records/` folder name is actively misleading — it contains registries, not records.  
**Recommendation:** Rename `Records/` → `Registry/`. Consolidate attribute files. Colocate single-use interfaces.

---

### Kaleido.Http.Abstractions

| Area | Files Today | Estimated Files After |
|---|---|---|
| Process contracts | 6 files in `Process/Contracts/` | 1–2 files |
| Queryable contracts | 6 files in `Queryable/Contracts/` | 1–2 files |
| Process client interfaces | 3 files | 1 file |
| Queryable client interfaces | 3 files | 1 file |
| Registry | 2 files | 1 file |

**Concept:** HTTP contract types  
**Files involved:** 22 files  
**Estimated cognitive load:** Medium. Each file is small and the contracts are stable, but a maintainer must open 5–6 files to understand the full shape of the Process HTTP API.  
**Recommendation:** Consolidate contracts into feature files.

---

### Kaleido.Http.Client

**Files involved:** 14 files  
**Estimated cognitive load:** Low-medium. The client project is small and well-organized. Main issue: `KaleidoProcessClientServiceCollectionExtensions.cs` is 4 lines and could be inlined into `KaleidoClientExtensions.cs`.

---

### Kaleido.Provider.SQLite

**Files involved:** 11 files  
**Estimated cognitive load:** Low. EF Core convention forces some separation (entities, configurations). Acceptable as-is. Minor: the 4 entity configuration files could potentially be colocated with entities.

---

## Part 2 — Top 20 File Organization Improvements

### 1. Consolidate Process attributes into one file ✅
**Category:** Record/Attribute Consolidation  
**Current:** `Process/Attributes/ProcessStepAttribute.cs`, `RepeatableAttribute.cs`, `DependsOnStepAttribute.cs`, `AvailableAfterAttribute.cs`, `AvailableUntilAttribute.cs` — 5 files  
**Proposed:** `Process/ProcessStepAttributes.cs` — all 5 attributes in one file  
**Why it matters:** These 5 attributes are always read together. `DependsOnStepAttribute` only ever appears alongside `ProcessStepAttribute`. Splitting them teaches nothing; it just forces 5 file opens.  
**Expected benefit:** A developer can see the entire step authoring model in one file.  
**Complexity:** Small  
**Breaking change:** No  
**Status:** Done. Namespace promoted from `Kaleido.Process.Attributes` → `Kaleido.Process`.

---

### 2. Consolidate Queryable attributes into one file ✅
**Category:** Attribute Consolidation  
**Current:** `Queryable/Attributes/FilterableAttribute.cs`, `PageableAttribute.cs`, `QueryContextAttribute.cs`, `QueryViewAttribute.cs`, `SearchableAttribute.cs`, `SortableAttribute.cs` — 6 files  
**Proposed:** `Queryable/QueryableAttributes.cs`  
**Why it matters:** Same reason as #1. These attributes are always used together when authoring a query context.  
**Complexity:** Small  
**Breaking change:** No  
**Status:** Done. Namespace promoted from `Kaleido.Queryable.Attributes` → `Kaleido.Queryable`.

---

### 3. Merge all Process enumerations into one file ✅
**Category:** Fragmentation  
**Current:** `Process/Enumerations.cs` (StepExecutionOutcome, StepExecutionStatus, MessageType, StepProcessingMessageCode), `Process/Execution/Enumerations.cs` (ExecutionDecisionType, ProcessExecutionState), `Process/Planning/Enumerations.cs` (StepCandidateStatus) — 3 files  
**Proposed:** `Process/ProcessEnumerations.cs` — all process enums in one file  
**Why it matters:** Three files named `Enumerations.cs` in different folders is a navigation trap. `ProcessExecutionState` and `StepExecutionOutcome` are conceptually adjacent; a developer looking for one will want the other.  
**Complexity:** Small  
**Breaking change:** No (namespace changes if `Execution` namespace types move to `Process` namespace — see #4)  
**Status:** Done. All enums consolidated into `Process/ProcessEnumerations.cs` under `Kaleido.Process`.

---

### 4. Promote `ProcessExecutionState` and `ExecutionDecisionType` to `Kaleido.Process` namespace ✅
**Category:** Namespace Simplification  
**Current:** `ProcessExecutionState` is in `Kaleido.Process.Execution` namespace despite being a core public-facing concept visible in `ProcessResult`, `ProcessorContext`, HTTP responses, and events.  
**Proposed:** Move to `Kaleido.Process` namespace alongside `ProcessResult`, `ProcessRequest`.  
**Why it matters:** A consumer importing `ProcessResult` (namespace `Kaleido.Process`) shouldn't need a separate `using Kaleido.Process.Execution` just to read the state.  
**Complexity:** Small  
**Breaking change:** Yes (namespace change for `ProcessExecutionState`, `ExecutionDecisionType`)  
**Status:** Done as part of #3 (ProcessEnumerations consolidation).

---

### 5. Colocate `ProcessorContext` and `IProcessContextStore` with `InMemoryProcessContextStore` ✅
**Category:** Interface + Implementation Colocation  
**Current:** `Process/Context/InMemoryProcessContextStore.cs` contains `IProcessContextStore`, `ProcessorContext`, `StepContext`, and `InMemoryProcessContextStore` — actually already colocated in one file. Good.  
**Issue:** The file is named after the default implementation (`InMemoryProcessContextStore.cs`), which makes it hard to find `IProcessContextStore` by searching for it conceptually.  
**Proposed:** Rename to `ProcessContextStore.cs`  
**Why it matters:** The name should reflect the primary concept, not the default implementation.  
**Complexity:** Small  
**Breaking change:** No  
**Status:** Done. Renamed to `ProcessContextStore.cs`, class renamed to `ProcessContextStore`.

---

### 6. Colocate `IProcessStateUpdater` with `ProcessStateUpdater`
**Category:** Interface + Implementation Colocation  
**Current:** `Process/Context/ProcessStateUpdater.cs` — contains both interface and implementation. Already colocated. Good.  
**Issue:** None. This is a good example already done correctly.

---

### 7. Colocate `IStepExecutionEvaluator` and `StepExecutionEvaluator`
**Category:** Interface + Implementation Colocation  
**Current:** Already colocated in `Process/Execution/StepExecutionEvaluator.cs`. Good.

---

### 8. Consolidate `ProcessStepResult.cs` and `IProcessStepHandler.cs` ✅
**Category:** Cohesion  
**Current:** `Process/Execution/ProcessStepResult.cs` contains `IProcessStepHandlerResult`, `ProcessStepHandlerResult<T>`, and `ProcessStepHandlerResult`. `Process/Execution/IProcessStepHandler.cs` contains the two handler interfaces. These are inseparable — you cannot use `IProcessStepHandler` without `ProcessStepHandlerResult`.  
**Proposed:** Merge into `Process/Execution/ProcessStepHandler.cs`  
**Why it matters:** A consumer implementing `IProcessStepHandler<T>` needs to know both the interface and the result type. Today that requires two file opens.  
**Complexity:** Small  
**Breaking change:** No  
**Status:** Done.

---

### 9. Merge `ProcessStepInvoker.cs` interface and result into itself
**Category:** Colocation  
**Current:** `ProcessStepInvoker.cs` already contains `IProcessStepInvoker`, `ProcessStepInvokerResult`, and `ProcessStepInvoker`. Good — this is the right pattern.  
**Issue:** `ProcessStepInvokerResult` and `ProcessStepHandlerResult` are very similar types that confuse new readers. Consider a naming clarification (see discoverability section).

---

### 10. Consolidate `ExecutionDecision.cs` into `ProcessExecutor.cs`
**Category:** Internal Type Colocation  
**Current:** `Process/Execution/ExecutionDecision.cs` is an `internal sealed record` only used by `ExecutionProcessor` and `StepExecutionEvaluator`.  
**Proposed:** Move into `ProcessExecutor.cs` as a nested type, or create a single `ExecutionEngine.cs` containing `IExecutionProcessor`, `ExecutionProcessor`, and `ExecutionDecision`.  
**Why it matters:** `ExecutionDecision` has no independent consumer value. It is an implementation detail of the evaluator/executor pair.  
**Complexity:** Small  
**Breaking change:** No (internal type)

---

### 11. Consolidate `ProcessStepDefinition.cs` into `ProcessStepRegistry.cs` ✅
**Category:** Internal Type Colocation  
**Current:** `Process/Registry/ProcessStepDefinition.cs` contains two internal records (`ProcessStepDefinition`, `ProcessStepTypeDefinition`) used only inside `ProcessStepRegistry.cs`.  
**Proposed:** Move as nested types or merge into `ProcessStepRegistry.cs`.  
**Why it matters:** These are implementation details of a four-pass build algorithm inside the registry. They should live next to the algorithm that uses them.  
**Complexity:** Small  
**Breaking change:** No (internal types)  
**Status:** Done.

---

### 12. Consolidate `ProcessStepDependencyGraph.cs` into `ProcessStepRegistry.cs` ✅
**Category:** Internal Type Colocation  
**Current:** `Process/Registry/ProcessStepDependencyGraph.cs` contains the cycle-detection logic used only during registry construction.  
**Proposed:** Move into `ProcessStepRegistry.cs` or its partial file.  
**Why it matters:** Only used in one place.  
**Complexity:** Small  
**Breaking change:** No (internal type)  
**Status:** Done.

---

### 13. Consolidate `ParticipantRegistryItem.cs` into `ProcessRegistry.cs` ✅
**Category:** Record Consolidation  
**Current:** `Process/Registry/ParticipantRegistryItem.cs` contains 7 public records (`ProcessorRegistryItem`, `ProcessorStepRegistryItem`, `ProcessorStepSummary`, `ProcessorPropertyDescriptor`, `ProcessorInputFieldDescriptor`, `ProcessorOutputFieldDescriptor`, `ProcessorStepResultDescriptor`) used as the discovery contract. `ProcessRegistry.cs` contains `IProcessRegistry` and `ProcessRegistry`.  
**Proposed:** Merge all into one file named `ProcessRegistry.cs` (or `ProcessorDiscovery.cs` if you want to separate the builder from the output shape).  
**Why it matters:** These records are the output shape of `IProcessRegistry`. They belong next to the registry that produces them.  
**Complexity:** Small  
**Breaking change:** No  
**Status:** Done.

---

### 14. Rename `Queryable/Records/` folder to `Queryable/Registry/` ✅
**Category:** Naming  
**Current:** `Queryable/Records/` contains `QueryableRegistry.cs`, `QueryContextRegistry.cs`, `QueryViewRegistry.cs`, `DelegatedQueryViewRegistry.cs`, `QueryContextRegistrationValidator.cs`, `QueryViewRegistrationValidator.cs` — not records at all.  
**Proposed:** Rename to `Queryable/Registry/`  
**Why it matters:** `Records/` strongly implies data shapes. The actual content is registry and validation infrastructure. Every new maintainer will look in the wrong place.  
**Complexity:** Small  
**Breaking change:** No (internal namespace only)  
**Status:** Done. Namespace updated from `Kaleido.Queryable.Records` → `Kaleido.Queryable.Registry`.

---

### 15. Consolidate Queryable query source interfaces into one file ✅
**Category:** Interface Consolidation  
**Current:** `Queryable/Query/IQueryContextSource.cs`, `IQueryContextSourceAsync.cs`, `IQueryViewSource.cs`, `IQueryViewSourceAsync.cs`, `IDelegateQueryViewSource.cs` — 5 separate interface files  
**Proposed:** `Queryable/Query/QuerySources.cs`  
**Why it matters:** These 5 interfaces form the authoring contract for Queryable. A developer implementing a query context needs all of them visible at once. No independent value in separating them.  
**Complexity:** Small  
**Breaking change:** No  
**Status:** Done. Consolidated into `Queryable/QuerySources.cs` and promoted to `Kaleido.Queryable` namespace (these are the primary consumer extension surface, not internal query machinery).

---

### 16. Consolidate `Kaleido.Http.Abstractions` Process contracts
**Category:** Contract Consolidation  
**Current:** `Process/Contracts/ExecuteProcessRequest.cs`, `ProcessContractUrls.cs`, `ProcessEndpointNames.cs`, `ProcessExecutionResponse.cs`, `ProcessStateResponse.cs`, `ProcessStepResponse.cs` — 6 files  
**Proposed:** `Process/ProcessContracts.cs` (request/response types) + `Process/ProcessRoutes.cs` (URLs and endpoint names)  
**Why it matters:** A developer reading the Process HTTP API must open 6 files. These types are used together.  
**Complexity:** Small  
**Breaking change:** No

---

### 17. Consolidate `Kaleido.Http.Abstractions` Queryable contracts
**Category:** Contract Consolidation  
**Current:** `Queryable/Contracts/PageableContract.cs`, `QueryableContractUrls.cs`, `QueryableFieldMetadata.cs`, `QueryableQueryParameter.cs`, `QueryableRecordResponse.cs`, `QueryableRecordSummary.cs`, `QueryApiRequest.cs` — 7 files  
**Proposed:** `Queryable/QueryableContracts.cs` + `Queryable/QueryableRoutes.cs`  
**Complexity:** Small  
**Breaking change:** No

---

### 18. Consolidate Process client interface files in `Kaleido.Http.Abstractions`
**Category:** Interface + Type Consolidation  
**Current:** `Process/IKaleidoProcessClient.cs`, `Process/IKaleidoProcessClientFactory.cs`, `Process/KaleidoProcessClientRouteOptionsMap.cs`, `Process/ProcessRoutePaths.cs`, `Process/Services/ProcessResponseFactory.cs` — 5 files for a single concept  
**Proposed:** `Process/ProcessClient.cs` (interfaces + route map) + keep `ProcessResponseFactory.cs` separate (it's a service)  
**Complexity:** Small  
**Breaking change:** No

---

### 19. Merge `KaleidoProcessClientServiceCollectionExtensions.cs` into `KaleidoClientExtensions.cs`
**Category:** Fragmentation  
**Current:** `KaleidoHttpClient/Process/KaleidoProcessClientServiceCollectionExtensions.cs` is 20 lines — a single method calling `AddKaleidoClient`.  
**Proposed:** Move `AddProcessClient` and `AddQueryableClient` into `KaleidoClientExtensions.cs` as internal extension methods.  
**Complexity:** Small  
**Breaking change:** No (internal methods)

---

### 20. Rename `Queryable/Runtime/` types to reflect their role
**Category:** Naming / Discoverability  
**Current:** `QueryContextExecutor.cs` contains `IQueryContextExecutor<>` and `QueryContextExecutor<>`. `CompiledQueryApplier.cs` contains `ICompiledQueryApplier<>` and `CompiledQueryApplier<>`. Both are internal.  
**Proposed:** Both are fine as-is for naming. Consider merging into `Queryable/Query/QueryRuntime.cs` since they are only called by `QueryContextEngine`.  
**Complexity:** Small  
**Breaking change:** No

---

## Part 3 — Top 10 Discoverability Improvements

### 1. "Where is ProcessResult?" — ProcessRuntime.cs contains `IProcessRuntime`, `ProcessRequest`, `ProcessResult`, and `ProcessRequest.ForStep<T>` in one file
**Current:** A developer looking for `ProcessResult` has to either search or discover it lives in `ProcessRuntime.cs`.  
**Recommendation:** Rename to `Process/ProcessRuntime.cs` with a comment at the top noting what the file contains, or split `ProcessRequest`/`ProcessResult` into `Process/ProcessModels.cs`. Either is fine; the key is that the file name should telegraph its contents.  
**Expected benefit:** New developer can find the public surface of the Process feature in one place.

---

### 2. "What is ProcessStepHandlerResult vs ProcessStepInvokerResult?"
**Current:** Two similar-sounding types exist: `ProcessStepHandlerResult` (what a handler returns to the framework) and `ProcessStepInvokerResult` (what the invoker returns after unwrapping the handler result). A new developer will confuse them.  
**Recommendation:** Rename `ProcessStepInvokerResult` → `StepInvocationResult` or `ProcessStepExecutionResult` to make the distinction clear in the name. It is an internal type.  
**Expected benefit:** Reduces confusion for future contributors.

---

### 3. "What does a step handler return?" — answer requires 2 file opens
**Current:** `IProcessStepHandler` is in `IProcessStepHandler.cs`; `ProcessStepHandlerResult` is in `ProcessStepResult.cs`. These are the core consumer contract.  
**Recommendation:** Merge into `Process/Execution/ProcessStepHandler.cs`. File name matches the consumer concept.  
**Expected benefit:** One file open to understand the authoring contract.

---

### 4. "Where is the query execution pipeline?" — 4 file hops
**Current:** `QueryableService` → dispatches to → `QueryContextEngine` or `DelegatedQueryViewEngine` → uses → `IQueryContextExecutor` → uses → `ICompiledQueryApplier`. All in different files.  
**Recommendation:** Add a comment block at the top of `QueryableService.cs` that names the dispatch chain and the files involved. Consider consolidating `IQueryContextExecutor`+`QueryContextExecutor` and `ICompiledQueryApplier`+`CompiledQueryApplier` into `QueryRuntime.cs` since they are pure internals with no independent consumer value.

---

### 5. "What namespaces do I need for a process step handler?"
**Current:** Implementing a handler requires `using Kaleido.Process.Execution` (for `IProcessStepHandler<T>`, `ProcessStepHandlerResult`, `ProcessStepContext`) — but `ProcessStepContext` contains `StepContext` from `Kaleido.Process.Context`, and `ProcessMessage` from `Kaleido.Process`. Multiple namespace imports for a single concept.  
**Recommendation:** Consider whether the sub-namespaces (`Kaleido.Process.Execution`, `Kaleido.Process.Planning`) provide enough value to justify the import overhead. For consumer-facing types specifically, consolidating into `Kaleido.Process` would make the consumer experience cleaner.

---

### 6. "Where is IProcessContextStore?"
**Current:** `IProcessContextStore` lives in `InMemoryProcessContextStore.cs` — a file named after the default implementation.  
**Recommendation:** Rename the file to `ProcessContextStore.cs` as noted in improvement #5.

---

### 7. "`Records/` folder in Queryable" — misleading name
**Current:** `Queryable/Records/` contains registries and validators, not records.  
**Recommendation:** Rename to `Queryable/Registry/`. (Improvement #14 above.)

---

### 8. Three `Enumerations.cs` files
**Current:** `Process/Enumerations.cs`, `Process/Execution/Enumerations.cs`, `Process/Planning/Enumerations.cs` — three files with the same name, impossible to distinguish in IDE tabs.  
**Recommendation:** Merge into `Process/ProcessEnumerations.cs`. (Improvement #3 above.)

---

### 9. `ProcessStepRegistration.Projection.cs` — partial file with no obvious purpose ✅
**Current:** `Process/Registry/ProcessStepRegistration.Projection.cs` extends `ProcessStepRegistration` with projection helpers.  
**Recommendation:** Either inline into `ProcessStepRegistration.cs` (if small enough) or rename to `ProcessStepRegistration.RegistryProjection.cs` to make its purpose clear from the name.  
**Status:** Done. Renamed to `ProcessStepRegistration.Discovery.cs`.

---

### 10. `Queryable/Query/CompiledContracts.cs` — ambiguous name
**Current:** Contains internal types used by the query compilation pipeline.  
**Recommendation:** Rename to `QueryCompilationContracts.cs` or merge into `QueryRequestCompiler.cs` if they are only used there.

---

## Part 4 — Top 10 Colocation Opportunities

| # | Types | Current Files | Proposed File | Reason |
|---|---|---|---|---|
| 1 | `IProcessStepHandler<T>`, `IProcessStepHandler<T,R>`, `ProcessStepHandlerResult`, `ProcessStepHandlerResult<T>`, `IProcessStepHandlerResult` | 2 files | `ProcessStepHandler.cs` | Consumer contract — always used together |
| 2 | `IExecutionProcessor`, `ExecutionProcessor`, `ExecutionDecision` | 2 files | `ProcessExecutor.cs` | `ExecutionDecision` is internal to executor |
| 3 | `IQueryContextSource<T>`, `IQueryContextSourceAsync<T>`, `IQueryViewSource<T,V>`, `IQueryViewSourceAsync<T,V>`, `IDelegateQueryViewSource<T,V>` | 5 files | `QuerySources.cs` | Authoring contract — always used together |
| 4 | `ProcessStepDefinition`, `ProcessStepTypeDefinition` | 1 file | Merge into `ProcessStepRegistry.cs` | Only used inside registry |
| 5 | `RegistrationNode`, `RegistrationSlot` | `ProcessStepRegistry.cs` | Already there — good pattern | — |
| 6 | `IProcessStepRegistry` | `ProcessStepRegistry.cs` | Already there — good pattern | — |
| 7 | `QueryContextRegistrationValidator`, `QueryViewRegistrationValidator` | 2 files | `QueryableValidators.cs` | Related validators with same lifecycle |
| 8 | `IQueryContextRegistry`, `QueryContextRegistry` | `QueryContextRegistry.cs` | Already colocated — good | — |
| 9 | `ProcessorRegistryItem` + 6 related records | `ParticipantRegistryItem.cs` | Merge into `ProcessRegistry.cs` | Output shape of registry |
| 10 | `KaleidoProcessClientServiceCollectionExtensions`, `KaleidoQueryableClientServiceCollectionExtensions` | 2 files | Merge into `KaleidoClientExtensions.cs` | 4-line internal methods with no independent value |

---

## Part 5 — Top 10 Record Consolidation Opportunities

| # | Records | Current Location | Proposed Location | Reason |
|---|---|---|---|---|
| 1 | `ProcessRequest`, `ProcessResult`, `ProcessStepResult` | `ProcessRuntime.cs` + `Process/ProcessStepResult.cs` (implied) | `ProcessRuntime.cs` | Public API surface — all in one place |
| 2 | `ProcessorRegistryItem`, `ProcessorStepRegistryItem`, `ProcessorStepSummary`, `ProcessorPropertyDescriptor`, `ProcessorInputFieldDescriptor`, `ProcessorOutputFieldDescriptor`, `ProcessorStepResultDescriptor` | `ParticipantRegistryItem.cs` | Merge into `ProcessRegistry.cs` | Registry output shape belongs with registry |
| 3 | `ProcessStepRegistration`, `RepeatableOptions`, `ProcessStepMetadata` | `ProcessStepRegistration.cs` | Keep together — already good | — |
| 4 | `ProcessStepDefinition`, `ProcessStepTypeDefinition` | `ProcessStepDefinition.cs` | Merge into `ProcessStepRegistry.cs` | Internal build-time types |
| 5 | `ExecutionDecision` | `ExecutionDecision.cs` | Merge into `ProcessExecutor.cs` | Internal type used only in executor/evaluator |
| 6 | `ProcessStepInvokerResult` | `ProcessStepInvoker.cs` | Already colocated — good | — |
| 7 | `QueryContextRegistration`, `QueryContextMetadata`, `FieldMetadata`, `QueryViewRegistration`, `DelegatedQueryViewRegistration`, `QueryViewMetadata`, `PageableMetadata`, `QueryParameterMetadata`, `QueryOutputFieldMetadata`, `QueryableContextRegistryItem`, `QueryableViewRegistryItem`, `QueryablePropertyDescriptor`, `QueryableFieldDescriptor`, `QueryableParameterDescriptor`, `QueryableOutputFieldDescriptor`, `QueryContextKind` | `Queryable/Metadata/QueryRegistration.cs` | Keep together — this is already correct | One 185-line file beats 15 files |
| 8 | `KaleidoEventEnvelope`, `IKaleidoEvent`, `IEventPublisher`, `NullEventPublisher` | Split across 2 files | `Eventing/EventPublisher.cs` | All eventing abstractions in one file | ✅ Done (`NullEventPublisher` → `EventPublisher`) |
| 9 | `ProcessEventContext`, `QueryableEventContext` | 2 files | `Eventing/EventContexts.cs` | Naturally grouped context types | — |
| 10 | `ProcessCreated`, `PlanBuilt`, `PlanBuiltCandidate`, `PlanBuiltCandidateMessage`, `StepCompleted`, `ExecutionCompleted`, `ProcessEventBase` | 6 files in `Process/Eventing/` | `Process/Eventing/ProcessEvents.cs` | All process events belong together | ✅ Done |

---

## Part 6 — Top 10 Interface Consolidation Opportunities

| # | Interface | Implementation | Recommendation | Reason |
|---|---|---|---|---|
| 1 | `IProcessStepHandler<T>`, `IProcessStepHandler<T,R>` | Consumer-implemented | Keep separate — public consumer contract | — |
| 2 | `IProcessRuntime` | `ProcessRuntime` | Colocate in `ProcessRuntime.cs` | Already done — good |
| 3 | `IExecutionProcessor` | `ExecutionProcessor` | Already colocated — good | — |
| 4 | `IProcessStepInvoker` | `ProcessStepInvoker` | Already colocated — good | — |
| 5 | `IProcessContextStore` | `InMemoryProcessContextStore` | Already colocated — rename file to `ProcessContextStore.cs` | File name misleads |
| 6 | `IQueryableService` | `QueryableService` | Already colocated — good | — |
| 7 | `IQueryContextRegistry` | `QueryContextRegistry` | Already colocated — good | — |
| 8 | `IQueryViewRegistry` | `QueryViewRegistry` | Keep public — consumed by `Kaleido.Http` for endpoint mapping | — |
| 9 | `IKaleidoProcessClient`, `IKaleidoProcessClientFactory` | `KaleidoProcessClient`, `KaleidoProcessClientFactory` | Separate — these ARE independent consumer contracts | Public API |
| 10 | `IStepExecutionEvaluator`, `IStepAvailabilityResolver`, `IProcessStateUpdater` | All internal | All already colocated with implementations — good pattern | — |

---

## Part 7 — Top 10 Internal Type Consolidation Opportunities

| # | Type | Current File | Proposed Location | Reason |
|---|---|---|---|---|
| 1 | `ExecutionDecision` | `ExecutionDecision.cs` | Nest inside `ProcessExecutor.cs` | Only used by executor/evaluator |
| 2 | `ProcessStepDefinition`, `ProcessStepTypeDefinition` | `ProcessStepDefinition.cs` | Merge into `ProcessStepRegistry.cs` | Build-time types used only in registry | ✅ Done |
| 3 | `KaleidoEventTypes` | `Eventing/KaleidoEventTypes.cs` | Merge into `Eventing/ProcessEventFactory.cs` or `EventPublisher.cs` | Internal constants used only in factories | ✅ Done (inlined as string literals) |
| 4 | `QueryContextRegistrationValidator` | `Records/QueryContextRegistrationValidator.cs` | Merge into `QueryContextRegistry.cs` | Validator is only called during registry construction |
| 5 | `QueryViewRegistrationValidator` | `Records/QueryViewRegistrationValidator.cs` | Merge into `QueryViewRegistry.cs` | Same reason |
| 6 | `InMemoryProcessContextStore` | `ProcessContext/InMemoryProcessContextStore.cs` | Keep here — already colocated with `IProcessContextStore` | Only rename file |
| 7 | `CompiledContracts` types | `Queryable/Query/CompiledContracts.cs` | Merge into `QueryRequestCompiler.cs` | Only used in compiler |
| 8 | `NullDisposable` | Root `NullDisposable.cs` | Inline where used or delete if unused | Utility type with no external value |
| 9 | `ValueConverter` | `Json/ValueConverter.cs` | Keep as-is | JSON infrastructure with clear home |
| 10 | `ProcessStepRegistry.Helpers.cs` | Partial file | Keep as partial — reasonable split given registry complexity | — |

---

## Part 8 — Specific Problem Areas

### Most Fragmented Framework Concept: Process Eventing
7 files to describe 4 events and their factory: `ExecutionCompleted.cs`, `PlanBuilt.cs`, `PlanBuiltCandidate.cs`, `PlanBuiltCandidateMessage.cs`, `ProcessCreated.cs`, `ProcessEventBase.cs`, `ProcessEventFactory.cs`.  
A maintainer adding a new process event must create a new file, register a new constant in `KaleidoEventTypes`, add a factory method to `IProcessEventFactory` and `ProcessEventFactory`, and update the factory's event building logic — across 3 separate files just for the event type.  
**Recommendation:** Consolidate all event records into `Process/Eventing/ProcessEvents.cs`. Keep `ProcessEventFactory.cs` separate (it has real logic). Delete `KaleidoEventTypes.cs` and inline the constants into the factory.

---

### Most Confusing Source Structure: `Queryable/Records/`
The folder name `Records/` in a C# codebase implies data shape types (record types). The actual contents are registration infrastructure — `QueryContextRegistry`, `QueryViewRegistry`, `DelegatedQueryViewRegistry`, `QueryContextRegistrationValidator`, `QueryViewRegistrationValidator`, `QueryableRegistry`. None of these are "records" in any meaningful sense.  
**Recommendation:** Rename to `Queryable/Registry/`.

---

### Biggest Discoverability Issue: Three `Enumerations.cs` files
Opening any of the three files named `Enumerations.cs` in an IDE (especially with fuzzy file search) will give results from three different folders. `ProcessExecutionState` is in `Process/Execution/Enumerations.cs` but `StepExecutionOutcome` is in `Process/Enumerations.cs`. A developer writing `switch (state)` on `ProcessExecutionState` and then trying to understand what `StepExecutionOutcome` means must know to look in a different file with the same name.  
**Recommendation:** Merge all process enumerations into `Process/ProcessEnumerations.cs`. This is a single-file, low-risk change with high discoverability payoff.

---

## Part 9 — Namespace Review

### Unnecessary namespace depth
- `Kaleido.Process.Execution` — `ProcessExecutionState` and `ExecutionDecisionType` are consumer-visible types in the `Execution` sub-namespace. A consumer calling `ProcessRuntime.ExecuteAsync()` gets back a `ProcessResult` from `Kaleido.Process` but reads its `.State` property which is of type `ProcessExecutionState` from `Kaleido.Process.Execution`. Two `using` statements for one operation.  
  **Recommendation:** Move `ProcessExecutionState` to `Kaleido.Process` namespace.

- `Kaleido.Process.Attributes` — attributes are in a sub-namespace but are always authored alongside process step classes in consumer code. The sub-namespace forces an extra `using`.  
  **Recommendation:** Move to `Kaleido.Process` namespace.

- `Kaleido.Queryable.Attributes` — same issue.  
  **Recommendation:** Move to `Kaleido.Queryable` namespace.

### Namespace fragmentation that is justified and should stay
- `Kaleido.Process.Planning` — planning is a distinct pipeline layer; internal types should stay namespaced separately
- `Kaleido.Http.Abstractions` vs `Kaleido.Http` — correct separation; shared contract boundary
- `Kaleido.Eventing` — correct; eventing is orthogonal to Process and Queryable

---

## Part 10 — File Naming Review

| File | Issue | Recommendation |
|---|---|---|
| `InMemoryProcessContextStore.cs` | Named after default implementation, not the concept | Rename to `ProcessContextStore.cs` | ✅ Done |
| `ParticipantRegistryItem.cs` | "Participant" is not used anywhere in the public API | Rename to `ProcessRegistryItems.cs` or merge into `ProcessRegistry.cs` | ✅ Done (merged into `ProcessRegistry.cs`) |
| `NullEventPublisher.cs` | Contains `IEventPublisher`, `IKaleidoEvent`, and `NullEventPublisher` — three distinct concepts | Rename to `EventPublisher.cs` | ✅ Done |
| `Enumerations.cs` (×3) | Identical names in different folders | Rename to `ProcessEnumerations.cs`, `ProcessExecutionEnumerations.cs`, `ProcessPlanningEnumerations.cs` — or merge | ✅ Done (merged into `ProcessEnumerations.cs`) |
| `CompiledContracts.cs` | "Compiled" is meaningful only in context | Rename to `QueryCompilationTypes.cs` | — |
| `ProcessStepRegistration.Projection.cs` | Partial file — purpose unclear from name | Rename to `ProcessStepRegistration.Discovery.cs` | ✅ Done |
| `QueryableEventContext.cs` | In `Eventing/` root but specific to Queryable | Move to `Queryable/Eventing/` or merge into `EventContexts.cs` | — |

---

## Part 11 — Final Assessment

### Scores

**Overall Organization: 5/10**  
The project boundary organization is genuinely good. Within those boundaries, the principle of one-type-per-file has been applied uniformly, resulting in dozens of tiny files with no independent value. The `Queryable/Records/` folder naming and the three `Enumerations.cs` files are the clearest organizational failures. The pattern of colocating interface + implementation (already done well in many places) should be applied consistently.

**Overall Discoverability: 4/10**  
A new maintainer must open 6–10 files to understand any single framework concept. The Process eventing system requires 7 file opens to see 4 event types and their factory. The authoring surface for a query context (what attributes to use, what interfaces to implement) is spread across 11 files in two folders. The biggest wins come from consolidating attributes, event types, and query source interfaces — each a small change with significant discoverability improvement.

**Overall Maintainability: 6/10**  
Naming is consistent and the project boundaries are correct. The primary maintainability drag is the cognitive overhead of navigating many small files. Adding a new process event today requires touching 3 files (event class, `KaleidoEventTypes`, `ProcessEventFactory`). After the recommended consolidations, this drops to 1 file (`ProcessEvents.cs`) and 1 method update in `ProcessEventFactory.cs`. Similarly, adding a new process step attribute today requires a new file; after consolidation it's a new class in `ProcessStepAttributes.cs`.

### Summary of Highest-Value Changes (in priority order)

1. Merge three `Enumerations.cs` → `ProcessEnumerations.cs` *(1 file, immediate win)*
2. Consolidate 5 process attributes → `ProcessStepAttributes.cs` *(5→1 file)*
3. Consolidate 6 queryable attributes → `QueryableAttributes.cs` *(6→1 file)*
4. Rename `Queryable/Records/` → `Queryable/Registry/` *(1 rename, major navigation improvement)*
5. Consolidate 5 query source interfaces → `QuerySources.cs` *(5→1 file)*
6. Consolidate process event records → `ProcessEvents.cs` *(6→1 file)*
7. Merge `ProcessStepDefinition.cs` into `ProcessStepRegistry.cs` *(internal types)*
8. Merge `ParticipantRegistryItem.cs` into `ProcessRegistry.cs` *(output shape with producer)*
9. Merge `IProcessStepHandler` + result types → `ProcessStepHandler.cs` *(consumer contract)*
10. Rename `InMemoryProcessContextStore.cs` → `ProcessContextStore.cs` *(naming)*

**Estimated file reduction: ~45 files (196 → ~151)**  
**Estimated cognitive load reduction: High** — most concepts drop from 5–10 file opens to 1–3.
