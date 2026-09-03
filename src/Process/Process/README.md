# Process

This project contains the core runtime for Kaleido Process.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)
- [`../Abstractions/README.md`](../Abstractions/README.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)

## What lives here

This project contains:
- assembly scanning for process steps and handlers
- DI registration for the Process runtime
- runtime step registry construction
- request planning and candidate building
- candidate validation and consistency checking
- step execution and decision handling
- processor state mutation and reconciliation
- event publishing and observability
- the default in-memory process context store

## Main entry point

The main runtime registration entry point is:
- [`AddProcessor`](./ProcessorServiceCollectionExtensions.cs)

This extension:
- scans registered assemblies for `[ProcessStep]` types
- validates discovered steps
- finds and registers handlers
- builds `IProcessStepRegistry`
- registers the runtime services required for planning, execution, state management, eventing, and observability

## How to register the runtime

At minimum:

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MyProcessStep).Assembly)
    .AddProcessor();
```

If your step types or handlers live in separate assemblies, register those assemblies before calling `AddProcessor()`.

Real example: <ref_snippet file="C:\Repos\Kaleido\samples\PriorAuth\Intake\Program.cs" lines="123-131" />.

## Runtime lifecycle

A runtime request flows through these layers:

1. `ProcessorRuntime`
   - validates the outer request
   - loads or initializes processor state
   - builds an execution plan
   - invokes the execution processor
   - publishes process-level events

2. `ExecutionPlanner`
   - builds candidates from submitted step values
   - validates payloads
   - checks dependency/history consistency
   - orders executable candidates

3. `ExecutionProcessor`
   - invokes step handlers
   - evaluates decisions
   - updates and persists processor state
   - publishes step-level events

4. `ProcessStateUpdater`
   - centralizes state initialization, reconciliation, and transition rules

See:
- [`ProcessorRuntime`](./Processor/ProcessorRuntime.cs)
- [`ExecutionPlanner`](./Processor/Planning/ExecutionPlanner.cs)
- [`ExecutionProcessor`](./Processor/Execution/ProcessExecutor.cs)
- [`ProcessStateUpdater`](./Processor/Context/ProcessStateUpdater.cs)

## Step registration rules

The runtime depends on these invariants:
- at least one step must be discovered
- every step must have a non-empty name and version
- step names must be unique
- every step must have exactly one handler
- dependency graphs must be valid

Those rules are enforced during startup and registry construction.

## State storage

By default, the runtime registers `InMemoryProcessContextStore`.

That is useful for:
- tests
- simple local experiments
- scenarios where state does not need to survive restarts

For durable state, replace the default `IProcessContextStore` with a provider-backed implementation.

Example using SQLite:

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MyProcessStep).Assembly)
    .AddProcessor()
        .UseSqliteProcessContextStore(
            "Data Source=my-process.sqlite");
```

Provider extension: <ref_snippet file="C:\Repos\Kaleido\src\Process\Providers\SQLite\SqliteProcessContextStoreServiceCollectionExtensions.cs" lines="8-31" />.

## Typed vs untyped step results

Use:
- `IProcessStepHandler<TStep>` when a step only needs to advance state or report messages
- `IProcessStepHandler<TStep, TResult>` when a step should also return a typed payload to the caller

Real typed-result example: <ref_snippet file="C:\Repos\Kaleido\samples\PriorAuth\Intake.Artifacts\Process\Handlers\CaptureRequestedServiceHandler.cs" lines="13-23" />.

## What this project does not do

This project does **not** own:
- the public contract definitions for steps, state, and handlers
- HTTP endpoint mapping
- transport request/response contracts

Those live in:
- [`../Abstractions/README.md`](../Abstractions/README.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)

## Where to look

- [`ProcessorServiceCollectionExtensions`](./ProcessorServiceCollectionExtensions.cs)
- [`ProcessorRuntime`](./Processor/ProcessorRuntime.cs)
- [`ProcessStepRegistry`](./Processor/Registry/ProcessStepRegistry.cs)
- [`ExecutionPlanner`](./Processor/Planning/ExecutionPlanner.cs)
- [`ExecutionProcessor`](./Processor/Execution/ProcessExecutor.cs)
- [`ProcessStateUpdater`](./Processor/Context/ProcessStateUpdater.cs)
