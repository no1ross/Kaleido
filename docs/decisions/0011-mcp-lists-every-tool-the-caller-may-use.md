# 0011. The MCP adapter lists every tool the caller may use

- **Status:** Accepted
- **Date:** 2026-10-12
- **Related:** #230, #140

## Context
The MCP adapter (`Kaleido.Http.Mcp`) lists every query and step tool the caller may use (filtered by
the declared roles/policies), plus the built-ins. #230 asked whether to list fewer tools up front -
only queries, start steps and built-ins - and let an agent reach later steps through `next`
("progressive disclosure"), to keep the tool list small if services grow to hundreds of tools.

A listed step tool does not mean the step can run: the process rules decide that at runtime. A step
that is not available in the process right now is refused (`pro_step_not_available`), and every step
result's `next` names the steps that are available. Hiding steps from the list would only change
what the agent sees, not what it may do, and would raise its own questions (how does a client call a
step it has not seen; `tools/list_changed` works only for session clients, not stateless ones).

## Decision
- **The adapter lists every tool the caller may use:** all queries (the agent needs them to find
  things before it starts anything), all steps, and the built-ins.
- **The process stays the gate:** a step runs only when the process makes it available; `next`
  tells the agent which steps those are.
- **No progressive disclosure, no opt-in setting.** Revisit when a real service's tool count makes
  the list a measurable problem for agents.

## Alternatives considered
- **List only queries, start steps and built-ins; surface the rest through `next`.** Rejected for
  now: it adds a second mechanism (schemas in step results, or `notifications/tools/list_changed`
  for session clients only) to solve a problem current services do not have.
- **An opt-in setting.** Rejected: an unused option is still code, docs and tests to maintain.

## Consequences
- Agents see one stable tool list per caller; nothing appears or disappears during a process.
- A large service's tool list grows with its steps. If that becomes a problem, a new record
  supersedes this one.
