# Kaleido.Provider.SQLite

> **Reference provider for a local SQLite database, not a production multi-node store.** Use a server database and a provider written for your deployment when services run on several machines. SQLite's file is not a network-share store; concurrent-writer WAL/busy-timeout and shared in-memory connection lifetime are tracked separately in #558.

This single technology project implements the existing [`IProcessContextStore` and `IProcessTransactions`](../../Capabilities/Process/Kaleido.Process/README.md), Process.Server's [`IProcessIdempotencyStore`](../../Capabilities/Process/Kaleido.Process.Server/README.md), Eventing's `IKaleidoOutboxStore` and the control Registry's [`IKaleidoRegistryStore`](../../Capabilities/Registry/Kaleido.Registry.Server/README.md). It references the base [`Kaleido.Provider`](../Kaleido.Provider/README.md) and Registry.Server; no framework capability depends back on SQLite. Its Process and Eventing stores share one scoped `SqliteKaleidoContextDbContext`. The Registry has its **own** `SqliteKaleidoRegistryDbContext` and database; no extra Registry provider project or interface is needed.

## Registration

An explicit aggregate choice reads `ConnectionStrings:orders` from the builder configuration and registers Process state, the outbox and server idempotency on the **same connection string**. It does not register the control Registry store or create a database:

```csharp
builder.Services.AddKaleido(builder.Configuration, new()
{
    ServiceName = "orders",
    ServiceVersion = "3",
    Assemblies = [typeof(Program).Assembly],
})
    .AddHosting()
    .AddSqliteProviders("orders");

// After building the host, before processing requests:
using (var scope = host.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>()
        .Database.EnsureCreatedAsync();
}
```

For mixed providers, choose **only** the stores that should live on SQLite: `AddSqliteContextProvider(connectionString)` (also selects `IProcessTransactions`), `AddSqliteOutboxProvider(connectionString)`, or `AddSqliteIdempotencyProvider(connectionString)`. Each uses the existing owner `Add*Store<T>()` seam; no capability silently selects SQLite or memory. The three SQLite stores in one host must use the same connection string (`connection_string_mismatch` otherwise). A missing `ConnectionStrings:{name}` for the aggregate choice is `sqlite_missing_connection_string`; a malformed nonblank string is `invalid_connection_string`. Registration does not install a database health check. The [file-backed host suite](../../../tests/Providers/Kaleido.Provider.SQLite.FunctionalTests/SqliteProviderHostFunctionalTests.cs) exercises the aggregate choice with `ValidateScopes`/`ValidateOnBuild` in Production.

A control Registry host independently calls `AddSqliteRegistryProvider(registryConnectionString)` and creates `SqliteKaleidoRegistryDbContext`'s schema on **its own database**. The aggregate service choice never registers this authority store. It implements the already-public Registry.Server store contract, including conditional instance/lease operations, current versions and typed build-version capabilities.

## Stored behavior

- **Process state:** `ProcessContexts` and child tables key rows by `(ProcessorName, ProcessorVersion, ProcessId)`; a nullable legacy processor version uses an empty database key. `SqliteProcessContextStore` round-trips `ProcessContext`'s callers, steps, available/required steps, pending information request, request ids, times and execution state. `TargetProcessorName` and **`TargetProcessorVersion`** are separate stored columns: the origin's route version must not replace the actual target version. A save reconciles the current step/required-step rows in place as the registered step catalog changes, compares the stored `Version`, increments it atomically and raises `ProcessConcurrencyException` for a stale update or duplicate creation. Missing keys load as null.
- **Atomic state and events:** `SqliteProcessTransactions` opens a transaction on the scoped EF context shared by the Process store and `SqliteKaleidoOutboxStore`; state and a transition's outbox events commit or roll back together. A rollback clears tracked entities. An independently written outbox is atomic with the state only when it explicitly joins the **same provider session** through `ISqliteKaleidoTransaction.Current`, not merely because it uses the same filename. A separate idempotency admission needs no state transaction.
- **Idempotency:** `IdempotentResults` stores the caller-scoped key, processor/version, admission id, hash, state, lease/expiry and **opaque** `ReplayFormat` text plus `ReplayPayload` bytes. SQLite does not interpret an HTTP status, JSON body or transport format. An atomic insert-if-absent admits one attempt; conditional complete/release requires its admission id, `InProgress` state and unexpired retention. An abandoned lease remains an unknown outcome until expiry; an expired or replaced admission cannot finalize a newer one. A completed row without both replay fields fails closed (`sqlite_invalid_stored_record`). The HTTP adapter owns its replay encoding.
- **Outbox:** Entries retain every `KaleidoOutboxEvent` field and the writer's service/version; a claim conditionally leases due rows to this host's internal instance id, and complete/fail act only while it holds the lease. Parked entries remain stored; relay heartbeats support the Eventing health check. The writer redacts classified event payloads **before** the store sees them. The SQLite outbox does not choose a relay or publisher.
- **Control Registry:** `RegistryRegistrations` keys one published build by `(ServiceName, ServiceVersion, BuildVersion)` and stores a typed `KaleidoRegistry` as provider-owned JSON. `RegistryInstances` retains the matching build identity, health and heartbeats. `RegistryCurrentVersions` and `RegistryWatch` retain the service's current route version and unbroken watch; writes are conditional on their own rows. Service names compare without case, route/build versions exactly. A stored `null` capability payload fails closed (`sqlite_invalid_stored_record`). The shared `IKaleidoRegistryStoreTests` runs against this SQLite implementation as well as the in-memory authority store.

## Schema and limits

`EnsureCreatedAsync()` only creates a **new** schema. It does not migrate or truncate existing files. In particular, older preview files lack `ProcessContexts.TargetProcessorVersion`, the `IdempotentResults` replay format/payload columns (they used HTTP `StatusCode`/`Response`), and the Registry's composite `BuildVersion` registration identity. The current provider must not reinterpret those rows as valid results: migrate a database deliberately, or recreate only disposable local-development data after confirming its contents are expendable. No old replay-row compatibility is promised by #587. Do not delete a deployed database as a build workaround.

The [SQLite unit suite](../../../tests/Providers/Kaleido.Provider.SQLite.UnitTests/Kaleido.Provider.SQLite.UnitTests.csproj) covers stored state fidelity, conditional saves, opaque replay/fencing, leased outbox operations, rollback and the Registry.Server contract. The [file-backed functional suite](../../../tests/Providers/Kaleido.Provider.SQLite.FunctionalTests/SqliteProviderHostFunctionalTests.cs) stops a composed host and recreates it on the same file: the hand-off's target version, replay bytes and outbox entries survive; an uncommitted state/event transaction leaves neither. The same suite also runs the compiled persistence guide's consumer-authored context/outbox/transaction recipe against a fresh SQLite file, including conditional saves, leases, retries and relay heartbeat. The [transaction suite](../../../tests/Providers/Kaleido.Provider.SQLite.FunctionalTests/SqliteTransitionFunctionalTests.cs) verifies both SQLite's own outbox and a host-written outbox joining `ISqliteKaleidoTransaction.Current`, including conflicts and outbox-write rollback. Telemetry names, event discriminators and error codes remain stable. See [provider dependencies](../../../docs/architecture/dependencies.md), [ADR 0044](../../../docs/decisions/0044-process-server-idempotency-ownership.md), [ADR 0048](../../../docs/decisions/0048-explicit-provider-registration-without-default-stores.md) and the [persistence guide](../../../docs/guide/persistence.md).
