# 0006. Structural-only answer validation; only next steps run

- **Status:** Accepted
- **Date:** 2026-10-08
- **Related:** #142, #138

## Context
Kaleido can guarantee that answers fit the questions that were asked. It can't know whether
they are right. A pending request is also pointless if a client can simply run another step
instead.

## Decision
- **Validation is structural only**, before the handler runs:
  - the request is well-formed (`pro_information_request_invalid`);
  - the answers match the pending request: the same `informationRequestId`
    (`pro_information_response_mismatch`), every question answered
    (`pro_information_response_unanswered`), and every answer fitting its type, with choices
    from the offered options (`pro_information_response_invalid_answer`).
- **Every presented question must be answered.** There are no optional questions: "none of the
  above" is an option the domain provides.
- One table-driven type check covers the supported item types. Kaleido never converts or
  interprets values.
- **Only next steps run** (`pro_step_not_available`). The first step a request executes must
  be the pending required step or, when nothing is required, a step available under the
  process rules (the initial steps for a new process). Later steps in the same request are
  chained by the evaluator. An information step is never chained into within the same
  request, because its questions must be presented first.

## Alternatives considered
- **Semantic validation:** impossible; the content belongs to the domain.
- **Optional questions:** these add a `required` flag and its edge cases for little value.
- **Gating only steps that bypass a pending question:** this leaves the general step graph
  advisory. The owner chose to enforce it everywhere.

## Consequences
- Answers to an earlier or a different round are rejected by id.
- Any client that skipped ahead now gets `pro_step_not_available` and must follow the declared
  graph.
- Concurrency and idempotency of answers remain #138.
