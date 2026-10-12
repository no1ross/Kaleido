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
- `SqliteProcessorContextStoreServiceCollectionExtensions` — `UseSqliteProcessorContextStore(connectionString)` builder extension

---

## Implementing your own store

The contract is four methods:

```csharp
public interface IProcessorContextStore
{
    Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default);
    Task<ProcessorContext> SaveAsync(ProcessorContext context, CancellationToken cancellationToken = default);

    Task<IdempotentResult?> FindResultAsync(string key, CancellationToken cancellationToken = default);
    Task SaveResultAsync(IdempotentResult result, CancellationToken cancellationToken = default);
}
```

### Process state

Your implementation must preserve, with full fidelity:

- `ProcessId`, `ProcessorName`, `LatestRequestId`, `Owner`
- `Version` - the optimistic-concurrency version (see below)
- `State` (`ProcessExecutionState` enum - Active, Complete, BusinessFailure, ProcessViolation, HandOff, AwaitingRequiredStep, AwaitingStepSelection, Exception, Canceled, AwaitingInformation)
- `RequiredStep` and `TargetProcessorName` (mutually exclusive continuation markers)
- `RequiredInformationRequest` - the pending information request for `RequiredStep`, stored whole (the SQLite store keeps it as JSON on the required-step row)
- `AvailableSteps` (collection - order not significant)
- `Steps` - every `StepContext` including `StepName`, `Version`, `Status`, `LatestRequestId`, `LastExecuted`
- `CreatedUtc` / `UpdatedUtc` timestamps

`LoadAsync` returns `null` when no process exists - never throws for a missing instance.

### Saving: optimistic concurrency

`SaveAsync` is **conditional**, so two concurrent requests for the same process can never overwrite each other:

- Save only when the stored version still equals `context.Version`. A `Version` of `0` means a new process: save only when no process with that id exists.
- Store the context with `Version + 1` and **return the saved context** (with the new version) - the runtime continues with it.
- Otherwise throw `KaleidoConcurrencyException(context.ProcessId)` and save nothing. The caller gets **409** `pro_process_conflict`.

The compare and the write must be one step: a row version / ETag / `WHERE Version = @expected` in the `UPDATE`, a conditional put, or a lock - whatever your storage offers. The SQLite store uses an EF Core concurrency token on a `Version` column (the `UPDATE` is conditional on the version the row was read with) and turns a duplicate insert of a new process into a conflict too.

### Idempotent results

`FindResultAsync` / `SaveResultAsync` keep the answers to execute requests sent with an `Idempotency-Key`, so a retry gets the stored answer instead of running the step again ([`Kaleido.Http`](../Kaleido.Http/README.md#idempotent-execution-idempotency-key)). Keep them **next to the process state, in the same database** - no extra store for the integrator:

- `FindResultAsync` returns the stored `IdempotentResult` for the key, or `null` when there is none or it has expired (`ExpiresUtc`).
- `SaveResultAsync` stores it, replacing an earlier answer for the same key. Delete expired answers here (or in a background job) so they do not pile up.
- Keep `Key`, `RequestHash`, `StatusCode`, `Response` (the JSON answer - plan for responses of several KB) and `ExpiresUtc` as given.

The SQLite store keeps them in its own `IdempotentResults` table (`Key` primary key, an index on `ExpiresUtc`) and deletes expired rows whenever a new answer is saved.

### Schema changes

The SQLite store creates its schema with `EnsureCreated`, which does not migrate an existing database. **Recreate development databases created before** the `Version` column and the `IdempotentResults` table existed (delete the file; it is recreated at startup). A production store manages its own schema migrations.

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

Malformed, non-blank connection strings fail at registration with `KaleidoConfigurationException` (`SqliteErrorCodes.InvalidConnectionString`, `invalid_connection_string`) before DI services are replaced. The parser checks connection-string syntax only: registration does **not** open or create a database, and reachability remains a separate health-check concern.

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
- `SqliteProcessorContextStoreServiceCollectionExtensions.cs` — `UseSqliteProcessorContextStore(...)` registration
- `tests/Kaleido.Provider.SQLite.UnitTests` — the executable contract specification to mirror for your own store
