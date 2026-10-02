# Kaleido.Provider.SQLite

> **This project is a reference implementation, not a production provider.**
>
> - `IProcessorContextStore` is the **contract** your deployment must implement.
> - `SqliteProcessorContextStore` is a **worked example** of that contract — a single-file embedded database that demonstrates correct `ProcessorContext` round-trip fidelity.
> - **Production deployments are expected to write their own implementation** against their real infrastructure (SQL Server, Postgres, Redis, document store, whatever the process actually needs).
> - SQLite's single-writer file model is deliberately unsuitable for the multi-node, multi-instance scenarios durable process state is designed for. Shipping it to production is an anti-pattern.
> - The `Kaleido.Provider.SQLite.UnitTests` suite is the **executable contract specification**: if your implementation passes equivalent round-trip fidelity, update, and multi-instance tests, it satisfies `IProcessorContextStore`.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../Kaleido/README.md`](../Kaleido/README.md)

---

## What lives here

- `SqliteProcessorContextStore` — example SQLite-backed `IProcessorContextStore` implementation
- `SqliteProcessContextStoreServiceCollectionExtensions` — `UseSqliteProcessorContextStore(connectionString)` builder extension

---

## Implementing your own store

The contract is two methods:

```csharp
public interface IProcessorContextStore
{
    Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default);
    Task SaveAsync(ProcessorContext context, CancellationToken cancellationToken = default);
}
```

Your implementation must preserve, with full fidelity:

- `ProcessId`, `ProcessorName`, `LatestRequestId`
- `State` (`ProcessExecutionState` enum — Active, Complete, BusinessFailure, ProcessViolation, HandOff, AwaitingRequiredStep, AwaitingStepSelection, Exception, Canceled)
- `RequiredStep` and `TargetProcessorName` (mutually exclusive continuation markers)
- `AvailableSteps` (collection — order not significant)
- `Steps` — every `StepContext` including `StepName`, `Version`, `Status`, `LatestRequestId`, `LastExecuted`
- `CreatedUtc` / `UpdatedUtc` timestamps

Save semantics are **upsert**: a second `SaveAsync` for the same `ProcessId` replaces the prior state entirely.

`LoadAsync` returns `null` when no process exists — never throws for a missing instance.

---

## Using the example implementation

For samples, demos, and local development:

```csharp
builder.Services.AddKaleido(builder.Configuration, o =>
{
    o.ServiceName = "my-service";
    o.Assemblies = new[] { typeof(Program).Assembly };
})
    .UseSqliteProcessorContextStore("Data Source=my-process.sqlite");
```

This registers `SqliteProcessorContextStore` as `IProcessorContextStore`, replacing the default in-memory store.

The default in-memory store is sufficient for tests and single-request processes; it logs a warning at registration precisely because production without a durable store is a configuration smell.

---

## What this project does NOT do

- it is not a production-grade store (see header)
- the Process runtime itself (see [`Kaleido`](../Kaleido/README.md))
- HTTP endpoint publication (see [`Kaleido.Http`](../Kaleido.Http/README.md))
- other persistence providers (EF Core, Redis, etc.) — write your own per the contract above

---

## Where to look

- `SqliteProcessorContextStore.cs` — the reference `IProcessorContextStore` implementation
- `SqliteProcessContextStoreServiceCollectionExtensions.cs` — `UseSqliteProcessorContextStore(...)` registration
- `tests/Kaleido.Provider.SQLite.UnitTests` — the executable contract specification to mirror for your own store
