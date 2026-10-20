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
| [0008](./0008-rejected-outcome-and-retryable-exceptions.md) | Correctable input is rejected without changing the process; unexpected errors are retryable | Accepted | #237, #238 |
| [0009](./0009-unknown-process-id-only-on-handoff.md) | An unknown process id starts a process only on a handoff | Accepted | #140 |
| [0010](./0010-no-case-level-guardrails.md) | Kaleido has no case-level guardrails: no process ownership, no transfer | Accepted | #194 |
| [0011](./0011-mcp-lists-every-tool-the-caller-may-use.md) | The MCP adapter lists every tool the caller may use; the process stays the gate | Superseded by 0016 | #230 |
| [0012](./0012-events-at-least-once-through-the-store.md) | Events reach the publisher at least once, through an outbox in the store | Accepted | #240 |
| [0013](./0013-event-payloads-classified-and-redacted.md) | Event payloads stay complete; classified values are redacted before the outbox | Accepted | #139 |
| [0014](./0014-kaleido-telemetry-only-what-kaleido-observes.md) | Kaleido emits telemetry only for what Kaleido alone can observe; OTel parts are switchable | Accepted | #109 |
| [0015](./0015-declared-step-behavior.md) | Step behavior (destructive, external, confirmation) is declared; idempotence derived; classifications published | Accepted | #137 |
| [0016](./0016-mcp-front-door-tool-listing.md) | The MCP adapter can list a front door (derived from initial steps and their lookups); open steps arrive in `next` and run with `call_tool` | Accepted | #338 |
| [0017](./0017-transport-contract-placement.md) | Transport contracts (including the error body) live in `Kaleido.Http.Abstractions` under the `Kaleido.Http` namespace; `IRegistrySnapshotStore` stays a public async seam | Accepted | #298, #293 |
| [0018](./0018-options-are-startup-inputs.md) | Options are startup inputs: no defensive copies; KAL0018 stays the immutability rule, no deep-immutability analyzer | Accepted | #294, #305 |
| [0019](./0019-idempotent-execution-admission.md) | Idempotent execution: an `Idempotency-Key` is admitted before its step runs; at most once per key, not exactly-once external work | Accepted | #285 |
| [0020](./0020-three-store-contracts.md) | Three store contracts, one concern each: process state, event outbox, idempotent requests; the context and outbox stores share a database | Accepted | #285 |
| [0021](./0021-authenticated-by-default.md) | Authorization is enforced by default (`Authenticated`); an open host sets `None` explicitly | Accepted | #288 |

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
