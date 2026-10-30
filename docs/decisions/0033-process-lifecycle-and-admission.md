# 0033. Process lifecycle: when a process ends, and what it admits

- **Status:** Accepted
- **Date:** 2026-10-30
- **Related:** #512, #534, #536, #517, #522, ADR 0001, ADR 0006, ADR 0008, ADR 0009

## Context
Admission (`StepCandidateNextStepChecker`) recomputed the available steps from the step graph alone
and never read the process state. The API said one thing and the runtime did another:
- `Failure()` was documented as ending the process, yet steps that had been available before it
  still ran;
- after a hand-off, a local step still ran and erased the target;
- the state endpoint published an empty list for an ended process, while execution admitted the
  recomputed one.

Real processes rarely end where the graph runs out:
- a prior authorization has phases ("promote to case" closes the request steps and opens audit and
  appeal);
- it may be re-opened or appealed several times;
- it truly ends on an explicit, usually internal decision, such as a job applying the turnaround time
  once the appeal window has passed, or a final audit.

## Decision
- **Phases are the step graph's.** `AvailableAfter` and `AvailableUntil` open and close steps
  (ADR 0006). Kaleido draws no business conclusion from them (ADR 0001).
- **A process ends in three ways, and an ended process admits no further step of this service.**
  A step sent to it is refused with `pro_process_ended`, whose message names the state:
  - **Complete:** the graph offers nothing more (derived), or a handler returned
    `ProcessStepHandlerResult.Complete(...)` (#534). The latter ends the process **whatever the graph
    still offers**. It typically comes from an internal-only step (e.g. `[KaleidoAuthorization(Policy =
    "InternalCaller")]`) that a job calls through a Kaleido client. Such a step runs after other steps,
    never as an entry step (KAL2022).
  - **BusinessFailure:** a handler returned `Failure(...)`, a hard stop ("this request cannot
    proceed": not eligible, a disaster rule applies). Nobody continues it; the caller starts a new
    process. Input the caller can correct is `Reject(...)`, which changes nothing (ADR 0008).
    Re-opening or appealing a business decision is a successful step, never a failure.
  - **HandOff:** the process belongs to the target service. This service refuses its own steps; the
    target name is kept and never erased by a local step. There is one exception, a **hand-back**: a
    request from the service holding the process (its calling processor is the target) reopens it
    here, and it continues from where its step graph stands. One processor holds a process at a time.
- **Nothing else changes the process state** (#536). A process violation (#522), an unexpected error,
  a validation failure and a request abort (#517) only record the step's status. The process keeps
  its state and its next steps, and the step can be sent again.
- **What the state shows, admission allows.** An ended process publishes no available step, and every
  attempt to run one is refused.

## Alternatives considered
- **Keep the graph as the only authority** (an ended process runs whatever the graph still offers).
  Rejected: it contradicts `Failure()` and hand-offs, and the state and the runtime disagree.
- **Let `Failure()` keep the graph open, so steps can recover from it.** Rejected by the owner: a
  failure is a hard stop; recovery after a business decision is modelled as a successful step.
- **Declare terminal steps with an attribute** instead of a handler result. Rejected: whether a call
  ends the process is the handler's decision at run time (the TAT job decides "now"), not a property
  of the step type.
- **Never accept a hand-back.** Rejected: services hand processes back (a specialist returns a case),
  and refusing it would force a new process id.

## Consequences
- A consumer must start a new process after `businessFailure`, and continue on the named service
  after a hand-off. Refused steps come back as `pending` with `pro_process_ended`.
- A process that needs steps after its business outcome (audit, appeal) keeps them open in the graph,
  and ends with an explicit `Complete()` when it truly ends.
- New steps deployed later (ADR 0026) are not offered on a complete process.
- The `processes` guide, the enum and handler-result docs, `ERROR_CODES.md` and the CHANGELOG
  describe the rules.
