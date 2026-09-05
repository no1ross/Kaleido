# Process.AspNetCore.Client

This project contains an HTTP client for consuming process step execution endpoints published by `Process.AspNetCore`.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)
- [`../AspNetCore.Abstractions/README.md`](../AspNetCore.Abstractions/README.md)

## What lives here

This project contains:
- [`IKaleidoProcessClient`](./IKaleidoProcessClient.cs) — typed client interface for registry, metadata, state, and step execution
- [`IKaleidoProcessClientFactory`](./IKaleidoProcessClientFactory.cs) — factory interface resolved by registered name
- [`KaleidoProcessClient`](./KaleidoProcessClient.cs) — concrete HTTP client implementation
- [`KaleidoProcessClientException`](./KaleidoProcessClientException.cs) — exception type wrapping non-success HTTP responses
- [`KaleidoProcessClientServiceCollectionExtensions`](./KaleidoProcessClientServiceCollectionExtensions.cs) — `AddProcessClient(name, baseUrl)` builder extension

## What this project is for

Use this project when a service needs to invoke process steps on a remote processor over HTTP.

Typical reasons:
- one processor's handler must signal a required step that lives on another processor
- an orchestrator or gateway needs to drive a remote process step programmatically
- a service needs to participate in a cross-service process flow without owning the target processor's runtime

## Registration

Register a named process client on the Kaleido builder:

```csharp
builder.Services.AddKaleido()
    .AddProcessClient("RemoteProcessor", "https://remote-processor-host")
    .AddProcessClient("Radiology", "https://radiology-service-host", routePrefix: "radiology");
```

The optional `routePrefix` parameter must match the `RoutePrefix` configured on the remote server's `ProcessRouteOptions`. When omitted, no prefix is used (equivalent to `RoutePrefix = ""`).

Multiple named clients can be registered for different remote processors.

## Usage

Inject `IKaleidoProcessClientFactory` and resolve a client by name.

### Get the full registry

Fetches all processor and step metadata from the remote service, including execute and metadata URLs for each step. The registry is lazily fetched and cached for the lifetime of the client instance — subsequent calls return the cached result without a new HTTP request.

```csharp
var registry = await clientFactory
    .GetClient("RemoteProcessor")
    .GetRegistryAsync(cancellationToken);

foreach (var processor in registry)
{
    Console.WriteLine($"{processor.Name}: {processor.Steps.Count} steps");
}
```

### Get metadata for a single step

Fetches the full metadata record for one named step, including fields, constraints, dependency and availability relationships, and execute/metadata URLs.

```csharp
var metadata = await clientFactory
    .GetClient("RemoteProcessor")
    .GetStepMetadataAsync("CaptureMriInfo", cancellationToken);

foreach (var field in metadata.Fields)
{
    Console.WriteLine($"{field.Name} ({field.DataType})");
}
```

### Get process state

Fetches the current state of an existing process instance, including executed steps and available next steps. Returns `null` if no process with that ID exists.

```csharp
var state = await clientFactory
    .GetClient("RemoteProcessor")
    .GetProcessStateAsync(processId, cancellationToken);

if (state is null)
{
    // process not found
}
else
{
    Console.WriteLine($"Required next step: {state.RequiredStep?.StepName}");
}
```

### Untyped step execution

```csharp
public class MyHandler(IKaleidoProcessClientFactory clientFactory)
{
    public async Task<StepExecutionResponse> ExecuteRemoteStepAsync(
        Guid processId,
        CancellationToken cancellationToken)
    {
        return await clientFactory
            .GetClient("RemoteProcessor")
            .ExecuteStepAsync(
                new MyRemoteStep { ... },
                processId: processId,
                cancellationToken: cancellationToken);
    }
}
```

### Typed step execution

For steps that return a structured result payload:

```csharp
var response = await clientFactory
    .GetClient("RemoteProcessor")
    .ExecuteStepAsync<MyRemoteStep, MyRemoteResult>(
        new MyRemoteStep { ... },
        processId: processId,
        cancellationToken: cancellationToken);

var result = response.Result; // typed as MyRemoteResult?
```

## Response shape

Both methods return a `StepExecutionResponse` (or `StepExecutionResponse<TResponse>`), which contains:
- `ProcessId` — the process instance ID (use this to continue the process in later requests)
- `StepName` — the name of the step that was executed
- `Outcome` — the execution decision (`Continue`, `Complete`, `BusinessFailure`, etc.)
- `RequiredStep` — the next required step if the process is waiting for one
- `AvailableSteps` — the steps available to submit next
- `Messages` — runtime and business messages from the step

## Correlation

`KaleidoProcessClient` automatically forwards Kaleido correlation headers on outbound requests.

## Error handling

Non-success HTTP responses throw `KaleidoProcessClientException`. Inspect its properties for the HTTP status and any error body returned by the remote endpoint.

## What this project does not do

This project does **not** contain:
- server-side endpoint mapping or registration
- planning or execution logic
- process state management

Those live in:
- [`../AspNetCore/README.md`](../AspNetCore/README.md)
- [`../Process/README.md`](../Process/README.md)
