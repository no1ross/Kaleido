# 0008. Correctable input is rejected without changing the process; unexpected errors are retryable

- **Status:** Accepted
- **Date:** 2026-10-10
- **Related:** #237, #238, #140

## Context
A handler had two ways to say "no": `Failure`, which ends the process in `BusinessFailure`, and
throwing. Both were terminal. In a live run, one unroutable procedure code ended a whole prior
authorization (the member and everything captured were lost), and one bug in a handler ended
another. A caller who mistypes a code, or whose member is not eligible on that date, should be
able to correct it and carry on. A defect, once fixed, should not have killed every request that
hit it.

The failed step was also recorded as `Completed` in process state, so readers were told a failed
step had completed (#238).

## Decision
- **`Reject(messages)`** is a third handler result, for input the caller can correct or a call
  that can be retried: the step is **not** completed and the process is left **exactly as it
  was** (state, required step, pending request, available steps). Nothing is saved. The outcome
  is `Rejected` and carries the handler's business messages. No later candidate of the same
  request runs.
- **`Failure(messages)` stays terminal** and is for real business endings: the request cannot
  proceed. The failed step is recorded as `Failed`, not `Completed`.
- **An unexpected exception leaves the process as it was before the step.** The step's status
  records `Exception`; the outcome is `Failed` with `pro_framework_exception`; the process keeps
  its previous state and next steps, so the step can be retried once the defect is fixed.
- Kaleido decides none of this: which input is correctable and which outcome ends a request is
  the handler's choice (ADR 0001). Kaleido only guarantees that a rejection changes nothing.

## Alternatives considered
- **Make `Failure` non-terminal.** Rejected: some outcomes really do end a request (e.g. the
  caller declines the only service that can be offered), and every existing `Failure` would
  silently change meaning.
- **Keep exceptions terminal.** Rejected: an exception is a defect, not a business outcome; the
  process state is still valid and the step can simply be retried.
- **Roll back handler side effects on rejection.** Not possible for Kaleido: the handler owns its
  storage and downstream calls. A rejecting handler must not have written anything it would need
  to undo, or must be safe to run again.

## Consequences
- Handlers choose between `Reject` (correctable, retryable) and `Failure` (ends the request).
  A handler that rejects after writing must be idempotent on retry (the PriorAuth sample checks
  eligibility before writing, and does not add a requested service twice).
- `StepExecutionOutcome.Rejected`, `StepExecutionStatus.Failed` and `StepExecutionStatus.Rejected`
  are new values on the wire (additive). The MCP adapter tells the agent that a rejected step
  changed nothing and lists the unchanged next steps; process state lists `failedSteps`
  separately from `completedSteps`.
- `ProcessExecutionState.Exception` is no longer set by the runtime.