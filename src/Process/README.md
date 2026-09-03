# Process

Process is Kaleido's metadata-driven framework for exposing business actions as discoverable process steps with durable state, validation, dependency rules, and consistent execution contracts.

It is organized into three main projects:

- [`Abstractions`](./Abstractions/README.md) — public step attributes, processor request/result contracts, handler interfaces, durable state contracts, registry contracts, event contracts, and shared observability constants
- [`Process`](./Process/README.md) — runtime registration, step registry construction, planning, execution, state mutation, persistence integration, and observability
- [`AspNetCore`](./AspNetCore/README.md) — HTTP request/response contracts, route publishing, execution/state endpoints, and transport adaptation

For the full subsystem model, see:
- [`ARCHITECTURE.md`](./ARCHITECTURE.md)
- [`AGENTS.md`](./AGENTS.md)

## Execution model

Process is step-centric.

A consumer submits one or more process steps for a new or existing process instance. The runtime then:
- loads or initializes processor state
- builds and validates submitted step candidates
- evaluates dependency and repeatability rules
- orders executable steps
- executes handlers
- persists updated state
- returns step results plus next-step guidance

## Getting started

Process consumer setup follows a simple pattern:

1. define one or more process step types
2. implement exactly one handler for each step
3. register the assemblies that contain those types
4. call `AddProcessor(...)` and supply processor metadata
5. optionally configure durable state storage
6. optionally call `AddProcessorAspNetCore(...)` and map endpoints with `MapProcessor()`

## How a developer uses Process

### 1. Register Process in your application
At minimum, register the assemblies that contain your process steps and handlers, then call `AddProcessor(...)` with processor metadata.

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MyDbContext).Assembly)
    .AddProcessor(options =>
    {
        options.Name = "my-processor";
        options.Description = "My processor workflow.";
        options.Version = "1.0.0";
        options.DisplayName = "My Processor";
    });
```

If you want HTTP discovery and execution endpoints:

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MyDbContext).Assembly)
    .AddProcessor(options =>
    {
        options.Name = "my-processor";
        options.Description = "My processor workflow.";
        options.Version = "1.0.0";
        options.DisplayName = "My Processor";
    })
        .AddProcessorAspNetCore(options =>
        {
            options.RoutePrefix = "my-service";
        });

var app = builder.Build();
app.MapProcessor();
```

This matches the real registration pattern used in the PriorAuth sample. See <ref_snippet file="C:\Repos\Kaleido\samples\PriorAuth\Intake\Program.cs" lines="123-145" />.

### 2. Configure durable process state when needed
By default, Process registers an in-memory `IProcessContextStore`, which is useful for tests and simple local scenarios.

If the process state must survive restarts or be shared across instances, replace that default with a durable store.

Example using the SQLite provider:

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MyProcessStep).Assembly)
    .AddProcessor(options =>
    {
        options.Name = "my-processor";
        options.Description = "My processor workflow.";
        options.Version = "1.0.0";
        options.DisplayName = "My Processor";
    })
        .AddProcessorAspNetCore()
        .UseSqliteProcessContextStore(
            "Data Source=my-process.sqlite");
```

Real example: <ref_snippet file="C:\Repos\Kaleido\samples\ECommerce\NetCoreApp\Program.cs" lines="25-33" />.
Provider extension: <ref_snippet file="C:\Repos\Kaleido\src\Process\Providers\SQLite\SqliteProcessContextStoreServiceCollectionExtensions.cs" lines="8-31" />.

### 3. Define a process step
A process step describes one business action. Use `[ProcessStep]` on the type and decorate public properties with standard validation attributes when the step requires input.

```csharp
[ProcessStep(
    Name = "CaptureRequestedService",
    DisplayName = "Capture Requested Service",
    Description = "Adds a requested service to the current prior authorization.",
    Version = "1.0.0")]
[AvailableAfter(typeof(CaptureMemberStep))]
[AvailableUntil(typeof(CaptureRequestingProviderStep))]
[Repeatable]
public sealed record CaptureRequestedServiceStep
{
    [Required]
    [StringLength(50)]
    public string CodeValue { get; init; } = string.Empty;

    [Required]
    public ProcedureCodeSystem CodeSystem { get; init; }
}
```

Real example: <ref_snippet file="C:\Repos\Kaleido\samples\PriorAuth\Intake.Artifacts\Process\Steps\CaptureRequestedServiceStep.cs" lines="7-23" />.

### 4. Implement a handler for the step
Each discovered step must have exactly one handler.

Handlers implement either:
- `IProcessStepHandler<TStep>`
- `IProcessStepHandler<TStep, TResult>`

Use the typed form when the step should return a structured payload.

```csharp
public sealed class CaptureRequestedServiceHandler(
    IntakeDbContext dbContext,
    ProcedureCodeClient procedureCodeClient,
    ProcedureModalityClient procedureModalityClient,
    QuestionnaireDefinitionClient questionnaireDefinitionClient)
    : IProcessStepHandler<CaptureRequestedServiceStep, CaptureRequestedServiceResponse>
{
    public async Task<ProcessStepHandlerResult<CaptureRequestedServiceResponse>> ExecuteAsync(
        CaptureRequestedServiceStep processStep,
        ProcessStepContext context,
        CancellationToken cancellationToken = default)
    {
        // business behavior
    }
}
```

Real example: <ref_snippet file="C:\Repos\Kaleido\samples\PriorAuth\Intake.Artifacts\Process\Handlers\CaptureRequestedServiceHandler.cs" lines="13-23" />.

### 5. Use step relationships intentionally
Process supports runtime relationship metadata through:
- `DependsOnStep`
- `AvailableAfter`
- `AvailableUntil`
- `Repeatable`

Use these to model:
- prerequisites
- next-step availability windows
- repeatable user actions

These relationships affect both:
- planning/execution behavior
- published metadata/discovery responses

### 6. Choose the right Process shape
Use Process when a capability is a business action rather than a passive query surface.

Use a **single step** when:
- one business action can execute independently
- you still want discoverability, uniform contracts, or resumable state

Use **multiple related steps** when:
- one action must happen before another
- available actions depend on prior execution history
- the business flow can pause and resume later
- the consumer needs guidance on what can happen next

### 7. What Process registers for you
After `AddProcessor(...)` runs, the framework scans the registered assemblies and automatically discovers:
- `[ProcessStep]` types
- matching step handlers
- step dependency and availability relationships

It then builds:
- the runtime step registry used by planning and execution
- the processor registry used by discovery and metadata publication
- the planning/execution services
- the default context store
- observability and eventing services

You do not manually register each step one by one. The assembly scan and registration flow is implemented in <ref_file file="C:\Repos\Kaleido\src\Process\Process\ProcessorServiceCollectionExtensions.cs" />.

### 8. What HTTP endpoints Process can publish
If you add `AddProcessorAspNetCore(...)` and call `MapProcessor()`, Process publishes endpoints for:
- processor catalog grouped by processor
- full step catalog
- full processor registry metadata
- per-step metadata
- process execution
- process state
- per-step execution

The processor catalog returns processor-level entries with metadata, registry URLs, and initial step summaries. The full registry endpoint returns processor registry records with full step metadata, including input constraints and typed-result output field metadata.

See the endpoint publisher in <ref_file file="C:\Repos\Kaleido\src\Process\AspNetCore\ProcessEndpointRouteBuilderExtensions.cs" />.

## Where to look

- Start with [`Abstractions/README.md`](./Abstractions/README.md) for public contracts
- Read [`Process/README.md`](./Process/README.md) for runtime behavior
- Read [`AspNetCore/README.md`](./AspNetCore/README.md) for HTTP/discovery behavior
- Read [`ARCHITECTURE.md`](./ARCHITECTURE.md) for the full architecture
- Read [`AGENTS.md`](./AGENTS.md) before making framework changes
