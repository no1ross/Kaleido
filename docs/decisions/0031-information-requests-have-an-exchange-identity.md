# 0031. An information request has an exchange identity, issued by Kaleido

- **Status:** Accepted
- **Date:** 2026-10-27
- **Related:** #442, ADRs 0002-0006, ADR 0024

## Context
Answers to an information request were checked against the pending request by its
`InformationRequestId` - a questionnaire identifier the implementer chooses (ADR 0002). A repeatable
information step can require itself again with the same questionnaire: same id, same item ids. A
delayed answer, a stale browser form or a replayed MCP continuation from the first time then fitted the
second one and was accepted as its answers. Optimistic concurrency guards concurrent writes, not a late
old answer.

## Decision
1. **Every time questions are put is an exchange with its own id.** `InformationRequest.ExchangeId` is
   issued by Kaleido (a new GUID, `N` format) whenever a handler returns `RequireInformation`; a value
   the handler sets is replaced. The questionnaire id stays the implementer's.
2. **The rounds of one request are one exchange.** Conditional questions (ADR 0024) are rounds of the
   one pending request: the exchange id does not change between them.
3. **Answers echo it.** `IInformationStep` declares `ExchangeId` next to `InformationRequestId` and
   `Items` (KAL2012 and the startup shape check allow exactly these three). Answers carrying another
   exchange's id are rejected with `pro_information_response_stale`, checked first, before any handler
   runs.
4. **It is part of the pending request.** Persisted with it (process state, the SQLite store's request
   JSON), so it survives a restart, and published wherever the request is (state and step responses,
   events).
5. **The MCP adapter captures it when the questions are shown.** The continuation state of a form and
   the `answersToken` given to the agent (now with every set of questions) carry it; the adapter submits
   that captured id, never the one pending at submission time, and refuses a form or token from an
   earlier exchange before submitting. The `answer_information_request` arguments are unchanged; the
   agent is told to pass the token back.

## Alternatives considered
- **Require a unique `InformationRequestId` per exchange.** Puts the burden on every implementer and
  mixes questionnaire identity with exchange identity; nothing would enforce it. Rejected.
- **Rename `InformationRequestId` to `QuestionnaireId`.** Clearer, but a breaking rename across the
  wire, events and every implementer for no behavior change; the exchange id alone closes the gap.
  Not done.
- **Compare the process version instead.** The version also moves for unrelated steps, so a valid
  answer would be refused after any other change to the process. Rejected.

## Consequences
- Breaking (preview): every information step type declares `ExchangeId`; clients echo the
  `exchangeId` they received with the questions. Answers without it are stale.
- MCP agents get an `answersToken` with every set of questions and pass it back.
