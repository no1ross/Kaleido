# Process Architecture

This document describes the current architecture of the Process subsystem in `src/Process`. It is intended for contributors working on the framework itself, not just consumers using it from feature code.

Process is Kaleido's metadata-driven framework for exposing business actions as discoverable process steps with durable participant state, validation, dependency rules, and consistent execution contracts.

The code for Process is currently split into three main projects:

- [`Abstractions`](./Abstractions)
- [`Process`](./Process)
- [`AspNetCore`](./AspNetCore)

---

## 1. Project structure

### [`Abstractions`](./Abstractions)
Contains:
- public attributes for process-step declaration
- participant request/result types
- durable state contracts
- planning and execution contracts
- step registry contracts
- process event contracts
- observability constants

Examples:
- [`ProcessStepAttribute`](./Abstractions/Attributes/ProcessStepAttribute.cs)
- [`DependsOnStepAttribute`](./Abstractions/Attributes/DependsOnStepAttribute.cs)
- [`IParticipantRuntime`](./Abstractions/Participant/IParticipantRuntime.cs)
- [`IProcessContextStore`](./Abstractions/Participant/Context/IProcessContextStore.cs)
- [`IProcessStepHandler`](./Abstractions/Participant/Execution/IProcessStepHandler.cs)
- [`IProcessStepRegistry`](./Abstractions/Participant/Registry/IProcessStepRegistry.cs)

### [`Process`](./Process)
Contains:
- assembly scanning and step registration
- handler discovery and validation
- step registry construction
- request planning and candidate building
- step execution and state mutation
- event publishing and observability
- default in-memory context storage

Examples:
- [`ParticipantServiceCollectionExtensions`](./Process/ParticipantServiceCollectionExtensions.cs)
- [`ProcessStepRegistry`](./Process/Participant/Registry/ProcessStepRegistry.cs)
- [`ParticipantRuntime`](./Process/Participant/ParticipantRuntime.cs)
- [`ExecutionPlanner`](./Process/Participant/Planning/ExecutionPlanner.cs)
- [`ExecutionProcessor`](./Process/Participant/Execution/ProcessExecutor.cs)
- [`ProcessStateUpdater`](./Process/Participant/Context/ProcessStateUpdater.cs)

### [`AspNetCore`](./AspNetCore)
Contains:
- HTTP request/response contracts
- route generation helpers
- endpoint mapping
- execution/state transport services
- ASP.NET Core OpenTelemetry integration

Examples:
- [`ProcessAspNetCoreServiceCollectionExtensions`](./AspNetCore/ProcessAspNetCoreServiceCollectionExtensions.cs)
- [`ProcessEndpointRouteBuilderExtensions`](./AspNetCore/ProcessEndpointRouteBuilderExtensions.cs)
- [`ProcessStepResponse`](./AspNetCore/Contracts/ProcessStepResponse.cs)
- [`ProcessExecutionResponse`](./AspNetCore/Contracts/ProcessExecutionResponse.cs)

---

## 2. Core concepts

### Process Step
A process step is a named business action represented by a CLR type decorated with [`ProcessStepAttribute`](./Abstractions/Attributes/ProcessStepAttribute.cs).

A step primarily defines:
- identity (`Name`, `Version`)
- display/documentation values
- the request shape for that business action
- validation attributes on its public properties

At runtime, a step is represented by:
- `ProcessStepRegistration`
- `ProcessStepMetadata`

See [`ProcessStepRegistration.cs`](./Abstractions/Participant/Registry/ProcessStepRegistration.cs).

### Step Relationships
A step can declare graph and availability semantics using attributes:
- [`DependsOnStepAttribute`](./Abstractions/Attributes/DependsOnStepAttribute.cs)
- `AvailableAfterAttribute`
- `AvailableUntilAttribute`
- `RepeatableAttribute`

These attributes define:
- prerequisites that must be completed first
- steps that become available only after another step completes
- steps that stop being available once another step completes
- whether a previously completed step may be executed again

### Step Handler
A handler executes the business behavior for a single step.

The framework supports:
- [`IProcessStepHandler<TProcessStep>`](./Abstractions/Participant/Execution/IProcessStepHandler.cs)
- [`IProcessStepHandler<TProcessStep, TProcessStepResult>`](./Abstractions/Participant/Execution/IProcessStepHandler.cs)

This means a step may:
- execute without returning a typed payload
- or execute and return a typed result object that flows into runtime and HTTP response contracts

### Participant Request
A participant request is the runtime input model for Process execution.

It consists of:
- optional `ProcessId`
- required `RequestId`
- `ParticipantRequest`, which contains a case-insensitive dictionary of submitted step names to raw request values

See [`IParticipantRuntime.cs`](./Abstractions/Participant/IParticipantRuntime.cs).

### Participant Context
A participant context is the durable state of a process instance.

It contains:
- process identity
- current process state
- required next step if any
- currently available next steps
- per-step execution summaries
- timestamps

Important distinction:
- this is resumable current state
- it is not intended to hold full operational history

Historical evidence is emitted through process events rather than stored in the durable context.

See [`IProcessContextStore.cs`](./Abstractions/Participant/Context/IProcessContextStore.cs).

### Step Candidate
A step candidate is the planning-time representation of a submitted step.

A candidate holds:
- submitted step name
- resolved registration metadata
- hydrated step instance
- candidate status
- whether it is included in the execution plan
- runtime and validation messages

See [`StepCandidate.cs`](./Abstractions/Participant/Planning/StepCandidate.cs).

### Execution Decision
After a step executes, the runtime produces an execution decision that determines what happens next.

Current decisions include:
- `Continue`
- `Complete`
- `BusinessFailure`
- `ProcessViolation`
- `AwaitingRequiredStep`
- `AwaitingStepSelection`

See [`ExecutionDecision.cs`](./Abstractions/Participant/Execution/ExecutionDecision.cs).

### Registry
Process now has two related registry layers.

`IProcessStepRegistry` is the runtime metadata index for execution/planning.

It supports:
- lookup by step name
- lookup by step CLR type
- enumeration of all registered steps
- enumeration of initial steps that can begin a process

`IParticipantRegistry` is the participant-facing discovery registry.

It projects participant metadata plus step metadata into transport-neutral discovery records that can be consumed by ASP.NET Core, the Kaleido registry, and downstream OpenAPI generation.

See:
- [`IProcessStepRegistry`](./Abstractions/Participant/Registry/IProcessStepRegistry.cs)
- [`IParticipantRegistry`](./Abstractions/Participant/Registry/IParticipantRegistry.cs)

---

## 3. Public framework contracts

### [`IParticipantRuntime`](./Abstractions/Participant/IParticipantRuntime.cs)
This is the core runtime entry point for process execution.

```csharp
Task<ParticipantProcessResult> ExecuteAsync(
    ProcessRequest request,
    CancellationToken cancellationToken = default)
```

The runtime owns:
- context loading/creation
- planning
- execution
- state persistence
- final result shaping

### [`IProcessContextStore`](./Abstractions/Participant/Context/IProcessContextStore.cs)
This abstraction owns durable participant state persistence.

```csharp
Task<ParticipantContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default);
Task SaveAsync(ParticipantContext context, CancellationToken cancellationToken = default);
```

Use this to replace the default in-memory store when a process must survive service restarts or be shared across instances.

### [`IProcessStepHandler<TProcessStep>` / `IProcessStepHandler<TProcessStep, TProcessStepResult>`](./Abstractions/Participant/Execution/IProcessStepHandler.cs)
These interfaces define the business handler contract for a step.

Handlers receive:
- the typed step request object
- a `ProcessStepContext`
- a cancellation token

Handlers do not own planning, dependency evaluation, or durable state mutation.

### [`IProcessStepRegistry`](./Abstractions/Participant/Registry/IProcessStepRegistry.cs)
This is the execution/planning registry for registered steps.

It is used by:
- planning
- execution
- HTTP step-name/type resolution

### [`IParticipantRegistry`](./Abstractions/Participant/Registry/IParticipantRegistry.cs)
This is the discovery/metadata registry for registered participants.

It is used by:
- participant catalog publication
- full participant registry publication
- per-step metadata publication through projected participant step records
- registry consumers such as the Kaleido registry and downstream OpenAPI generation

---

## 4. Registration model

### [`AddParticipant`](./Process/ParticipantServiceCollectionExtensions.cs)
`AddParticipant(...)` is the main Process registration entry point.

It requires that at least one assembly has already been registered on the outer Kaleido builder.

The registration flow is:
1. validate participant metadata from `ParticipantOptions` (`Name`, `Description`, `Version`, `DisplayName`)
2. scan registered assemblies for candidate classes
3. keep types decorated with `[ProcessStep]`
4. apply the optional `ParticipantOptions.TypeFilter`
5. validate discovered step metadata
6. validate duplicate step names
7. resolve and register exactly one handler for each step
8. construct the runtime step registry
9. construct the participant discovery registry
10. register framework services

### Assembly scanning
The runtime scans all registered assemblies and considers non-abstract classes, including internal/non-public step or handler types that live in the scanned assemblies.

### Step validation during registration
Registration currently enforces:
- participant must have a non-empty name
- participant must have a non-empty version
- participant must have a non-empty display name
- at least one step must be discovered
- every step must have a non-empty name
- every step must have a non-empty version
- step names must be unique
- every step must have exactly one matching handler

### Registry construction passes
[`ProcessStepRegistry`](./Process/Participant/Registry/ProcessStepRegistry.cs) builds registrations in multiple passes:

1. discover each step's handler type, result type, and core metadata
2. hydrate dependency and availability references
3. validate the definitions
4. produce immutable registrations and lookup dictionaries

This multi-pass structure is important because relationship attributes reference other step types.

---

## 5. Registries and metadata

### [`IProcessStepRegistry`](./Abstractions/Participant/Registry/IProcessStepRegistry.cs)
The runtime registry exposes:
- `Registrations`
- `InitialRegistrations`
- lookup by name
- lookup by type

`InitialRegistrations` are the steps that have:
- no dependencies
- no `AvailableAfter` requirements

### [`ProcessStepRegistration`](./Abstractions/Participant/Registry/ProcessStepRegistration.cs)
A registration contains:
- `StepType`
- `StepResultType`
- `HandlerType`
- `Dependencies`
- `AvailableAfter`
- `AvailableUntil`
- `Repeatable`
- `Metadata`

### Metadata publication shape
The ASP.NET Core metadata layer projects registrations into [`ProcessStepResponse`](./AspNetCore/Contracts/ProcessStepResponse.cs).

That published metadata currently includes:
- step identity and descriptions
- repeatability
- input field metadata
- dependencies
- availability relationships
- execute URL
- metadata URL

Field metadata is inferred from the step type's public properties using:
- `DataTypeMapper.GetDescriptor(property)`
- `ConstraintMapper.Map(property)`

---

## 6. Validation rules

### Registration validation
During startup, Process validates:
- missing steps
- missing name/version metadata
- duplicate names
- missing handlers
- multiple handlers

See [`ParticipantServiceCollectionExtensions.cs`](./Process/ParticipantServiceCollectionExtensions.cs).

### Graph validation
The registry validation layer prevents:
- self-referencing dependencies
- self-referencing `AvailableAfter`
- self-referencing `AvailableUntil`
- circular dependency chains

See [`RegistrationValidator.cs`](./Process/Participant/Registry/RegistrationValidator.cs).

### Request and payload validation
During execution planning, Process validates:
- unknown submitted step names
- invalid step payload hydration
- `DataAnnotations` on hydrated step objects
- dependency consistency against history or submitted candidates

See:
- [`StepCandidateBuilder`](./Process/Participant/Planning/StepCandidateBuilder.cs)
- [`StepCandidateValidator`](./Process/Participant/Planning/StepCandidateValidator.cs)
- [`StepCandidateConsistencyChecker`](./Process/Participant/Planning/StepCandidateConsistencyChecker.cs)

### Historical and repeatability validation
A previously completed step:
- is treated as satisfied if it is not repeatable
- remains eligible for execution if it is repeatable

This logic is part of candidate consistency checking.

---

## 7. Execution flow

The runtime entry point is [`IParticipantRuntime.ExecuteAsync`](./Abstractions/Participant/IParticipantRuntime.cs), implemented by [`ParticipantRuntime`](./Process/Participant/ParticipantRuntime.cs).

The execution flow is:

1. validate the outer request shell
2. load or create participant context
3. build step candidates from submitted values
4. validate candidate payloads
5. validate candidate/history/dependency consistency
6. order executable candidates by dependency graph
7. publish planning events
8. execute eligible candidates
9. update and persist participant state after each execution
10. publish completion events
11. return participant-facing execution results

### 7.1 Context load or creation
If `ProcessId` is not supplied, the runtime initializes a new process instance.

If `ProcessId` is supplied:
- the context store is queried
- a new context is initialized if none exists
- otherwise the context is reconciled against the current registry

This reconciliation step allows newly registered steps or version updates to flow into existing saved process instances.

### 7.2 Candidate building
Submitted steps are converted into `StepCandidate` instances.

For each submitted step:
- the registry is consulted by step name
- unknown steps become invalid candidates with runtime messages
- raw values are serialized and deserialized into the declared step CLR type

### 7.3 Candidate validation and consistency
Candidates are then passed through:
- `StepCandidateValidator` for object validation via `DataAnnotations`
- `StepCandidateConsistencyChecker` for repeatability, historical completion, and dependency checks

Only candidates in the `Built` state are considered executable.

### 7.4 Candidate ordering
Executable candidates are ordered by dependencies using `StepCandidatePlanner`.

Candidates included in the plan are marked `IncludedInExecutionPlan = true`.

Non-executable candidates remain in the returned result for transparency, but they are not executed.

### 7.5 Step execution loop
[`ExecutionProcessor`](./Process/Participant/Execution/ProcessExecutor.cs) executes planned candidates one at a time.

For each candidate it:
1. resolves current step context from participant state
2. computes available next steps relative to current completion history
3. invokes the business handler through the step invoker
4. evaluates the execution decision
5. updates participant state
6. persists participant context
7. publishes a step-completed event
8. either continues to another candidate or stops

### 7.6 Failure and cancellation behavior
If a step is canceled or throws an exception:
- the runtime updates participant state accordingly
- persists the updated state
- records runtime messages
- stops further execution for the current request

### 7.7 Final result shaping
The runtime merges:
- planning status
- execution status
- response payloads
- runtime messages
- business messages
- required next step
- available next steps

into `ParticipantProcessResult`.

---

## 8. ASP.NET Core transport layer

### [`AddParticipantAspNetCore`](./AspNetCore/ProcessAspNetCoreServiceCollectionExtensions.cs)
This extension adds the HTTP transport layer for Process.

It currently:
- requires `AddParticipant()` to have been called first
- registers route options
- adds routing and `IHttpContextAccessor`
- registers execution and state services

### [`MapParticipant`](./AspNetCore/ProcessEndpointRouteBuilderExtensions.cs)
This extension publishes the HTTP endpoint set for Process under the configured route prefix.

It maps:
- participant catalog endpoint
- execute endpoint
- process state endpoint
- step catalog endpoint
- full step registry endpoint
- per-step metadata endpoints
- per-step execution endpoints

### Request contracts
The transport layer uses request contracts such as:
- [`ExecuteProcessRequest`](./AspNetCore/Contracts/ExecuteProcessRequest.cs)
- `ExecuteStepRequest<TProcessStep>`

These contracts are adapted into runtime `ProcessRequest` values by [`ProcessExecutionService`](./AspNetCore/Srevices/ProcessExecutionService.cs).

### Response contracts
The main response contracts are:
- [`ProcessExecutionResponse`](./AspNetCore/Contracts/ProcessExecutionResponse.cs)
- [`StepExecutionResponse`](./AspNetCore/Contracts/ProcessExecutionResponse.cs)
- [`ProcessStepResponse`](./AspNetCore/Contracts/ProcessStepResponse.cs)
- [`ProcessStateResponse`](./AspNetCore/Contracts/ProcessStateResponse.cs)

### Header behavior
The execution service writes the resolved `ProcessId` into the response headers so clients can continue the same process instance in later requests.

---

## 9. Discovery and metadata publishing

The discovery model is step-centric.

### Participant catalog endpoint
The participant catalog returns only initial steps that can be used to begin a new process instance.

This endpoint is intended as the entry-point discovery surface for clients that need to know how a process can start.

### Step catalog endpoint
The step catalog returns a lightweight summary for every registered step, including links to metadata and execution endpoints.

### Full registry endpoint
The full registry returns the complete set of published step metadata for all registered steps.

This is intended for:
- application startup
- process explorers
- dynamic clients
- local metadata caching

### Per-step metadata endpoint
Each registered step also gets an individual metadata endpoint that returns field metadata, constraints, relationship links, and execution URLs.

### Endpoint URL generation
Published metadata and execute URLs are generated through [`ProcessContractUrls`](./AspNetCore/Contracts/ProcessContractUrls.cs) and route helpers in [`ProcessRoutePaths`](./AspNetCore/Contracts/ProcessRoutePaths.cs).

---

## 10. Observability

Process has a dedicated observability layer in [`ProcessObservability`](./Process/Observability/ProcessObservability.cs).

### Activity source and meter
Observability is published through:
- `ActivitySource`
- `Meter`

using names defined in [`ProcessTelemetry`](./Abstractions/Observability/ProcessTelemetry.cs).

### Execution-level signals
The runtime records:
- process execution count
- execution failure count
- process contexts initialized
- process contexts loaded
- submitted step count
- plan candidate count
- executable candidate count

### Step and handler signals
The runtime also records:
- step execution count
- step cancellation count
- step failure count
- handler execution count
- handler failure count

### Correlation tags
Execution activities include Kaleido correlation tags such as:
- request id
- process id
- participant id
- participant instance id
- orchestrator id
- orchestrator instance id

---

## 11. Developer guidance

### When to use Process
Use Process when a capability is a business action rather than a passive query surface.

Process is a good fit when you need:
- durable progress across multiple requests
- step dependencies or ordering rules
- discoverable action metadata
- consistent validation and execution contracts
- next-step guidance for clients

### When to model a single step
Use a single process step when the action can be validated and executed independently but still benefits from discoverability, uniform contracts, or durable state.

### When to model multiple related steps
Use multiple related steps when:
- one action must happen before another
- available actions depend on prior execution history
- the business flow can pause and resume later
- the consumer needs guidance on what can happen next

### When to return a typed step result
Use `IProcessStepHandler<TStep, TResult>` when the consumer needs a structured response payload from the step.

Use `IProcessStepHandler<TStep>` when the business action primarily advances state and no explicit result payload is required.

### Persistence guidance
Replace the default in-memory context store when process state must persist across service restarts or multiple application instances.

---

## 12. Contributor cautions

### 1. Process is step-centric, not query-centric
Do not import Queryable terminology such as direct contexts, local views, or delegated views into Process documentation or runtime changes unless the underlying model actually changes.

### 2. `ParticipantContext` is not a full audit log
Keep durable state limited to resumable current state.

Historical evidence belongs in emitted process events, not in the saved participant context.

### 3. Registration invariants matter
Startup validation assumes:
- unique step names
- exactly one handler per step
- valid relationship graphs

Changes to scanning or handler resolution ripple through execution, metadata endpoints, and state reconciliation.

### 4. State reconciliation is important
Existing saved contexts are reconciled against the current registry, including step version updates and newly registered steps.

Be careful not to break backward compatibility for existing process instances.

### 5. HTTP services should stay thin
The ASP.NET Core layer should adapt contracts and publish endpoints, not reimplement planning or business execution rules that belong in the runtime.

### 6. Naming is not fully settled
The current subsystem mixes the terms Process, Participant, and Step across types and APIs. New documentation and code should be careful to define terms explicitly rather than assuming they are self-evident.

---

## 13. Glossary

### Process
A business capability exposed through one or more discoverable steps and executed against durable participant state.

### Participant
The runtime execution model and durable state owner for a process instance.

### Process Step
A named business action declared by a CLR type with `ProcessStepAttribute`.

### Step Handler
A DI-resolved component that executes the business behavior for a specific step.

### Participant Request
A runtime request that submits one or more steps for a new or existing process instance.

### Participant Context
The durable current state of a process instance.

### Step Candidate
A planning-time representation of a submitted step, including hydrated request data, status, and messages.

### Execution Decision
The runtime decision that determines whether execution continues, completes, fails, or waits for more caller input.

### Registry
The runtime metadata index of registered process steps.

### Initial Step
A step that can begin a process because it has no dependency or `AvailableAfter` prerequisite.

### Available Step
A step the consumer may submit next based on current process history and availability rules.

---

## 14. Areas worth documenting further later

The current architecture document should be enough for contributors to understand the main runtime model, but a few areas likely deserve deeper follow-up docs later:

1. the exact decision-evaluation rules in `StepExecutionEvaluator`
2. provider guidance for durable `IProcessContextStore` implementations
3. more precise semantics for `AvailableAfter` vs `DependsOnStep`
4. typed vs untyped step result guidance for API design
5. client guidance for using participant catalog vs step catalog vs full registry
6. the intended long-term relationship between Process terminology and Participant terminology

---

## 15. Practical summary

If you are new to Process, the simplest accurate mental model is:

1. declare a step type with `[ProcessStep]`
2. implement exactly one handler for it
3. register assemblies and call `AddParticipant()`
4. let the runtime build a registry and manage state
5. submit one or more steps through `IParticipantRuntime` or the HTTP transport layer
6. let the framework validate, order, execute, persist, and publish next-step guidance

Process is therefore not just a command endpoint helper. It is a stateful, metadata-driven business action framework with built-in discovery, planning, execution, and transport conventions.
