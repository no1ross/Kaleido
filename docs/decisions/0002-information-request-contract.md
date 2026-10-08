# 0002. Information requests use Kaleido's own contract, modelled on the FHIR Questionnaire structure

- **Status:** Accepted
- **Date:** 2026-10-08
- **Related:** #142

## Context
Steps whose questions are decided at runtime need a transport-neutral way to present questions
and receive answers. The shape must suit portals, IVRs and AI agents, and must survive many
rounds (adaptive questioning).

## Decision
- `InformationRequest { informationRequestId, title, items }` and
  `InformationItem { id, text, type, repeats, options, items }`. The answers are the information
  step's payload: `{ informationRequestId, items: [{ itemId, answers: [{ value }] }] }`.
- The **structure** follows the FHIR R4 Questionnaire / QuestionnaireResponse: a tree of
  items, ids, types, options, groups, and answers keyed by item id. The **schema** is Kaleido's:
  no FHIR names, `resourceType`, canonical references or status codes, and no FHIR SDK.
- Item types: `text`, `longText`, `boolean`, `wholeNumber`, `number`, `date`, `dateTime`,
  `choice`, `display`, `group`. Answers are text interpreted by the item type.
- `informationRequestId` is required and set by the implementer; it names the questionnaire.
  The correlation `RequestId` stays in the process state, off the contracts.
- Not in the first slice: conditional items (`enableWhen`), search-backed answer lists,
  translations.

## Alternatives considered
- **The exact FHIR schema:** implies Kaleido is a FHIR framework, and leaks FHIR-specific names
  and status codes (`in-progress`, `entered-in-error`) into a domain-neutral framework.
  Translators can map to FHIR where needed.
- **JSON Schema / JSON Forms:** describes data, not a question-and-answer conversation (no
  display text, options or rounds).
- **A FHIR SDK:** heavy, and the same "FHIR framework" implication.
- **A request id on the response only, or none:** without an id the response can't say which
  questionnaire it answers, so answers to another or an earlier round can't be rejected.

## Consequences
- A proven, domain-neutral structure that existing renderers and translators understand.
- The contract is Kaleido's to evolve; adding `enableWhen` etc. later is additive.
