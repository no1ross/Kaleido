# Architecture decision records

Framework-shaping decisions, with the alternatives considered and why. Issues and PRs track
*work*; these records keep the *reasoning*, which often spans several issues.

## Process

- One decision per file: `NNNN-short-title.md`, numbered in the order written.
- Status is **Proposed**, **Accepted** or **Superseded by NNNN**.
- Records are never rewritten. To change a decision, write a new record and mark the old one
  superseded.
- Each record links the issues and PRs it relates to.
- Any change that shapes the framework (its contracts, boundaries or principles) gets a record.

## Index

| # | Decision | Status | Related |
|---|---|---|---|
| [0001](./0001-process-engine-not-decision-maker.md) | Kaleido is a process engine, never a business decision maker | Accepted | foundational |
| [0002](./0002-information-request-contract.md) | Information requests use Kaleido's own contract, modelled on the FHIR Questionnaire structure | Accepted | #142 |
| [0003](./0003-schema-not-content.md) | The framework owns the schema, the implementer owns the content; two kinds of steps | Accepted | #142 |
| [0004](./0004-requests-travel-with-the-required-step.md) | Information requests travel only with the required step; `AwaitingInformation`; `RequiredStep` as an object | Accepted | #142 |
| [0005](./0005-execution-state-only.md) | Kaleido stores only the pending request; history is the implementer's | Accepted | #142 |
| [0006](./0006-structural-validation-and-next-step-gate.md) | Structural-only answer validation; only next steps run | Accepted | #142 |
| [0007](./0007-input-metadata-for-ai.md) | Input metadata is a published hint, nudged by analyzer warnings; verbatim text; AI safeguards live in the adapter | Accepted | #142, #140 |

Earlier decisions still to be recorded (correlation model, authorization responses, error-code
ownership, telemetry, health checks, event envelopes, case ownership and identity tagging) are
tracked in their own issues.

## Template

```markdown
# NNNN. Title

- **Status:** Proposed | Accepted | Superseded by NNNN
- **Date:** YYYY-MM-DD
- **Related:** #issue, #pr

## Context
What forces the decision.

## Decision
What we decided.

## Alternatives considered
Each alternative and why it was not chosen.

## Consequences
What follows, good and bad.
```
