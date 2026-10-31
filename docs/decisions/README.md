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
| [0013](./0013-event-payloads-classified-and-redacted.md) | Event payloads stay complete; classified values are redacted before the outbox | Accepted; its redaction policy amended by [0025](./0025-event-redaction-policy.md) | #139 |
| [0014](./0014-kaleido-telemetry-only-what-kaleido-observes.md) | Kaleido emits telemetry only for what Kaleido alone can observe; OTel parts are switchable | Accepted | #109 |
| [0015](./0015-declared-step-behavior.md) | Step behavior (destructive, external, confirmation) is declared; idempotence derived; classifications published | Accepted | #137 |
| [0016](./0016-mcp-front-door-tool-listing.md) | The MCP adapter can list a front door (derived from initial steps and their lookups); open steps arrive in `next` and run with `call_tool` | Accepted | #338 |
| [0017](./0017-transport-contract-placement.md) | Transport contracts (including the error body) live in `Kaleido.Http.Abstractions` under the `Kaleido.Http` namespace; `IRegistrySnapshotStore` stays a public async seam; amended: shared core shapes are supported wire contracts, and the behavior Abstractions holds is listed | Accepted | #298, #293, #448 |
| [0018](./0018-options-are-startup-inputs.md) | Options are startup inputs: no defensive copies; KAL0015 stays the immutability rule, no deep-immutability analyzer | Superseded in part by 0030 | #294, #305 |
| [0019](./0019-idempotent-execution-admission.md) | Idempotent execution: an `Idempotency-Key` is admitted before its step runs; at most once per key, not exactly-once external work | Accepted | #285 |
| [0020](./0020-three-store-contracts.md) | Three store contracts, one concern each: process state, event outbox, idempotent requests; the context and outbox stores share a database | Accepted; its store coupling amended by [0023](./0023-outbox-integration.md) | #285 |
| [0021](./0021-authenticated-by-default.md) | Authorization is enforced by default (`Authenticated`); an open host sets `None` explicitly | Accepted | #288 |
| [0022](./0022-call-credentials.md) | One credential model for every outgoing call: a host-owned provider per destination and purpose; a caller's call never uses the host's own credentials. Provenance behind a front door: informational origin end to end, trusted actor per hop | Accepted; `ForwardCallerToken()` guidance amended by [0027](./0027-forward-caller-token-kept-with-guards.md) | #287, #402 |
| [0023](./0023-outbox-integration.md) | Outbox integration: Kaleido owns *when* an event belongs to a state transition, the outbox integration owns *how* it is stored and delivered; built-in outbox (default), the host's own, or direct publication; transactions from persistence packages | Accepted | #408 |
| [0024](./0024-conditional-questions.md) | Conditional questions (`enableWhen`): Kaleido decides which questions apply - in validation, the MCP adapter's rounds and UIs - never the agent | Accepted | #235 |
| [0025](./0025-event-redaction-policy.md) | Events have their own redaction policy (`AddEventRedaction`), separate from the host's log redaction (`AddRedaction`) | Accepted; erratum on the method's name (#457) | #278, #457 |
| [0026](./0026-versioning-concept.md) | Versions are labels; one live version per capability (a breaking change takes a new name); in-flight processes continue on the deployed definition, and steps keep the version they ran with | Superseded in part by 0029 | #197 |
| [0028](./0028-stores-are-chosen-explicitly.md) | Every store a service uses is chosen explicitly: a store left at its in-memory default stops the host at startup (`stores_not_chosen`); memory is chosen with `AddInMemoryStores()`; only the stores in use are checked | Accepted | #438 |
| [0029](./0029-capabilities-have-no-version.md) | Capabilities have no version: the name is the identity; `Version` is removed from the attributes, registry, wire, state, events and telemetry; ADR 0026's other rules stand | Accepted | #474 |
| [0030](./0030-options-are-immutable-configuration-overrides-code.md) | Options are immutable records with no builders; registration takes an optional options object; configuration overrides code setting by setting; Kaleido never changes a supplied value (invalid values stop startup) | Accepted | #445 |
| [0031](./0031-information-requests-have-an-exchange-identity.md) | An information request has an exchange identity issued by Kaleido for each time the questions are put (kept across conditional rounds); answers echo it and answers to an earlier exchange are rejected (`pro_information_response_stale`) before any handler runs | Accepted | #442 |
| [0032](./0032-public-api-at-1-0.md) | The public API at 1.0, reviewed type by type: every public type kept in a role (contract, seam, DTO/options, constant); `missing_step_result` and `missing_base_url` take their area prefixes | Accepted | #447 |
| [0033](./0033-process-lifecycle-and-admission.md) | A process ends as complete (nothing available, or a handler's `Complete()`), a business failure (a hard stop) or a hand-off (the target owns it; a hand-back reopens it); an ended process admits no step (`pro_process_ended`), and only a handler result changes the state | Accepted | #512, #534 |
| [0034](./0034-unsaved-step-outcomes.md) | A step whose outcome cannot be saved: answered as having run (`pro_outcome_pending_recovery` / `pro_outcome_not_saved`), kept in memory and saved by a recovery service when the store is back, the process refused meanwhile (`pro_process_recovering`); retries belong to the store provider | Accepted | #537 |
| [0027](./0027-forward-caller-token-kept-with-guards.md) | `ForwardCallerToken()` stays as a stated single-audience choice (OAuth-protected MCP uses a per-service exchange); credentials sent over plain HTTP are reported at startup; a client's route prefix must be a service name | Accepted | #434 |

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
