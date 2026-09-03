# Process.AspNetCore

This project contains the ASP.NET Core transport layer for Kaleido Process.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../Process/README.md`](../Process/README.md)
- [`../Abstractions/README.md`](../Abstractions/README.md)

## What lives here

This project contains:
- HTTP request contracts such as [`ExecuteProcessRequest`](./Contracts/ExecuteProcessRequest.cs)
- HTTP metadata and execution response contracts such as [`ProcessStepResponse`](./Contracts/ProcessStepResponse.cs) and [`ProcessExecutionResponse`](./Contracts/ProcessExecutionResponse.cs)
- route URL/path helpers used by those contracts
- ASP.NET Core registration and endpoint mapping extensions
- transport services that adapt HTTP contracts into runtime `ProcessRequest` values

## What this project is for

Reference this project when you need to:
- expose Process over HTTP
- publish discovery endpoints for process steps
- execute process steps through ASP.NET Core endpoints
- expose process state lookup endpoints
- reuse the shared HTTP contract shapes for Process clients or tooling

## Main entry points

### `AddParticipantAspNetCore(...)`
Adds the ASP.NET Core transport services for Process.

This extension:
- requires `AddParticipant()` to be called first
- registers route options
- adds routing and `IHttpContextAccessor`
- registers execution and state services

### `MapParticipant()`
Publishes the Process endpoint set under the configured route prefix.

This extension maps:
- participant catalog endpoint
- full step catalog endpoint
- full registry endpoint
- per-step metadata endpoints
- process execution endpoint
- process state endpoint
- per-step execution endpoints

See <ref_file file="C:\Repos\Kaleido\src\Process\AspNetCore\ProcessEndpointRouteBuilderExtensions.cs" />.

## How to expose Process over HTTP

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MyProcessStep).Assembly)
    .AddParticipant()
        .AddParticipantAspNetCore(options =>
        {
            options.RoutePrefix = "my-service";
        });

var app = builder.Build();
app.MapParticipant();
```

Real example: <ref_snippet file="C:\Repos\Kaleido\samples\PriorAuth\Intake\Program.cs" lines="123-145" />.

## Discovery and metadata endpoints

The transport layer publishes a step-centric discovery model.

### Participant catalog
Returns only the initial steps that can begin a new process instance.

### Step catalog
Returns a lightweight summary of all registered steps.

### Full registry
Returns full metadata for all registered steps.

### Per-step metadata
Returns detailed metadata for one step, including:
- input fields
- constraints
- dependencies
- availability relationships
- execute URL
- metadata URL

## Execution endpoints

The transport layer supports:
- a process-wide execute endpoint that can submit one or more steps in one request
- per-step execution endpoints for a specific typed step request shape

Execution requests are translated into runtime `ProcessRequest` values by `ProcessExecutionService`.

Responses include the resolved `ProcessId`, which is also written into the response headers so the client can continue the same process instance later.

## Process state endpoint

The state endpoint is read-only.

It returns the current durable process state, including:
- overall process state
- required next step if any
- available next steps
- per-step execution status summaries

It does not execute a step.

## Relationship to the core runtime

This project does not contain the planning or execution model itself.

That lives in the core runtime project.

This project is responsible for:
- HTTP transport contracts
- endpoint publishing
- route generation
- adapting HTTP requests to runtime calls
- adapting runtime results back to HTTP responses

## Where to look

- [`ProcessAspNetCoreServiceCollectionExtensions`](./ProcessAspNetCoreServiceCollectionExtensions.cs)
- [`ProcessEndpointRouteBuilderExtensions`](./ProcessEndpointRouteBuilderExtensions.cs)
- [`ProcessStepResponse`](./Contracts/ProcessStepResponse.cs)
- [`ProcessExecutionResponse`](./Contracts/ProcessExecutionResponse.cs)
- [`ProcessStateResponse`](./Contracts/ProcessStateResponse.cs)
