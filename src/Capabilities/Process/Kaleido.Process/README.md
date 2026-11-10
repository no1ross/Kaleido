# Kaleido.Process

The Process capability owns step and handler declarations, registration, planning, execution, resumable state, recovery, process events and runtime telemetry. It depends on the transport-free [`Kaleido` foundation](../../../Kaleido/README.md); Eventing remains there until its own extraction. Process does not own Queryable, the control registry, transport request parsing or a persistence technology.

Call `AddKaleido(configuration, options)` with explicit assemblies and then `AddProcess()` on its builder. The Process owner scans step types, validates their declared relationships and handlers, registers the planner and runtime, and supplies the process-state and idempotency store defaults. `AddContextStore<T>()`, `AddIdempotencyStore<T>()` and `AddTransactions<T>()` replace those defaults; choose in-memory stores explicitly for development. The outbox store and relay remain Eventing's responsibility. `AddProcessEventDispatch()` registers typed Process event delivery separately for relay-only hosts with no steps.

`IProcessStep`, `[ProcessStep]`, `IProcessStepHandler<TStep>` and `ProcessStepHandlerResult` are the consumer declaration and result contracts. `ProcessorContext` is current resumable state, not an audit log; the event stream records what happened. State saves and transition events share a persistence transaction when the host provides one. `IKaleidoContextStore`, `IKaleidoIdempotencyStore` and `IKaleidoTransactions` are provider seams, not implementations. An in-memory default is not a production persistence choice.

## Execution and boundaries

Process is step-centric: declare an `IProcessStep` type with `[ProcessStep]` metadata and exactly one handler, then register its assembly with `AddProcess()`. A step's public name is its unmodified `Type.Name` in state, events and transport requests. Handlers name the next step by type; names are produced at the boundary. `StepCandidateBuilder`, `StepCandidateValidator`, `StepCandidateConsistencyChecker`, `StepCandidatePlanner` and `StepCandidateNextStepChecker` prepare the plan before `ProcessorExecutor` invokes handlers. Its `ProcessorTransition` saves state and its event together when the selected provider supplies a shared transaction. Recovery holds an outcome whose save failed after its handler ran.

`ProcessEventTypes` declares the stable Process event discriminators beside their event payload types. Eventing provides the generic envelope/outbox/relay, not a catalog of Process events. The Process step registry knows this host's steps; it does not assemble or query the central Registry capability.

This is the shallow extraction: existing `Kaleido.Processor.*` namespaces and the project's `<RootNamespace>Kaleido</RootNamespace>` are **temporary**. The deep Process review in #589 must remove that override and align names, tests, APIs and docs with the final architecture. The current idempotency replay record also retains its HTTP-shaped fields pending #587; it is not claimed to be a finished neutral contract.

For broader context see [source architecture](../../../ARCHITECTURE.md), [step/handler patterns](../../../../docs/PATTERNS.md) and the [information-request model](../../../../docs/INFORMATION_REQUESTS.md).
