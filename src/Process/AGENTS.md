# Process Contributor Notes

This file contains contributor guidance for the Process subsystem under `src/Process`.

Read this before changing Process runtime behavior, contracts, endpoint wiring, or metadata publication.

See also:
- [`README.md`](./README.md) when it exists
- [`ARCHITECTURE.md`](./ARCHITECTURE.md)
- [`Process/README.md`](./Process/README.md)

---

## 1. Scope and project boundaries

`src/Process` is the framework area for stateful, metadata-driven business action execution.

The subsystem is split into three main projects:

- `Abstractions`
  - public attributes
  - processor request/result contracts
  - step handler contracts
  - durable state contracts
  - registry contracts
  - event contracts
  - observability constants

- `Process`
  - assembly scanning and registration
  - step registry construction
  - planning and candidate building
  - execution and decision handling
  - state mutation and persistence integration
  - event publishing and observability

- `AspNetCore`
  - HTTP request/response contracts
  - route helpers
  - endpoint publication
  - transport adaptation services

Keep these boundaries clear:
- `Abstractions` should stay transport-agnostic and runtime-agnostic where possible.
- `Process` owns the actual execution model.
- `AspNetCore` should adapt and publish the runtime, not reimplement it.

---

## 2. Canonical mental model

Process is step-centric, not query-centric.

The core model is:
1. declare a process step with `[ProcessStep]`
2. implement exactly one handler for that step
3. register assemblies with `AddProcessor()`
4. let the runtime build a registry and processor state
5. submit one or more steps for a new or existing process instance
6. let the framework validate, plan, execute, persist, and publish next-step guidance

Do not import Queryable terminology like direct contexts, local views, or delegated views into Process docs or code unless the Process model actually changes.

---

## 3. Registration invariants

These rules are fundamental to startup and runtime correctness:

- At least one assembly must be registered before `AddProcessor(...)`.
- Every processor registration must provide a non-empty `Name`.
- Every processor registration must provide a non-empty `Version`.
- Every processor registration must provide a non-empty `DisplayName`.
- At least one process step must be discovered.
- Every `[ProcessStep]` must have a non-empty `Name`.
- Every `[ProcessStep]` must have a non-empty `Version`.
- Process step names must be unique.
- Every discovered step must have exactly one matching handler.
- Relationship graphs must not self-reference.
- Dependency graphs must not be circular.

Be careful when changing:
- assembly scanning
- handler discovery
- step metadata shape
- relationship attributes
- registry construction

Those changes ripple into:
- request planning
- runtime execution
- state reconciliation
- metadata publishing
- HTTP step lookup and endpoint behavior

---

## 4. State model rules

`ProcessorContext` is current resumable state, not an audit log.

That means:
- keep it small and execution-oriented
- store only what is needed to resume or evaluate the process
- do not accumulate full operational history inside the durable context

Historical evidence should be emitted as process events instead.

When modifying processor state behavior:
- preserve compatibility for existing saved process instances when possible
- remember that `Reconcile(...)` updates older contexts to reflect current registrations
- be cautious about renaming step identities or changing step lifecycle assumptions

---

## 5. Planning and execution rules

The runtime flow is intentionally layered:

- `StepCandidateBuilder`
  - resolves submitted step names
  - hydrates raw input into typed step objects

- `StepCandidateValidator`
  - enforces `DataAnnotations`

- `StepCandidateConsistencyChecker`
  - enforces history, dependency, and repeatability consistency

- `StepCandidatePlanner`
  - orders executable candidates by dependencies

- `ExecutionProcessor`
  - invokes handlers
  - evaluates decisions
  - updates state
  - persists context
  - emits step-level events

Keep those responsibilities separated.

In particular:
- endpoint code should not perform business planning
- handlers should not become mini-orchestrators for unrelated steps
- validation should not be duplicated in multiple layers without a reason
- state mutation rules should stay centralized in the runtime/state updater path

---

## 6. Metadata and discovery rules

The registry is the source of truth for published step metadata.

Published metadata currently includes:
- processor identity and descriptions
- processor version and display name
- initial-step summaries grouped by processor
- full step identity and descriptions
- repeatability
- input field metadata
- validation constraints
- typed-result output field metadata
- dependency relationships
- availability relationships
- execute URL
- metadata URL

Input field metadata is inferred from public step properties.
Typed result metadata is inferred from the typed handler result object in the same field-descriptor style used by Queryable `TView` outputs.

If you change:
- step property interpretation
- type mapping
- constraint mapping
- dependency/availability semantics

then also verify the corresponding HTTP metadata responses and registry endpoints.

The discovery model is processor-grouped and step-centric:
- processor catalog exposes processor summaries with grouped initial steps
- step catalog exposes all steps in lightweight form
- full registry exposes processor records with full step metadata
- per-step metadata exposes one detailed step record

---

## 7. ASP.NET Core guidance

The ASP.NET Core layer should stay thin.

Its responsibilities are:
- route configuration
- endpoint publication
- translating HTTP requests into runtime `ProcessRequest` values
- translating runtime results into HTTP contract shapes
- exposing discovery and state endpoints

It should not:
- duplicate planning logic
- own durable state transition rules
- bypass the registry as the discovery source of truth
- silently drift from runtime semantics

When updating endpoint contracts or route behavior:
- verify both process-wide and per-step execution endpoints
- verify processor catalog, step catalog, full registry, and per-step metadata endpoints
- verify response header behavior for `ProcessId`

---

## 8. Typed vs untyped step results

There are two valid handler styles:

- `IProcessStepHandler<TStep>`
  - use when a step primarily advances state and does not need a structured result payload

- `IProcessStepHandler<TStep, TResult>`
  - use when the caller needs a strongly shaped result contract back from the step

When changing typed result behavior:
- verify registry metadata if the response shape matters to clients
- verify per-step execution endpoints
- verify aggregated `ProcessExecutionResponse` payload behavior

---

## 9. Observability and eventing guidance

Process publishes:
- execution-level observations
- step-level observations
- handler-level observations
- process lifecycle events

Do not remove or weaken observability/event hooks casually.

If you change execution flow, review impact on:
- `ProcessCreated`
- `PlanBuilt`
- `StepCompleted`
- `ExecutionCompleted`
- activity names/tags
- metrics and counters

Observability should continue to reflect real runtime boundaries:
- request-level execution
- per-step execution
- per-handler invocation

---

## 10. Contributor cautions

### Naming remains layered
The subsystem currently uses the terms:
- Process
- Processor
- Step

Use them intentionally:
- `Process` for the overall framework and execution lifecycle
- `Processor` for the top-level registered execution surface and discovery identity
- `Step` for the individual business actions within a processor

### `AspNetCore/Srevices` is misspelled
There is currently a `Srevices` folder in `AspNetCore`.
Do not silently normalize references in docs or code reviews without deciding whether the directory itself will be renamed.

### `ProcessMetadataService` appears minimal
There is an interface for metadata service behavior, but endpoint mapping currently relies directly on the registry for most metadata publication.
If you expand metadata services later, keep the layering intentional.

### Public abstractions ripple widely
Changes in `Abstractions` affect:
- runtime planning/execution
- persistence providers
- event payloads
- HTTP contracts
- tests across multiple projects

Treat abstraction changes as broad-impact work.

---

## 11. Verification guidance

Choose verification based on the scope of the change.

### If you change registration or registry behavior
Run:
- Process unit tests covering registration and registry validation
- Process ASP.NET Core functional tests covering discovery endpoints

### If you change planning or execution behavior
Run:
- Process unit tests for candidate building/validation/planning
- runtime execution tests
- ASP.NET Core execution/state functional tests when relevant

### If you change HTTP contracts or endpoint mapping
Run:
- Process ASP.NET Core unit tests
- Process ASP.NET Core functional tests
- any tests that validate metadata contracts or route URLs

### If you change durable state behavior
Run:
- Process runtime tests
- provider tests if using non-default context stores
- functional tests that load existing process state

---

## 12. Good default instincts for Process changes

Prefer these patterns:
- keep handlers focused on one step
- keep endpoint services thin
- keep state updater logic centralized
- keep registry metadata as the discovery source of truth
- keep durable context minimal and resumable
- keep events and observability aligned with actual runtime boundaries

Avoid these patterns unless there is a deliberate redesign:
- bypassing the registry for metadata
- adding HTTP-only execution semantics that runtime does not understand
- embedding audit history into `ProcessorContext`
- weakening step/handler uniqueness guarantees
- importing Queryable concepts into Process terminology
