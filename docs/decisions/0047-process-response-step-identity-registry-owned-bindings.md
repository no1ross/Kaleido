# 0047. Process results stay neutral; Registry owns callable bindings

- **Status:** Accepted
- **Date:** 2026-11-17
- **Related:** #585, #619, #617, #618, #577, #610, #611, ADR 0040, ADR 0044, ADR 0045

## Context

The old Platform Process HTTP execute/state responses embedded `executeUrl` in next-step descriptions and mixed JSON payloads with Process results. The central Registry now publishes versioned `execute`, per-step and `state` operations in `ProcessRegistryItem`/step `Bindings.Http`. Generic Process already has public `ProcessRequest`, `ProcessorRequest`, `ProcessResult` and `ProcessStepResult`, but a durable `ProcessContext` is not itself a safe read response. A wire-only response hierarchy in Process.Http would duplicate the neutral result and leave future transports with different contracts. This is greenfield: no released HTTP/Registry result or stored data must remain compatible with old Platform responses.

## Decision

**Generic `Kaleido.Process` owns reusable Process request/result and state contracts.** An execute result identifies its process, the next required step by **name**, pending `InformationRequest`, available step **names**, state, step outcomes, messages, notice and hand-off target. It contains no `executeUrl`, Registry binding, JSON value or transport status. `ProcessStateSnapshot.FromContext` projects durable `ProcessContext` into a neutral read result with step names, pending information, current statuses, timestamps and caller evidence, but without store version or internal request IDs. A handler passes the target's existing `ProcessResult` plus its submitted step name to the neutral `ProcessStepHandlerResult.HandOff` overload, which derives the `HandOffContinuation` from the matching `ProcessStepResult`. No second public step-outcome wrapper is needed; future transports and clients reuse `ProcessResult.Steps` directly (#619).

`Kaleido.Process.Http` owns versioned URL/request **wire** only, including any JSON step-input envelope needed on the HTTP boundary; the HTTP server parses and authorizes those inputs, calls generic Process and serializes its neutral result through its HTTP options. JSON parsing, serialization, filtering, status codes, headers and idempotency replay encoding remain in the HTTP adapter or Process.Server admission owner, never in generic Process. Do not move `ProcessExecutionResponse`, `ProcessStateResponse`, `StepExecutionResponse` or duplicated Registry step metadata from old Platform into Process.Http as canonical response contracts merely to keep unpublished API shapes.

An outbound Process client chooses the current service version for new work or receives an **explicit version** for a continuing process. It resolves a required/available step's **name** against that version's published `execute` HTTP operation from the participant's hydrated **unfiltered Registry**, validates the route before sending credentials and adapts the transport response into the neutral result contract. A process id does not identify a service version. HTTP server mapping stays local to the Process runtime, independent of central Registry participation for ordinary local execution.

## Alternatives considered

- **Preserve old `executeUrl` response fields for compatibility.** Rejected: no release requires a shim, and it would create a second, stale routing authority.
- **Put Registry `ProcessStepSummary`/`Bindings.Http` into each execute/state answer.** Rejected: execution outcomes are not discovery documents; the Registry alone owns callable bindings.
- **Copy old Platform HTTP responses into Process.Http as the primary client contract.** Rejected: generic Process already owns the reusable request/result, and future transports would need wrappers around HTTP-derived models. #619 completes only the missing neutral state/step output semantics.
- **Infer a version from a process id.** Rejected by ADR 0040: an ongoing client must be pinned explicitly. How a cross-service hand-off names its target version is part of the subsequent Process server/client integration gate, not a license to invent an unversioned URL.

## Consequences

Old preview HTTP response JSON and public CLR types are deliberately replaced, without a data migration. #619 and #617 run as distinct lower project units before #618 Process.Http.Server; each has a fresh source/test/API/docs gate. The eventual Process.Http.Client must prove route resolution from the already-hydrated unfiltered Registry, never from a local catalog, response URL or per-call central read. #587's HTTP idempotency replay/provider work remains separate; no released Registry SQLite data exists to migrate.
