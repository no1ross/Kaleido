# 0042. Event type discriminators belong to their emitting capabilities

- **Status:** Accepted
- **Date:** 2026-11-10
- **Related:** #585, #588, #589, #590

## Context

The former `KaleidoEventTypes` catalog in foundation/Eventing named four Process events and a Queryable event. Once those capabilities are independent, Eventing cannot link to their concrete payload types without depending upward on them. Replacing the links with plain text would lose useful documentation while keeping event names under the wrong owner.

## Decision

Process owns `ProcessEventTypes`, beside its declared Process event payloads. Queryable owns `QueryableEventTypes`, beside `QueryExecuted`. Each constant's XML documentation links directly to its capability's event declaration. Eventing owns the generic `KaleidoEventEnvelope<TEvent,TContext>.EventType` string, storage, redaction and relay; its lower-level documentation describes a discriminator defined by the emitter, not a catalog it owns.

Keep the persisted discriminator values unchanged: `process.created.v1`, `process.plan-built.v1`, `process.step-completed.v1`, `process.execution-completed.v1`, and `query.executed.v1`. Process and Queryable dispatch their own typed envelopes; a relay-only host registers each event family it drains without registering a runtime.

## Alternatives considered

- Keep all constants in Eventing and remove the unresolvable `cref` links. Rejected: loses useful navigation and assigns capability names to lower-level infrastructure.
- Make Eventing reference Process and Queryable so the links compile. Rejected: reverses the dependency and creates a cycle with the capabilities that write events.
- Change the stored event type strings. Rejected: persistence and subscribers depend on their stable values; this is an ownership migration, not an event-format revision.

## Consequences

Package references and C# constant names change; the stored values and serialized envelopes do not. Update public API baselines, consumers, tests and docs with the capability that owns each event. Existing unmigrated consumers may temporarily fail during #585; complete their changes before the full repository gate. The generic Eventing package must stay independent of Process and Queryable.
