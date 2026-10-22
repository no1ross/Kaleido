# 0013. Event payloads stay complete; classified values are redacted before the outbox

- **Status:** Accepted; the redaction policy is amended by [ADR 0025](./0025-event-redaction-policy.md): events have their own policy (`AddEventRedaction`), separate from the host's log redaction (`AddRedaction`)
- **Date:** 2026-10-12
- **Related:** #139, ADR 0001, ADR 0012

## Context
Events carry full payloads: a step's request and response, a query's records. #139 (from an AI
review) proposed metadata-only events. But events are not for AI - nothing in Kaleido feeds events
to an agent - they feed analytics, reporting, the warehouse and the audit trail. An audit has to
answer "what did the provider see when they selected this?", months later, after the data has
changed: without the payload it cannot. The real concern is the data footprint: PHI/PII copied into
event infrastructure. Since ADR 0012 the outbox also stores the payload before the publisher sees
it, so "redact in your publisher" is too late for the outbox table. Logs and trace tags carry ids
only.

## Decision
- **Payloads stay complete** (no metadata-only default, no size cap).
- **Sensitive values are redacted before the outbox write**, so the raw value reaches neither the
  outbox nor the publisher. Responses to the caller, logs and traces are not affected.
- **Data classification with .NET's standard** `Microsoft.Extensions.Compliance`: implementers mark
  properties of their view, source and step types with their own `DataClassificationAttribute`
  subclasses and choose a redactor per classification with `AddRedaction` - erase, HMAC
  (pseudonymize: correlation without the value), or an own encrypting redactor (the real value later,
  for whoever holds the key). Kaleido applies it while serializing the payload, also inside the
  `object`-typed members (request, response, records).
- **Safe default:** a classified value with no registered `IRedactorProvider` is erased, with a
  one-time warning.
- **`IKaleidoEventRedactor`** (`AddEventRedactor<T>()`, several in order) for what attributes cannot
  express: information-request answers (generic items), per-view or per-caller rules, dropping a
  record set. It runs on the typed envelope before classification.

## Alternatives considered
- **Metadata-only events.** Rejected: destroys the audit trail; not needed for AI.
- **Drop query records by default.** Rejected: "what did the provider see" is exactly the records.
- **Redaction in the publisher only.** Rejected: the outbox already holds the raw payload.
- **A Kaleido-specific `[Sensitive]` attribute.** Rejected: .NET has a standard classification and
  redaction stack, shared with logging; implementers configure it once.

## Consequences
- Classification is opt-in per field; unclassified sensitive fields still flow (the hook is the
  catch-all). The decision what is sensitive, and how it is redacted, stays with the implementer
  (ADR 0001).
- A redacted field is a string in the event payload, whatever its original type.
- Core depends on `Microsoft.Extensions.Compliance.Abstractions` (already in the graph); hosts that
  choose redactors reference `Microsoft.Extensions.Compliance.Redaction`.
- For positional records, the attribute needs the `property:` target (`[property: PHI]`).
