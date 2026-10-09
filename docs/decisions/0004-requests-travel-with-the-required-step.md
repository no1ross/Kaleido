# 0004. Information requests travel only with the required step; `AwaitingInformation`; `RequiredStep` as an object

- **Status:** Accepted
- **Date:** 2026-10-08
- **Related:** #142, #198

## Context
Usually the step that learns *whether* questions are needed is not the step that answers
them. A servicing-provider step, for example, may discover an out-of-network situation that
must be attested. Consumers need to know, in one response, what to do next and what to ask.

## Decision
- **Producer = the handler result.** A handler returns
  `RequireInformation<TNext>(request)`, with `TNext : IInformationStep` as a compile-time
  constraint. Any handler may produce a request; whether it does is business logic.
- **Receiver = the step type.** The required information step receives the answers.
- **A request only travels with the required step.** There are no builders for available
  steps and no eager building. Plain `Success<TNext>()` carries none. Requiring an information
  step without a request is a process violation (`pro_information_request_missing`; KAL2016).
- **New decision and state, `AwaitingInformation`,** distinct from `AwaitingRequiredStep`.
- **`RequiredStep` is an object** on step, multi-step and state responses: the same shape as an
  available step, plus `informationRequest`. Events carry `RequiredInformationRequest`.
- **"A step isn't done until it can get the next question":** if the downstream system can't
  produce the questions, the producing step fails.
- **Handoff:** the target processor owns its next steps and requests. Returning them through
  the originating processor is #198.

## Alternatives considered
- **Each information step builds its own questions when it becomes next** (eagerly, including
  for available steps): it reverses where the knowledge lives, runs builders for steps that
  are rarely chosen, and adds latency.
- **A sibling `questionnaire` property on responses:** the request belongs to the step. A
  separate property kept `RequiredStep` a string while available steps were objects.
- **Reusing `AwaitingRequiredStep`:** UIs, agents and dashboards benefit from telling "do step
  X" apart from "answer these questions", especially for looping steps.

## Consequences
- A breaking wire change (pre-release): `requiredStep` is an object. Both sample UIs read
  `requiredStep.name`.
- Clients render the request from the response or from `GET` state. Reading the state never
  builds a request.
