# 0036. Stores are scoped by service and version; services write events, one relay publishes them

- **Status:** Accepted
- **Date:** 2026-11-02
- **Supersedes in part:** ADR 0023 (the three outbox integrations; the host's own outbox and direct
  publication are removed), ADR 0028 (an outbox store is chosen by every service)
- **Amends:** ADR 0020 (the store contracts take the scope)
- **Related:** #552, #553, #554, ADR 0012

## Context
Kaleido's stores assumed one database per service, though nothing said so:
- **Process state was keyed by process id alone.** A hand-off keeps the process id, so in a database
  shared by two services the target's new process collided with the origin's row.
- **Outbox claims were not scoped.** Each service's relay would claim and publish every other service's
  events.
- **Idempotency, events and registry snapshots** carried the service name, but no version.

Deployments want the opposite: several services, and several versions of one, on one database, for
example a document store kept for months. And with many services behind a gateway, every service
running its own relay and publisher multiplies the publishing configuration; often one host should
publish everything.

## Decision
1. **Every record is scoped by the service and its version.**
   - The version is `KaleidoServiceOptions.ServiceVersion`: an optional, opaque label (an ASCII letter
     or digit, then letters, digits, `.`, `-` and `_`).
   - **The scope is explicit in the contracts**, so a provider cannot forget it:
     - `IKaleidoContextStore.LoadAsync(ProcessKey)`, with
       `ProcessKey(ProcessorName, ProcessorVersion, ProcessId)`; a context saves under its own `Key`.
     - The origin and the target of a hand-off each keep their own process under the same id.
     - A process of one version is not found by another; moving a process to another version is an
       explicit hand-off.
   - **Idempotency:** the record key is scoped by the version, and records name their processor and
     version.
   - **Events:** they carry `ServiceVersion`, and process events carry `ProcessVersion`, the version of
     the state they describe.
   - **Registry snapshot keys:** they include the version.
2. **Every service writes every event to its outbox store, in the same transaction as its state.** It
   needs no publisher. Outbox entries name their writer (`ServiceName`, `ServiceVersion`). The outbox
   store sits on the context store's database; that is documented, not enforced.
3. **One relay publishes.** `AddOutboxRelay<TPublisher>()` runs on any host, a writer service or a host
   with nothing else, and owns the publisher and the relay settings.
   - It drains every entry in its outbox store's database, polling and claiming a batch at a time.
   - Several relay instances coordinate through leased claims.
   - There is no default publisher.
4. **Removed:** the host's own outbox (`AddOutbox<T>`, `IKaleidoOutbox` as a host choice) and direct
   publication (`AddDirectPublication`). A host that publishes through its own bus implements
   `IEventPublisher` on the relay host.
5. **An outbox store is required of every service,** a query-only service included. Durable, or memory
   chosen explicitly with `AddInMemoryStores()`.
6. **No ordering guarantee.** Consumers order a process's events by `ProcessVersion` and drop
   duplicates by `EventId`.
7. **Redaction stays with the writer:** it needs the service's own classified types. The relay only
   forwards stored envelopes.
8. **A missing relay is detected at run time.** It cannot be checked at startup: the relay may be any
   host.
   - Every relay instance keeps a heartbeat in the database it drains, and every writer's health check
     reads it there: no relay seen within a configurable window is unhealthy.
   - A backlog health check watches the oldest due entry's age and the number of due entries.
   - Neither calls another service, so nothing recurses.

## Alternatives considered
- **Implicit scope** (providers read `KaleidoServiceOptions`). It changes fewer signatures, but a
  provider that forgets the filter mixes services' data silently, and only in a shared database.
- **A cross-version lookup** ("this process belongs to version 1"). A friendlier error, but every store
  needs a query across versions. Not found is enough; the URLs a process was given already carry its
  version (#553).
- **Keep the host's own outbox.** A host with its own bus publishes through `IEventPublisher` on the
  relay host, which keeps one model: services write, a relay publishes.
- **Keep direct publication.** An in-process relay over an in-memory outbox gives the same development
  experience, with retries, and one less way to lose events.
- **Ordered delivery per process.** It needs head-of-line blocking (a parked event blocks its process)
  and claims partitioned by process across relays. A sequence on the events lets consumers order
  them instead.

## Consequences
- **Breaking (preview):**
  - `IKaleidoContextStore.LoadAsync` takes a `ProcessKey`;
  - `AddEventPublisher<T>`, `AddOutbox<T>` and `AddDirectPublication` are removed, and a host that
    publishes calls `AddOutboxRelay<TPublisher>()`;
  - SQLite development databases must be recreated.
- **A database no relay reads keeps growing.** The relay heartbeat and the backlog health checks report
  it.
- **A provider's store keeps the scope columns.** The SQLite reference and the guide's EF Core example
  show how; a document store uses a compound key.
