# Information requests

Kaleido processes already tell a client *which step is next*. Some steps can't say *what
information they need* until runtime: the questions come from an outside system (clinical
algorithms, site-of-care rules, client configuration). An **information request** is the
standard way for a process to say "here are the questions this step needs answered". Any
client (portal, IVR, AI agent) sends the answers back in a standard shape.

Kaleido owns the **schema**, never the **content**. It carries the questions, records that the
process is waiting for them, and checks that the answers fit. What to ask, the wording, the
options and what an answer means all belong to the implementer
([ADR 0003](./decisions/0003-schema-not-content.md)).

## Two kinds of steps

| | Step with properties | Information step |
|---|---|---|
| **When** | The required information is known up front (member ID, date of birth, procedure code) | The questions are decided at runtime |
| **Shape** | `IProcessStep` record with typed properties + validation attributes | `IInformationStep`: only `InformationRequestId` and `Items` (KAL2015, startup `pro_invalid_registration`) |
| **Describes its input with** | Properties, `[Description]`, `[Display(Name, Prompt, Description)]` (published in the registry; hints for UIs and AI agents) | The pending information request on the required step |
| **Validation** | Data annotations | Structural checks against the pending request |

The same data never lives in both places. `[Repeatable]` means a step may run again; nothing
repeats by default. A `[Repeatable]` information step can loop: its handler requires itself
with the next request (adaptive rounds).

## The round trip

1. **Ask.** A handler that learns there are questions requires the information step with them:

   ```csharp
   return ProcessStepHandlerResult.RequireInformation<CaptureOutOfNetworkResponseStep>(request);
   // or ProcessStepHandlerResult<TResult>.RequireInformation<TNext>(response, request)
   ```

   `TNext` must be an `IInformationStep` (a compile-time constraint). A request only ever
   travels with the **required** step. An information step required with `Success<TNext>()`
   fails with `pro_information_request_missing` (and KAL2016 at compile time).
2. **Wait.** The process state becomes `AwaitingInformation`. The response's `requiredStep` is
   an object carrying the request:

   ```json
   "requiredStep": {
     "name": "CaptureOutOfNetworkResponseStep",
     "displayName": "Out-of-network responses",
     "description": "Answers the out-of-network attestation.",
     "version": "1.0",
     "repeatable": false,
     "isInformationStep": true,
     "executeUrl": "/intake/processes/steps/captureoutofnetworkresponsestep",
     "informationRequest": {
       "informationRequestId": "out-of-network-attestation",
       "title": "Out-of-network attestation",
       "items": [
         { "id": "reason", "text": "Why is an out-of-network provider needed?", "type": "choice",
           "repeats": false, "options": [ { "value": "no-in-network", "display": "No in-network provider available" } ],
           "items": [] }
       ]
     }
   }
   ```

   `GET /{service}/processes/{processId}` returns the same pending request. It is stored with
   the state; reading the state never builds a request.
3. **Answer.** The client submits the information step with the answers as its payload:

   ```json
   { "processStep": { "informationRequestId": "out-of-network-attestation",
                      "items": [ { "itemId": "reason", "answers": [ { "value": "no-in-network" } ] } ] } }
   ```

4. **Check.** Before the handler runs, Kaleido validates the answers against the pending
   request ([below](#validation)). If they don't fit, the step is rejected and the request
   stays pending.
5. **Continue.** The handler reads `Items`, translates the answers into domain values and
   returns as usual: `Success…`, `RequireInformation…` (another round), `Failure` or `HandOff`.
   The next outcome replaces or clears the pending request.

**Only next steps run.** While a request is pending, only the required information step can
execute. Any other step is rejected with `pro_step_not_available`, so a question can't be
bypassed. When nothing is required, a request may start with any step available under the
process rules.

## Contracts

The contracts are modelled on the **structure** of the FHIR R4 Questionnaire /
QuestionnaireResponse: a tree of items with ids, types, options and nested items, and answers
keyed by item id. They use Kaleido's own schema; Kaleido is not a FHIR framework
([ADR 0002](./decisions/0002-information-request-contract.md)).

| Type | Fields |
|---|---|
| `InformationRequest` | `informationRequestId` (required, set by you), `title`, `items` |
| `InformationItem` | `id` (unique in the request), `text` (verbatim), `type`, `repeats`, `options`, `items` (groups only) |
| `InformationOption` | `value` (what is submitted), `display` |
| `IInformationStep` (the answers) | `informationRequestId`, `items` |
| `InformationResponseItem` | `itemId`, `answers`, optional nested `items` (mirroring groups) |
| `InformationAnswer` | `value` (text) |

Item types and the answers they accept:

| `type` | Answer `value` |
|---|---|
| `text`, `longText` | Any non-empty text |
| `boolean` | `true` or `false` |
| `wholeNumber` | An integer, e.g. `42` |
| `number` | A number, invariant culture, e.g. `12.5` |
| `date` | `yyyy-MM-dd` |
| `dateTime` | ISO 8601, e.g. `2024-05-01T13:30:00Z` |
| `choice` | One of the item's option `value`s (several when `repeats`) |
| `display` | No answer (text to show) |
| `group` | No answer (contains nested items) |

`informationRequestId` names the questionnaire (e.g. `out-of-network-attestation`). It isn't the
correlation `RequestId`, which stays in the process state and off the contracts.

## Validation

Kaleido checks shape only, never meaning:

- **Request** (when a handler returns one), `pro_information_request_invalid`:
  - an `informationRequestId` and at least one item;
  - unique item ids, and text on every item;
  - choice items have options with unique, non-empty values;
  - only groups have nested items, and every group has some.
- **Response**, against the pending request:
  - `pro_information_response_mismatch`: the step isn't the pending required step, or the `informationRequestId` differs (answers for another or an earlier round);
  - `pro_information_response_unanswered`: every question (not `display`/`group`) must be answered. There is no optional question: if you ask, you need an answer; "none of the above" is an option you provide;
  - `pro_information_response_invalid_answer`: unknown item ids, an answer that doesn't fit its type, a choice that wasn't offered, or several answers to a question that doesn't `repeat`.

Whether the questions were the right ones, and what the answers mean, is the domain's
responsibility. Converting answers to internal representations (units, codes) is the handler's
job.

## State, events and history

- **State** holds only the pending request (`ProcessorContext.RequiredInformationRequest`). It
  sits next to `RequiredStep` and is tied to the execution that produced it by
  `LatestRequestId`. Stores persist it (the SQLite reference store keeps it as JSON on the
  required-step row); a consumer `IProcessorContextStore` must persist it too.
- **No history.** Kaleido doesn't keep past questions or answers. Recording them is yours.
  `StepCompleted` already carries each step's payload (the answers), and `PlanBuilt` /
  `StepCompleted` / `ExecutionCompleted` carry `RequiredInformationRequest`.
- **Who answered** will be tagged on events by #193; until then, events carry no caller
  identity.

## Wording and verbatim text

- Question and display text, like business messages, is delivered **verbatim**. Clients must
  not paraphrase it.
- `[Display]` text on property-based steps is a **hint**: a UI, IVR or agent may phrase the
  question its own way.
- Kaleido can't tell a person from a process (it only sees the caller's identity). Guarding
  against an AI intermediary inventing answers belongs to the MCP adapter (#140, elicitation),
  not to core.

## Observability

- **Step activity tags:** `kaleido.processor.step_kind` (`properties` / `information`) and
  `kaleido.processor.information_request_id`.
- **Counters:** `kaleido.processor.information_requests` (requests presented, per step) and
  `kaleido.processor.information_responses_rejected` (per step and
  `kaleido.processor.rejection_code`).
- There is no waiting-time metric. Cases span days across disconnected systems; derive it
  from events if needed.

## Analyzers

KAL2015 (information-step shape), KAL2016 (`Success<TNext>` with an information step),
KAL2017/KAL2018 (describe step and query inputs). See [`ANALYZERS.md`](./ANALYZERS.md).

## Sample

PriorAuth's `CaptureMriInfoStep` and `ConfirmCtInsteadOfMriStep` are information steps. The
PriorAuth UI renders any request with one generic form (`information-request-form`). The
sample's question builder is interim: the MRI questions come from configuration and the CT
confirmation is written in code. #209 replaces it with a generic, configuration-driven builder.
