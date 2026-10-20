# 0012. Events reach the publisher at least once, through an outbox in the store

- **Status:** Accepted (the single store interface is revised by ADR 0020: the outbox has its own contract, still in the state's database)
- **Date:** 2026-10-12
- **Related:** #240, #233

## Context
Kaleido published events fire-and-forget: after saving the process state the runtime called
`IEventPublisher.PublishAsync` without awaiting it. A publisher that threw, or whose task failed,
lost the event, and a crash between the save and the call lost it too. Events are the audit trail
and the feed of everything downstream, so "may be lost" is not good enough - but Kaleido must not
become a message broker either, and it must not make integration harder.

## Decision
- **The guarantee:** every event is handed to `IEventPublisher` successfully **at least once**.
  Whether the final receiver gets it is the publisher's concern (Kaleido never talks to brokers).
- **Outbox in the store.** `IProcessorContextStore` becomes `IKaleidoContextStore`, the one durable
  store of a service (process state, idempotent results, outbox), registered by `AddKaleido()` for
  every service. Events that describe a save are written in the same transaction as the state:
  `ProcessCreated` (a new process is saved right away, so every created process is announced) and
  `StepCompleted`. `PlanBuilt`, `ExecutionCompleted` and `QueryExecuted` are enqueued right after.
- **In memory by default, persistent opt-in.** The in-memory store keeps the outbox in memory
  (survives a publisher outage, not a restart); the SQLite store - or a custom one - keeps it in the
  same database as the state.
- **A relay per instance.** A hosted background service claims a batch, publishes each entry, and
  removes it on success. Claims are leased, so with several instances on one store an entry is in
  one instance's hands at a time, and a stopped instance's entries are taken over when its leases
  expire. Completing or failing an entry requires the lease.
- **Two layers of retry.** The durable schedule lives in the outbox (`Attempts`, `NextAttemptUtc`,
  exponential backoff, parked after the last retry - never dropped). Each attempt runs through a
  Polly pipeline with a timeout and a circuit breaker and no Polly retries; while the circuit is
  open the relay claims nothing.
- **Bounded load.** `Kaleido:Events:BatchSize` caps a claim, `MaxConcurrentPublishes` the parallel
  publishes per instance, `Resilience` the timeout/retry/circuit settings (the same vocabulary as
  the HTTP clients, `KaleidoResilienceSettings`).
- **Duplicates are possible** (a crash between a successful publish and its removal), so every
  event carries `IKaleidoEvent.EventId` for consumers to deduplicate.

## Alternatives considered
- **Best-effort by contract** (document that events may be lost; consumers needing more tail the
  store's log). Rejected: it pushes the hardest part onto every integrator.
- **A separate outbox interface / store.** Rejected: one more seam to implement and register; the
  outbox belongs in the same database as the state for the atomic save.
- **Polly retries for the events themselves.** Rejected as the retry mechanism: in-memory retries
  vanish with a restart. Polly is used per attempt (timeout, circuit breaker) only.
- **Broker integration in Kaleido.** Rejected: topologies differ per deployment; the publisher is
  the integration point.

## Consequences
- A publisher never slows down or fails a request; event delivery survives publisher outages and,
  with a durable store, restarts and instance failures.
- The store contract grows (outbox methods, `SaveAsync` takes the save's events); custom stores
  must implement leased claims as an atomic compare-and-set. Development databases created before
  must be recreated.
- Publishers receive the stored (JSON) form of an envelope: `object`-typed members arrive as
  `JsonElement`. Order is not guaranteed across retries (`OccurredOn` is).
- Core depends on `Microsoft.Extensions.Hosting.Abstractions` (the relay) and `Polly.Core`.
