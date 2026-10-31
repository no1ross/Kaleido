# 0034. A step outcome that cannot be saved: say the step ran, keep it, and never run it twice

- **Status:** Accepted
- **Date:** 2026-10-30
- **Related:** #537, #513, #517, #514, ADR 0008, ADR 0019, ADR 0023, ADR 0033

## Context
A step's handler does its work and returns: the consumer has saved its data or called another
service. Then Kaleido saves the process state with the step's event (ADR 0023). When that save fails
(the store is down, a timeout, the commit fails), the consumer's work is done and Kaleido does not
know it.

The executor caught the save failure in the same `try` as the handler's own failure. It then:
- recorded the step as a failed step on the old state;
- saved a second time, to the store that had just failed;
- answered a generic error.

A retry after that ran the handler's work again. A version conflict after the handler ran was
answered "nothing was saved, read again". The idempotency filter released the key, so a keyed retry
ran it again too.

A transaction cannot fix this: the handler's work is often outside Kaleido's database (an HTTP call,
an email, another system), and it cannot be rolled back.

## Decision
- **Retries belong to the store provider.** Kaleido does not retry a save in core. Only the provider
  knows which errors are transient and whether a commit is ambiguous; after a lost acknowledgement, a
  retry would read as a conflict.
- **A failure after the handler returned is not a step failure.** The executor's catch blocks cover
  the handler only. A save failure is reported as an unsaved outcome, with no second save. The
  decision is recorded in telemetry only once it is saved.
- **The answer says the step ran,** and the handler's result is not returned:
  - `503 pro_outcome_pending_recovery`: the outcome is kept and saved when the store is back;
  - `500 pro_outcome_not_saved`: it could not be kept;
  - `409 pro_outcome_not_saved`: another request changed the process meanwhile.

  With an `Idempotency-Key`, a retry gets `pro_idempotency_outcome_unknown` (ADR 0019), never a
  second run.
- **The outcome is kept in memory and recovered:**
  - an internal, bounded recovery store keeps the context Kaleido tried to save (with the version it
    was based on) and its event;
  - a background recovery service re-applies that save as it was attempted. The version check makes
    this safe: if the process moved meanwhile, the outcome is parked for an operator, never applied
    blindly. Other failures are retried with backoff and parked after a maximum age;
  - while a process has a pending outcome, every step on it is refused with
    `409 pro_process_recovering` (nothing ran, so the key is released), so the handler is never
    run twice.
- **No durable recovery store.** The services usually keep the process state in the same database,
  so a durable copy would fail with it. Documented limits:
  - a restart loses the pending outcomes;
  - another instance does not see them (an `Idempotency-Key` still guards a keyed retry).

  Every outcome that is not recovered is logged at Critical with what to reconcile from: the process,
  the step, the request id, the version and state it would have saved, and the step's (redacted)
  event.
- **Telemetry goes through the Processor's observability seam,** like every Processor signal:
  - a `kaleido.processor.step.outcome_unsaved` event on the step activity and the
    `kaleido.processor.outcomes_unsaved` counter (tag `kaleido.processor.unsaved_outcome`);
  - a `kaleido.processor.recover` activity per attempt, with the `outcomes_recovered` and
    `outcomes_parked` counters (tag `kaleido.processor.park_reason`).
- **The consumer contract:** make a handler's external effects safe to see twice (an idempotency
  key from the process id and step), or run them after the commit from the step's event. Kaleido
  promises that an outcome is either saved, or the process is blocked until it is reconciled; never
  silently forgotten or run again by Kaleido.
- **MCP:** the agent is told the step ran and not to repeat it. A failure to read where a completed
  step continues is a note, not the step failing.

## Alternatives considered
- **Retry the save in core.** Rejected: it cannot tell a lost acknowledgement from a failure, and
  the provider already owns its transient-error policy.
- **A durable recovery store, or a sink the host provides.** Rejected for now: with the state in the
  same database it adds a store that fails at the same time. A write-only sink that Kaleido never
  reads would not stop a resend from running the handler again.
- **Wrap the handler in Kaleido's transaction.** Rejected: it rolls back only that database's
  writes, never external effects, and it holds locks across arbitrary calls.
- **Record the decision before running the handler (two-phase).** Not now: it is a larger model
  change, and the recovery store covers the transient case.

## Consequences
- A consumer can tell "the step ran but is not saved yet" (503) from "failed" and "refused".
  Consumers and agents must not resend on these codes.
- A short store outage heals itself: the state, the history and the event appear once the store is
  back, and the process admits steps again.
- An instance that restarts during an outage loses its pending outcomes. The Critical logs and the
  metrics are the operator's record.
- Source rule KAL0022 reports code that catches a handler's failure and a save's failure in one `try`.
