# 0009. An unknown process id starts a process only on a handoff

- **Status:** Accepted
- **Date:** 2026-10-10
- **Related:** #140, ADR 0008

## Context
A request may name its process (`X-Kaleido-Process-Id`). When no process with that id existed,
the runtime started a new one under it, so a handing-off processor (Intake -> Radiology) could
continue the same process id on the target. The same rule applied to every caller: a wrong id, a
removed process, or a process lost when a store was reset silently became a new, empty process.
In a live MCP run the agent continued a request whose data had been reset, and carried on in an
empty request without the member it had captured, with no signal that anything was wrong.

## Decision
- **No process id:** a new process, with a generated id (unchanged).
- **A known process id:** the process continues (unchanged).
- **An unknown process id:** rejected with `pro_process_not_found` (HTTP 400), and nothing is
  started, **unless the request is a handoff**.
- **A handoff is recognised by the correlation Kaleido already carries:** the request is made from
  inside a step of **another** processor (`X-Kaleido-Calling-Processor`, stamped by the Kaleido
  clients on every call from a step scope, and different from the receiving service). No new API.

## Alternatives considered
- **An explicit handoff flag** on the execute request or a `HandOffAsync` client method. Clearer
  intent, but a new contract and a change to every handing-off handler, for the same information
  the calling-processor header already gives.
- **A guard in the MCP adapter** (check the process exists before continuing). Protects only MCP
  callers, costs a state read per step, and leaves the UI and other clients with the silent start.
- **Keep starting a process for any unknown id.** Rejected: it turns mistakes into empty processes.

## Consequences
- A client can no longer choose the id of a new process; it omits the id and uses the one returned.
  Handoffs keep the shared process id.
- The calling-processor header is a correlation header, not a credential: it decides only whether
  an unknown id may start a process. Authorization (the step's roles and policies, e.g.
  `InternalCaller` on a handoff step) is unchanged and still decides who may call.
- The MCP adapter needs no guard: the service's error reaches the agent (`pro_process_not_found`).