# Kaleido.Provider.SQLite

> **This project is a reference implementation, not a production provider.**
>
> - `IKaleidoContextStore`, `IKaleidoOutboxStore` and `IKaleidoIdempotencyStore` are the **contracts** your deployment must implement.
> - `SqliteKaleidoContextStore`, `SqliteKaleidoOutboxStore` and `SqliteKaleidoIdempotencyStore` are a **worked example** of those contracts on one database — a single-file embedded database that demonstrates correct `ProcessorContext` round-trip fidelity.
> - **Production deployments are expected to write their own implementation** against their real infrastructure (SQL Server, Postgres, Redis, document store, whatever the process actually needs).
> - SQLite's single-writer file model is deliberately unsuitable for the multi-node, multi-instance scenarios durable process state is designed for. Shipping it to production is an anti-pattern.
> - The `Kaleido.Provider.SQLite.UnitTests` suite is the **executable contract specification**: if your implementation passes equivalent round-trip fidelity, update, and multi-instance tests, it satisfies the contracts.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../Kaleido/README.md`](../Kaleido/README.md)

---

## What lives here

- `SqliteKaleidoContextStore` — example SQLite-backed `IKaleidoContextStore` implementation (process state; writes a save's events to the outbox table in the same transaction)
- `SqliteKaleidoOutboxStore` — example `IKaleidoOutboxStore` implementation (the event outbox, same database)
- `SqliteKaleidoIdempotencyStore` — example `IKaleidoIdempotencyStore` implementation (idempotent requests, same database)
- `SqliteKaleidoContextDbContext` — the one EF Core context all three share
- `SqliteKaleidoContextStoreServiceCollectionExtensions` — `UseSqliteKaleidoContextStore(connectionString)` builder extension

---

## Implementing your own store

A Kaleido service has three store contracts, one concern each ([ADR 0020](../../docs/decisions/0020-three-store-contracts.md)). `AddKaleido()` registers an in-memory default for each (every service gets them, so a queryable-only service uses the outbox too, for its query events); register your own after `AddKaleido()` to replace them.

**The rule that ties two of them together:** `IKaleidoContextStore.SaveAsync` writes the events that describe a save to the outbox **in the same transaction** as the state, so your context store and outbox store must share their database (e.g. one `DbContext`) and are replaced together. The idempotency store needs no shared transaction; keeping it in the same database simply means nothing extra to set up.

```csharp
public interface IKaleidoContextStore
{
    Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken = default);
    Task<ProcessorContext> SaveAsync(ProcessorContext context, IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken = default);
}

public interface IKaleidoIdempotencyStore
{
    Task<IdempotentResult?> AdmitAsync(IdempotentResult admission, CancellationToken cancellationToken = default);
    Task CompleteAsync(IdempotentResult result, CancellationToken cancellationToken = default);
    Task ReleaseAsync(string key, CancellationToken cancellationToken = default);
}

public interface IKaleidoOutboxStore
{
    Task EnqueueEventsAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken = default);
    Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken = default);
}
```

### Process state

Your implementation must preserve, with full fidelity:

- `ProcessId`, `ProcessorName`, `LatestRequestId`, `CreatedBy`, `UpdatedBy` (caller tags: `KaleidoCaller` name and id; the SQLite store keeps them as `...Name`/`...Id` columns)
- `Version` - the optimistic-concurrency version (see below)
- `State` (`ProcessExecutionState` enum - Active, Complete, BusinessFailure, ProcessViolation, HandOff, AwaitingRequiredStep, AwaitingStepSelection, Exception, Canceled, AwaitingInformation)
- `RequiredStep` and `TargetProcessorName` (mutually exclusive continuation markers)
- `RequiredInformationRequest` - the pending information request for `RequiredStep`, stored whole (the SQLite store keeps it as JSON on the required-step row)
- `AvailableSteps` (collection - order not significant)
- `Steps` - every `StepContext` including `StepName`, `Version`, `Status`, `LatestRequestId`, `FirstExecuted`, `StartedBy`, `LastExecuted`, `LastUpdatedBy`
- `CreatedUtc` / `UpdatedUtc` timestamps

`LoadAsync` returns `null` when no process exists - never throws for a missing instance.

### Saving: optimistic concurrency

`SaveAsync` is **conditional**, so two concurrent requests for the same process can never overwrite each other:

- Save only when the stored version still equals `context.Version`. A `Version` of `0` means a new process: save only when no process with that id exists.
- Store the context with `Version + 1` and **return the saved context** (with the new version) - the runtime continues with it.
- Add `events` to the outbox **in the same transaction**: state and events are saved together or not at all (a step's `StepCompleted`, a new process's `ProcessCreated`).
- Otherwise throw `KaleidoConcurrencyException(context.ProcessId)` and save nothing - no state, no event. The caller gets **409** `pro_process_conflict`.

The compare and the write must be one step: a row version / ETag / `WHERE Version = @expected` in the `UPDATE`, a conditional put, or a lock - whatever your storage offers. The SQLite store uses an EF Core concurrency token on a `Version` column (the `UPDATE` is conditional on the version the row was read with) and turns a duplicate insert of a new process into a conflict too.

### Idempotent results

`IKaleidoIdempotencyStore` (`AdmitAsync` / `CompleteAsync` / `ReleaseAsync`) keeps the lifecycle of execute requests sent with an `Idempotency-Key`: the key is admitted before the step runs, so a retry never runs alongside the first attempt and gets its answer instead of running the step again ([`Kaleido.Http`](../Kaleido.Http/README.md#idempotent-execution-idempotency-key), [ADR 0019](../../docs/decisions/0019-idempotent-execution-admission.md)). The SQLite store keeps them next to the process state, in the same database - no extra store for the integrator:

- `AdmitAsync(admission)` stores the record (state `InProgress`, with its `LeaseUntilUtc`) when no record exists for the key, or only an expired one, and returns `null`; otherwise it stores nothing and returns the existing record. It must be **one atomic insert-if-absent** - two admissions of the same key at once never both get `null` (a unique key with `INSERT ... ON CONFLICT DO NOTHING`, a conditional put). Delete expired records here (or in a background job) so they do not pile up.
- `CompleteAsync(result)` stores the answer (state `Completed`), replacing the admission.
- `ReleaseAsync(key)` removes the record **only while it is still `InProgress`** (its request failed, so the key may be used again); a completed record stays.
- Keep `Key`, `RequestHash`, `State`, `LeaseUntilUtc`, `StatusCode`, `Response` (the JSON answer - plan for responses of several KB) and `ExpiresUtc` as given, and keep an admission across restarts: a record still `InProgress` after its lease is how Kaleido knows an attempt never finished.

The SQLite store keeps them in its own `IdempotentResults` table (`Key` primary key, times as UTC ticks, an index on `ExpiresUtc`). An admission is one `INSERT ... ON CONFLICT(Key) DO NOTHING` after deleting expired rows; a completion is an upsert; a release is `DELETE ... WHERE Key = @key AND State = InProgress`.

### Event outbox

Kaleido never publishes an event directly: it writes it to the outbox, and a background relay in every instance hands it to `IEventPublisher`, retrying until it succeeds (at least once - [ADR 0012](../../docs/decisions/0012-events-at-least-once-through-the-store.md)). `IKaleidoOutboxStore` keeps the entries - those `SaveAsync` wrote and the enqueued ones, in the same database - and makes sure **an entry is in one instance's hands at a time**:

- `EnqueueEventsAsync` adds events that do not describe a save (`PlanBuilt`, `ExecutionCompleted`, `QueryExecuted`).
- `ClaimEventsAsync(maxCount, lease)` leases up to `maxCount` entries that are due (not parked, `NextAttemptUtc` reached) and not leased, to **this instance** (the store knows its identity, e.g. `KaleidoServiceOptions.InstanceId`) until now + `lease`, oldest first, and returns exactly those. It must be an **atomic compare-and-set** - two instances claiming at once never get the same entry (a write transaction, a row lock, `UPDATE ... WHERE lease expired`, a conditional put). An entry whose lease expired (its instance stopped) can be claimed again: that is the failover.
- `CompleteEventAsync` removes a published entry and `FailEventAsync` records a failed attempt (`Attempts` + 1, `LastError`, `NextAttemptUtc` = `retryAtUtc`; `null` parks it - kept, never claimed again) and releases the lease. **Both only while this instance still holds the lease**, so an instance whose lease expired cannot touch an entry another instance has taken over.
- Keep every `KaleidoOutboxEvent` member as given and treat `Payload` as opaque text (it includes a step's request and response - plan for several KB). Classified values are already redacted when the payload reaches the store ([ADR 0013](../../docs/decisions/0013-event-payloads-classified-and-redacted.md)).

The SQLite store keeps them in an `OutboxEvents` table (`EventId` primary key, times as UTC ticks, `LeaseOwner`/`LeaseUntil` columns). A claim selects due entries and leases them with one `UPDATE` that re-checks the lease (SQLite serializes writers, so two claims cannot overlap); complete and fail are `DELETE`/`UPDATE ... WHERE LeaseOwner = @me AND LeaseUntil > @now`. Parked entries stay in the table: inspect them, fix the cause, and set `IsParked = 0, NextAttemptUtc = 0` to retry them.

### Schema changes

The host creates the schema (see [Using the example implementation](#using-the-example-implementation)) with `EnsureCreated`, which does not migrate an existing database. **Recreate development databases created before** the `Version` column, the `IdempotentResults` and `OutboxEvents` tables, the `IdempotentResults` `State`/`LeaseUntilUtc` columns (#285), or the caller-tag columns (`CreatedBy*`/`UpdatedBy*`, which replaced `Owner`, and the step `FirstExecuted`/`StartedBy*`/`LastUpdatedBy*` columns) existed (delete the file; the host's `EnsureCreated` call recreates it at startup). A production store manages its own schema migrations.

---

## Using the example implementation

For samples, demos, and local development:

```csharp
builder.Services.AddKaleido(builder.Configuration, o =>
{
    o.ServiceName = "myservice";
    o.Assemblies = [typeof(Program).Assembly];
})
    .UseSqliteKaleidoContextStore("Data Source=my-process.sqlite");

var app = builder.Build();

// The store does not create its tables: the host does, once, at startup.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync();
}
```

This registers the three SQLite stores (`SqliteKaleidoContextStore`, `SqliteKaleidoOutboxStore`, `SqliteKaleidoIdempotencyStore`), scoped, on one `SqliteKaleidoContextDbContext`, replacing the in-memory defaults. Registration does not touch the database: without the `EnsureCreatedAsync()` call the first save fails because the tables do not exist. (This exact sequence is tested against a fresh database file: `tests/Kaleido.IntegrationTests/SqliteReadmeTests.cs`.)

Malformed, non-blank connection strings fail at registration with `KaleidoConfigurationException` (`SqliteErrorCodes.InvalidConnectionString`, `invalid_connection_string`) before DI services are replaced. The parser checks connection-string syntax only: registration does **not** open or create a database.

**Health check.** None is registered. To report the database's reachability, add the Entity Framework Core check (package `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`) and map an endpoint:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SqliteKaleidoContextDbContext>("kaleido-sqlite-context-store");

app.MapHealthChecks("/health");
```

**Telemetry.** The store traces process-state reads and writes and counts their failures: activities `kaleido.sqlite.context.load` / `kaleido.sqlite.context.save` (source `Kaleido.Provider.SQLite`, tagged with the process id) and the counters `kaleido.sqlite.context.load_failures` / `kaleido.sqlite.context.save_failures` (meter `Kaleido.Provider.SQLite`; a concurrency conflict or a cancellation is not a failure). The idempotent-result and outbox methods are not instrumented by the store: event delivery is measured by core's `Kaleido.Events` meter (`kaleido.events.published`, `failed`, `parked`, `enqueue_failures`), and EF Core's own diagnostics cover individual commands. `AddKaleidoInstrumentation()` in [`Kaleido.Observability.OpenTelemetry`](../Kaleido.Observability.OpenTelemetry/README.md) subscribes to both.

The default in-memory stores are sufficient for tests and single-request processes; each logs a warning when it is created (on first use) precisely because production without durable stores is a configuration smell.

---

## What this project does NOT do

- it is not a production-grade store (see header)
- the Process runtime itself (see [`Kaleido`](../Kaleido/README.md))
- HTTP endpoint publication (see [`Kaleido.Http`](../Kaleido.Http/README.md))
- other persistence providers (EF Core, Redis, etc.) — write your own per the contract above

---

## Where to look

- `SqliteKaleidoContextStore.cs`, `SqliteKaleidoOutboxStore.cs`, `SqliteKaleidoIdempotencyStore.cs` — the reference implementations of the three store contracts
- `SqliteKaleidoContextStoreServiceCollectionExtensions.cs` — `UseSqliteKaleidoContextStore(...)` registration
- `tests/Kaleido.Provider.SQLite.UnitTests` — the executable contract specification to mirror for your own store
