# Process.AspNetCore.Abstractions

This project contains the wire contract types for the Process ASP.NET Core transport layer.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)
- [`../AspNetCore.Client/README.md`](../AspNetCore.Client/README.md)

## What lives here

This project contains:
- HTTP request contracts such as [`ExecuteProcessRequest`](./Contracts/ExecuteProcessRequest.cs)
- HTTP execution response contracts such as [`ProcessExecutionResponse`](./Contracts/ProcessExecutionResponse.cs) and [`StepExecutionResponse`](./Contracts/ProcessExecutionResponse.cs)
- HTTP state response contracts such as [`ProcessStateResponse`](./Contracts/ProcessStateResponse.cs)
- HTTP metadata/discovery response contracts such as [`ProcessStepResponse`](./Contracts/ProcessStepResponse.cs), [`ProcessStepSummary`](./Contracts/ProcessStepResponse.cs), and [`ProcessStepInfo`](./Contracts/ProcessStepInfo.cs)

These types are transport-facing but have no dependency on `Microsoft.AspNetCore.App`. That makes them safe to reference from HTTP clients, orchestrators, and other consumers that should not carry server-side ASP.NET Core dependencies.

## What this project is for

Reference this project when you need to:
- deserialize process execution or state responses from HTTP
- issue process step execution requests using the shared request contract shapes
- build a client or tooling layer against the published Process HTTP surface
- consume the HTTP contract types without referencing the full ASP.NET Core server package

## Relationship to Process.AspNetCore

`Process.AspNetCore` is the server-side project. It references this project and uses these types to shape its endpoint inputs and outputs.

`Process.AspNetCore.Client` is the HTTP client project. It also references this project to deserialize responses from remote process endpoints.

The split exists so client code does not inherit a hard dependency on `Microsoft.AspNetCore.App`.

## What this project does not do

This project does **not** contain:
- ASP.NET Core service registration
- endpoint mapping
- request normalization or transport services
- planning or execution logic

Those live in:
- [`../AspNetCore/README.md`](../AspNetCore/README.md)
- [`../Process/README.md`](../Process/README.md)

## Key types

| Type | Purpose |
|------|---------|
| `ExecuteProcessRequest` | HTTP request body for the process-wide execute endpoint |
| `ProcessExecutionResponse` | Response from a process-wide execute call; includes `ProcessId`, `RequiredStep`, `AvailableSteps`, and per-step results |
| `StepExecutionResponse` | Response from a per-step execute call; includes `ProcessId`, `StepName`, `Outcome`, `RequiredStep`, and `AvailableSteps` |
| `StepExecutionResponse<TResponse>` | Typed variant of `StepExecutionResponse` carrying a strongly typed result payload |
| `ProcessStateResponse` | Response from the process state read endpoint |
| `ProcessStepInfo` | Carries `ProcessorName`, `StepName`, `ExecuteUrl`, and `MetadataUrl` for a required or available step |
| `ProcessStepResponse` | Full step metadata record including fields, constraints, relationships, and URLs |
| `ProcessStepSummary` | Lightweight step reference record used in catalogs and relationship lists |
