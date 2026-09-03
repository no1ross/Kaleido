# Process.Abstractions

This project contains the public contracts for the Process subsystem.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../Process/README.md`](../Process/README.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)

## What lives here

This project contains:
- process-step declaration attributes such as [`ProcessStepAttribute`](./Attributes/ProcessStepAttribute.cs)
- relationship attributes such as [`DependsOnStepAttribute`](./Attributes/DependsOnStepAttribute.cs), `AvailableAfterAttribute`, `AvailableUntilAttribute`, and `RepeatableAttribute`
- participant runtime contracts such as [`IParticipantRuntime`](./Participant/IParticipantRuntime.cs)
- step handler contracts such as [`IProcessStepHandler`](./Participant/Execution/IProcessStepHandler.cs)
- durable state contracts such as [`IProcessContextStore`](./Participant/Context/IProcessContextStore.cs)
- registry contracts such as [`IProcessStepRegistry`](./Participant/Registry/IProcessStepRegistry.cs)
- planning and execution result types
- event contracts and observability constants

## What this project is for

Reference this project when you need to:
- define process-step request models
- implement step handlers against shared contracts
- work with runtime request/result types
- define or replace durable process state providers
- consume registry metadata contracts and event types without taking a dependency on the full runtime or ASP.NET Core transport layer

## Key public concepts

### Process steps
Use `[ProcessStep]` to declare a business action and supply its identity/documentation metadata.

### Step relationships
Use relationship attributes to define prerequisites and availability rules between steps.

### Participant runtime
`IParticipantRuntime` is the runtime execution surface for a process request.

### Step handlers
`IProcessStepHandler<TStep>` and `IProcessStepHandler<TStep, TResult>` define the business execution contract for a single step.

### Participant context
`ParticipantContext` represents resumable durable state for a process instance, not full audit history.

## What this project does not do

This project does **not** contain:
- assembly scanning or DI registration
- runtime planning and execution behavior
- state mutation logic
- endpoint mapping
- HTTP contract adaptation

Those live in:
- [`../Process/README.md`](../Process/README.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)

## Typical usage

A service using Process will usually:
1. define steps and handlers against the abstractions in this project
2. register assemblies and call `AddParticipant()` from the runtime project
3. optionally add `AddParticipantAspNetCore(...)` from the transport project

For a full setup example, see the parent README: <ref_file file="C:\Repos\Kaleido\src\Process\README.md" />.
