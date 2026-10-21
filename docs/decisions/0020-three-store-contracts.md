# 0020. Three store contracts, one concern each

- **Status:** Accepted; decision 2 (the context store writes a save's events) amended by
  [ADR 0023](./0023-outbox-integration.md): the context store is state only, the outbox integration
  receives every event, and a persistence package's transaction makes them atomic
- **Date:** 2026-10-19
- **Related:** #285, ADR 0012, ADR 0019

## Context
ADR 0012 made `IKaleidoContextStore` "the one durable store of a service": process state, the
event outbox and the stored answers of idempotent requests, on one interface. It rejected a separate
outbox interface as "one more seam to implement and register". With #285 the idempotency part grew
an admission lifecycle (ADR 0019), and the interface held three concerns and ten methods; a custom
store had to implement all of them in one class.

The intent was always **one database** for the three - so a SQLite (or any) host sets up nothing
extra, and a save's events are written in the state's transaction - not **one interface**.

## Decision
1. **Three contracts** in `Kaleido.Storage`, one concern each:
   - `IKaleidoContextStore` - process state: `LoadAsync`, `SaveAsync(context, events)`;
   - `IKaleidoOutboxStore` - the event outbox: `EnqueueEventsAsync`, `ClaimEventsAsync`,
     `CompleteEventAsync`, `FailEventAsync` (and the `KaleidoOutboxEvent` record);
   - `IKaleidoIdempotencyStore` - requests sent with an idempotency key: `AdmitAsync`,
     `CompleteAsync`, `ReleaseAsync`.
2. **One coupling stays, by design:** `SaveAsync` writes the events that describe a save to the
   outbox in the same transaction as the state (ADR 0012's guarantee). So a context store and an
   outbox store share their database and are replaced together. The idempotency store needs no
   shared transaction.
3. **Defaults and providers implement them separately.** `AddKaleido()` registers three in-memory
   defaults (`TryAdd`, so a host's own registration wins); each logs a Warning that its data is
   lost on restart. The in-memory context store adds a save's events to the registered outbox.
   `UseSqliteKaleidoContextStore` registers three SQLite stores on one scoped
   `SqliteKaleidoContextDbContext`.
4. Consumers depend on the contract they use: the runtime on the context store, the event outbox
   and relay on the outbox store, the HTTP idempotency filter on the idempotency store.

## Alternatives considered
- **Keep one interface** (ADR 0012). Fewer registrations, but one class with three unrelated
  responsibilities, and a host replacing only one concern must still implement all ten methods.
  Rejected.
- **Decouple the save from the outbox** (a unit of work across stores). Would let the two live in
  different databases, at the cost of a transaction abstraction in core. Not needed: the shared
  database is the point.

## Consequences
- Breaking for custom stores (preview): one interface becomes three; folded into the same change
  as ADR 0019, so the contract changes once.
- A host that replaces the context store must replace the outbox store on the same database too,
  or a save's events land where the relay never reads them. Documented on both interfaces and in
  the SQLite provider README.
- Tests mirror the split: one fixture per store, in memory and SQLite, plus a SQLite test that the
  outbox store claims a row written with a save.
