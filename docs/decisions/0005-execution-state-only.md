# 0005. Kaleido stores only the pending request; history is the implementer's

- **Status:** Accepted
- **Date:** 2026-10-08
- **Related:** #142, #193

## Context
Answers must be validated against exactly what was presented, and a client must be able to
re-render the pending questions. Storing more, such as previous rounds or answers, turns
process state into an audit system.

## Decision
- The process state stores **only the pending request**
  (`ProcessorContext.RequiredInformationRequest`), whole, as presented. It sits next to
  `RequiredStep` and is tied to the producing execution by the existing `LatestRequestId`.
- The next outcome replaces or clears it. Exceptions and cancellation clear it with the
  required step.
- Kaleido keeps **no history** of questions or answers. Recording them is the implementer's
  concern. `StepCompleted` carries each step's payload (the answers) and the presented
  request.
- Store contract change: every `IProcessorContextStore` persists the new field. The SQLite
  reference store uses a JSON column on the required-step row.

## Alternatives considered
- **The latest exchange per step** (last request and last answers on every `StepContext`): that
  is history in state, and it grows the store contract.
- **A reference only** (an id, without the request): the wording can change between
  presentation and answer, so validation must use the request as presented.
- **Events only:** the default publisher discards events, so state must be self-sufficient.

## Consequences
- State stays "where are we now".
- Without #193, events carry no caller identity, so "who answered" isn't recorded yet.
