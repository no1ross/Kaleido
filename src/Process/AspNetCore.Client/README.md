# Process.AspNetCore.Client

This project contains an HTTP client for consuming process step execution endpoints published by `Process.AspNetCore`.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)
- [`../AspNetCore.Abstractions/README.md`](../AspNetCore.Abstractions/README.md)

## What lives here

This project contains:
- [`IKaleidoProcessClient`](./IKaleidoProcessClient.cs) — typed client interface for executing individual process steps
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
    .AddProcessClient("RemoteProcessor", "https://remote-processor-host");
```

Multiple named clients can be registered for different remote processors.

## Usage

Inject `IKaleidoProcessClientFactory` and resolve a client by name.

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
