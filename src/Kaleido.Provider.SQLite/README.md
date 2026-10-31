# Kaleido.Provider.SQLite

> **This project is a reference implementation, not a production provider.**
>
> - `IKaleidoContextStore`, `IKaleidoOutboxStore` (or your own `IKaleidoOutbox`), `IKaleidoIdempotencyStore` and `IKaleidoTransactions` are the **contracts** your deployment implements.
> - `SqliteKaleidoContextStore`, `SqliteKaleidoOutboxStore`, `SqliteKaleidoIdempotencyStore` and `SqliteKaleidoTransactions` are a **worked example** of those contracts on one database — a single-file embedded database that demonstrates correct `ProcessorContext` round-trip fidelity.
> - **Production deployments are expected to write their own implementation** against their real infrastructure (SQL Server, Postgres, Redis, document store, whatever the process actually needs).
> - SQLite's single-writer file model is deliberately unsuitable for the multi-node, multi-instance scenarios durable process state is designed for. Shipping it to production is an anti-pattern.
> - The `Kaleido.Provider.SQLite.UnitTests` suite is the **executable contract specification**: if your implementation passes equivalent round-trip fidelity, update, and multi-instance tests, it satisfies the contracts.

> How this project fits into a whole service, with tested examples: the [consumer guide](../../docs/guide/README.md). This README is the reference for the project.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../Kaleido/README.md`](../Kaleido/README.md)

---

## What lives here

- `SqliteKaleidoContextStore` — example `IKaleidoContextStore` implementation (process state only)
- `SqliteKaleidoOutboxStore` — example `IKaleidoOutboxStore` implementation (the storage of Kaleido's built-in outbox, same database)
- `SqliteKaleidoIdempotencyStore` — example `IKaleidoIdempotencyStore` implementation (idempotent requests, same database)
- `SqliteKaleidoTransactions` — `IKaleidoTransactions`: one transaction on the shared `DbContext` per process-state transition, so state and events commit together
- `ISqliteKaleidoTransaction` — the open transaction of the current transition, for the host's own outbox on the same database to join
- `SqliteKaleidoContextDbContext` — the one EF Core context all of them share (scoped: one per request)
- `SqliteKaleidoServiceCollectionExtensions` — `AddSqliteContextStore`, `AddSqliteOutboxStore`, `AddSqliteIdempotencyStore`

---

## Implementing your own store

Each concern is its own contract ([ADR 0020](../../docs/decisions/0020-three-store-contracts.md), [ADR 0023](../../docs/decisions/0023-outbox-integration.md)). `AddKaleido()` registers an in-memory default for each (every service gets them, so a queryable-only service uses the outbox too, for its query events), but a host that leaves a store it uses at that default does not start (`stores_not_chosen`) unless it calls `AddInMemoryStores()` ([ADR 0028](../../docs/decisions/0028-stores-are-chosen-explicitly.md)); replace them on the builder: `AddContextStore<T>()`, `AddIdempotencyStore<T>()`, `AddOutboxStore<T>()` (storage for Kaleido's outbox) or `AddOutbox<T>()` (your own outbox), and `AddTransactions<T>()`.

**How state and events stay together.** The context store saves state only; Kaleido adds a transition's event (a new process's `ProcessCreated`, a step's `ProcessStepCompleted`) to the outbox itself. Both happen inside one transaction from `IKaleidoTransactions`: begin, `SaveAsync`, `AddAsync`, commit. A failure before the commit keeps neither. That is atomic when your context store and your outbox write to the database the transaction covers (e.g. one scoped `DbContext`: write in the open transaction when there is one, on your own otherwise). Without `IKaleidoTransactions` each write stands alone. The idempotency store needs no shared transaction.

```csharp
public interface IKaleidoContextStore
{
    Task<ProcessorContext?> LoadAsync(Guid processId, CancellationToken cancellationToken);
    Task<ProcessorContext> SaveAsync(ProcessorContext context, CancellationToken cancellationToken);
}

public interface IKaleidoTransactions   // optional
{
    Task<IKaleidoTransaction> BeginAsync(CancellationToken cancellationToken);   // IKaleidoTransaction: CommitAsync; disposing without a commit rolls back
}

public interface IKaleidoIdempotencyStore
{
    Task<IdempotentResult?> AdmitAsync(IdempotentResult admission, CancellationToken cancellationToken);
    Task<bool> CompleteAsync(IdempotentResult result, CancellationToken cancellationToken);
    Task<bool> ReleaseAsync(string key, Guid admissionId, CancellationToken cancellationToken);
}

public interface IKaleidoOutboxStore
{
    Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken);
    Task<IReadOnlyList<KaleidoOutboxEvent>> ClaimEventsAsync(int maxCount, TimeSpan lease, CancellationToken cancellationToken);
    Task CompleteEventAsync(Guid eventId, CancellationToken cancellationToken);
    Task FailEventAsync(Guid eventId, DateTimeOffset? retryAtUtc, string errorMessage, CancellationToken cancellationToken);
}
```

### Process state

Your implementation must preserve, with full fidelity:

- `ProcessId`, `ProcessorName`, `LatestRequestId`, `CreatedBy`, `UpdatedBy` (caller tags: `KaleidoCaller` name and id; the SQLite store keeps them as `...Name`/`...Id` columns)
- `Version` - the optimistic-concurrency version (see below)
- `State` (`ProcessExecutionState` enum, stored by name - Active, Complete, BusinessFailure, HandOff, AwaitingRequiredStep, AwaitingStepSelection, AwaitingInformation; a store created before #536 may hold `ProcessViolation`, `Exception` or `Canceled`, which no longer load: recreate it)
- `RequiredStep` and `TargetProcessorName`, each on its own: a hand-off has a target and no required step, a cross-processor required step has both, and a hand-back clears the target (the SQLite store keeps the target in its own `TargetProcessorName` column on `ProcessContexts`, #513)
- `RequiredInformationRequest` - the pending information request for `RequiredStep`, stored whole (the SQLite store keeps it as JSON on the required-step row)
- `AvailableSteps` (collection - order not significant)
- `Steps` - every `StepContext` including `StepName`, `Status`, `LatestRequestId`, `FirstExecuted`, `StartedBy`, `LastExecuted`, `LastUpdatedBy`
- `CreatedUtc` / `UpdatedUtc` timestamps

`LoadAsync` returns `null` when no process exists - never throws for a missing instance.

### Saving: optimistic concurrency

`SaveAsync` is **conditional**, so two concurrent requests for the same process can never overwrite each other:

- Save only when the stored version still equals `context.Version`. A `Version` of `0` means a new process: save only when no process with that id exists.
- Store the context with `Version + 1` and **return the saved context** (with the new version) - the runtime continues with it.
- Otherwise throw `KaleidoConcurrencyException(context.ProcessId)` and save nothing. Kaleido then adds no event and rolls the transition back. The caller gets **409** `pro_process_conflict`.

The compare and the write must be one step: a row version / ETag / `WHERE Version = @expected` in the `UPDATE`, a conditional put, or a lock - whatever your storage offers. The SQLite store uses an EF Core concurrency token on a `Version` column (the `UPDATE` is conditional on the version the row was read with) and turns a duplicate insert of a new process into a conflict too: a new process's row is inserted in a statement of its own, so a primary-key violation there can only be the process id - other constraint failures stay store failures, and no error message is read (#513). When a transition rolls back, the store's `DbContext` forgets what it tracked in it, so a later save in the same scope reads the stored state again instead of a phantom conflict.

### Idempotent results

`IKaleidoIdempotencyStore` (`AdmitAsync` / `CompleteAsync` / `ReleaseAsync`) keeps the lifecycle of execute requests sent with an `Idempotency-Key`: the key is admitted before the step runs, so a retry never runs alongside the first attempt and gets its answer instead of running the step again ([`Kaleido.Http`](../Kaleido.Http/README.md#idempotent-execution-idempotency-key), [ADR 0019](../../docs/decisions/0019-idempotent-execution-admission.md)). The SQLite store keeps them next to the process state, in the same database - no extra store for the integrator:

- `AdmitAsync(admission)` stores the record (state `InProgress`, with its `AdmissionId` and `LeaseUntilUtc`) when no record exists for the key, or only an expired one, and returns `null`; otherwise it stores nothing and returns the existing record. It must be **one atomic insert-if-absent** - two admissions of the same key at once never both get `null` (a unique key with `INSERT ... ON CONFLICT DO NOTHING`, a conditional put). Delete expired records here (or in a background job) so they do not pile up.
- `CompleteAsync(result)` replaces the record with `result` - state `Completed` (the answer) or `OutcomeUnknown` (the attempt failed after it may have run) - **only while the record is still `InProgress` under the same `AdmissionId`**, and returns whether it did. Never an insert.
- `ReleaseAsync(key, admissionId)` removes the record **only while it is still `InProgress` under that `AdmissionId`** (the request was refused before anything ran, so the key may be used again), and returns whether it did.
- The `AdmissionId` check is the fencing (#441): an attempt that outlived its record - it expired, and the key was admitted again - is refused, so it can neither overwrite nor delete the newer admission. The compare and the write must be one step (`UPDATE`/`DELETE ... WHERE Key = @key AND AdmissionId = @id AND State = InProgress`, a conditional put).
- Keep `Key`, `AdmissionId`, `RequestHash`, `State`, `LeaseUntilUtc`, `StatusCode`, `Response` (the JSON answer - plan for responses of several KB) and `ExpiresUtc` as given, and keep records across restarts: a record still `InProgress` after its lease, or one marked `OutcomeUnknown`, is how Kaleido knows an attempt may have run. Such a key is refused (`pro_idempotency_outcome_unknown`) until its record expires (`KaleidoHttpOptions.IdempotencyUnknownOutcomeRetention`, default 7 days); an operator resolves it sooner by reading the process state and deleting the record.

The SQLite store keeps them in its own `IdempotentResults` table (`Key` primary key - a fixed-length `v1:` hash of the caller's scope, which the logs name next to the header's key, `AdmissionId` as text, times as UTC ticks, an index on `ExpiresUtc`). An admission is one `INSERT ... ON CONFLICT(Key) DO NOTHING` after deleting expired rows; a completion is `UPDATE ... WHERE Key = @key AND AdmissionId = @id AND State = InProgress` and a release the matching `DELETE`, each reporting from its row count whether it applied - so the fencing holds across instances sharing the database file.

### Event outbox

With Kaleido's built-in outbox (the default), events are added to the outbox store, and a background relay in every instance hands them to `IEventPublisher`, retrying until it succeeds (at least once - [ADR 0012](../../docs/decisions/0012-events-at-least-once-through-the-store.md)). `IKaleidoOutboxStore` keeps the entries and makes sure **an entry is in one instance's hands at a time**. (A host that already runs an outbox registers its own `IKaleidoOutbox` instead and needs none of this - see [Your own outbox on the same database](#your-own-outbox-on-the-same-database).)

- `AddAsync` adds entries: a transition's event inside its transaction (write in the open transaction when there is one on your database), others (`PlanBuilt`, `ExecutionCompleted`, `QueryExecuted`) on their own.
- `ClaimEventsAsync(maxCount, lease)` leases up to `maxCount` entries that are due (not parked, `NextAttemptUtc` reached) and not leased, to **this instance** (the store knows its identity, e.g. `KaleidoServiceOptions.InstanceId`) until now + `lease`, oldest first, and returns exactly those. It must be an **atomic compare-and-set** - two instances claiming at once never get the same entry (a write transaction, a row lock, `UPDATE ... WHERE lease expired`, a conditional put). An entry whose lease expired (its instance stopped) can be claimed again: that is the failover.
- `CompleteEventAsync` removes a published entry and `FailEventAsync` records a failed attempt (`Attempts` + 1, `LastError`, `NextAttemptUtc` = `retryAtUtc`; `null` parks it - kept, never claimed again) and releases the lease. **Both only while this instance still holds the lease**, so an instance whose lease expired cannot touch an entry another instance has taken over.
- Keep every `KaleidoOutboxEvent` member as given and treat `Payload` as opaque text (it includes a step's request and response - plan for several KB). Classified values are already redacted when the payload reaches the store ([ADR 0013](../../docs/decisions/0013-event-payloads-classified-and-redacted.md)).

The SQLite store keeps them in an `OutboxEvents` table (`EventId` primary key, times as UTC ticks, `LeaseOwner`/`LeaseUntil` columns). A claim selects due entries and leases them with one `UPDATE` that re-checks the lease (SQLite serializes writers, so two claims cannot overlap); complete and fail are `DELETE`/`UPDATE ... WHERE LeaseOwner = @me AND LeaseUntil > @now`. Parked entries stay in the table: inspect them, fix the cause, and set `IsParked = 0, NextAttemptUtc = 0` to retry them.

### Schema changes

The host creates the schema (see [Using the example implementation](#using-the-example-implementation)) with `EnsureCreated`, which does not migrate an existing database. **Recreate development databases created before** the `Version` column, or with the step `Version` column in `ProcessStepContexts` (removed, #474), the `IdempotentResults` and `OutboxEvents` tables, the `IdempotentResults` `State`/`LeaseUntilUtc` columns (#285) or its `AdmissionId` column (#441), the caller-tag columns (`CreatedBy*`/`UpdatedBy*`, which replaced `Owner`, and the step `FirstExecuted`/`StartedBy*`/`LastUpdatedBy*` columns) existed, or before the `ProcessContexts.TargetProcessorName` column (`ProcessRequiredSteps.ProcessorName` removed, #513) (delete the file; the host's `EnsureCreated` call recreates it at startup). A production store manages its own schema migrations.

---

## Using the example implementation

For samples, demos, and local development:

```csharp
builder.Services.AddKaleido(builder.Configuration, new() { ServiceName = "myservice", Assemblies = [typeof(Program).Assembly] })
    .AddSqliteContextStore("Data Source=my-process.sqlite")
    .AddSqliteOutboxStore("Data Source=my-process.sqlite")
    .AddSqliteIdempotencyStore("Data Source=my-process.sqlite");

var app = builder.Build();

// The store does not create its tables: the host does, once, at startup.
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>().Database.EnsureCreatedAsync();
}
```

Each call registers one store, scoped, on one `SqliteKaleidoContextDbContext`, replacing its in-memory default; take only the ones you want (e.g. SQLite state with your own outbox). `AddSqliteContextStore` also registers `SqliteKaleidoTransactions`: with `AddSqliteOutboxStore` on the same database, a step's state and its event commit together. Registration does not touch the database: without the `EnsureCreatedAsync()` call the first save fails because the tables do not exist. (This exact sequence is tested against a fresh database file: `tests/Kaleido.IntegrationTests/SqliteReadmeTests.cs`.)

**How a step's state and event commit together.** The stores never call each other: they share the request's `DbContext`, and the transaction Kaleido begins through `SqliteKaleidoTransactions` covers both writes. The sequence diagram is in [ADR 0023](../../docs/decisions/0023-outbox-integration.md#a-step-completes-sqlite-state-kaleidos-outbox-on-sqlite); `tests/Kaleido.IntegrationTests/Transitions/SqliteTransitionTests.cs` proves it (a conflict or a failed event write keeps neither).

Registration fails at startup with `KaleidoConfigurationException`:
- a malformed, non-blank connection string: `SqliteErrorCodes.InvalidConnectionString` (`invalid_connection_string`), before anything is registered (syntax only: no database is opened);
- a different connection string than another SQLite store's: `SqliteErrorCodes.ConnectionStringMismatch` (`connection_string_mismatch`) - they share one database;
- another context store, idempotency store or transactions already registered (`conflicting_store`), or a SQLite outbox store next to `AddOutbox`/`AddDirectPublication` (`conflicting_outbox`).

### Your own outbox on the same database

A host that already has an outbox (its own table and dispatcher, MassTransit's EF outbox, ...) keeps SQLite for the state and plugs its outbox in with `AddOutbox<T>()`; to commit with the state, it joins the SQLite transaction through `ISqliteKaleidoTransaction`:

```csharp
builder.Services.AddKaleido(builder.Configuration, new())
    .AddSqliteContextStore(connectionString)       // state + the SQLite transaction
    .AddOutbox<IntakeOutbox>();                    // the host's outbox, same database file

public sealed class IntakeOutbox(IntakeDbContext db, ISqliteKaleidoTransaction transaction) : IKaleidoOutbox
{
    public async Task AddAsync(IReadOnlyCollection<KaleidoOutboxEvent> events, CancellationToken cancellationToken)
    {
        if (transaction.Current is { } current)                        // inside a transition: join it
        {
            db.Database.SetDbConnection(current.Connection);
            await db.Database.UseTransactionAsync(current, cancellationToken);
        }

        db.OutboxMessages.AddRange(events.Select(e => new OutboxMessage(e.EventId, e.EventType, e.Payload, e.OccurredUtc)));
        await db.SaveChangesAsync(cancellationToken);                  // committed with the state
    }
}
```

Only on the same database file: an outbox in another database cannot join, and its writes stand alone.

**Health check.** None is registered. To report the database's reachability, add the Entity Framework Core check (package `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`) and map an endpoint:

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<SqliteKaleidoContextDbContext>("kaleido-sqlite");

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

- `SqliteKaleidoContextStore.cs`, `SqliteKaleidoOutboxStore.cs`, `SqliteKaleidoIdempotencyStore.cs`, `SqliteKaleidoTransactions.cs` — the reference implementations of the contracts
- `SqliteKaleidoTransaction.cs` — `ISqliteKaleidoTransaction`, for a host's own outbox to join
- `SqliteKaleidoServiceCollectionExtensions.cs` — the three registrations
- `tests/Kaleido.Provider.SQLite.UnitTests` — the executable contract specification to mirror for your own store
