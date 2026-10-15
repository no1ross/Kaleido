# 0016. The MCP adapter can list a front door; open steps arrive in `next`

- **Status:** Accepted
- **Date:** 2026-10-15
- **Related:** #338, supersedes [0011](./0011-mcp-lists-every-tool-the-caller-may-use.md)

## Context
[0011](./0011-mcp-lists-every-tool-the-caller-may-use.md) decided that the MCP adapter lists every
tool the caller may use, to be revisited "when a real service's tool count makes the list a
measurable problem for agents". The PriorAuth sample now shows it:

- Today: 25 tools, about 46 KB of tool definitions (queries 23 KB, steps 21 KB) sent to the model on
  every turn.
- Projected: 11 products of about 36 steps each, plus more than 100 user-facing queries, is about
  600 tools and 1.1 MB. That is past the tool caps of common clients, and well before the caps,
  models choose tools worse from hundreds of definitions. Trimming descriptions does not change the
  order of magnitude.
- In an end-to-end session an agent missed a nested `parameters.processId` it never had in view;
  the list had been cut short by the client.

`tools/list` is requested per connection, not per turn, and the adapter is stateless (no session
affinity, MRTR). A tool list that changes as a process moves on is therefore not available:
`notifications/tools/list_changed` reaches only session clients and would need the adapter to keep
per-conversation state.

The structure of the processes helps: every product is entered through a handoff, and every step
result already carries `next`, the steps the process makes available (the process engine's own
state under its declared rules, [0001](./0001-process-engine-not-decision-maker.md)). An agent
never needs a non-initial step before `next` names it.

## Decision
- **A setting, `Kaleido:Mcp:ToolListing`:** `All` (default; 0011's behavior) or `FrontDoor`.
- **The front door is derived from what the registry already publishes:** the steps that can
  start a process, the queries those steps declare with `[Lookup]`, the names in
  `Kaleido:Mcp:ListedTools` (for searches no starting step declares), and the built-ins. Nothing
  else is listed, so the list stays flat as steps and queries are added.
- **Every tool is still built and callable**, with the same authorization and process rules.
  Three built-ins reach the unlisted ones: `search_tools` (find a tool by what it does),
  `describe_tool` (one tool's full inputs) and `call_tool` (run any of the caller's tools by name;
  the handler unwraps it, so it runs exactly as the tool itself).
- **Open steps are described where the agent looks:** in FrontDoor mode each unlisted step in
  `next` carries its inputs, approval hints, and its lookups' search with the processId already in
  the arguments. The agent moves on in one call per step and never has to find the search itself.

## Alternatives considered
- **Keep listing everything (0011).** Rejected at this scale: the list exceeds what clients and
  models can hold.
- **A per-turn tool list (`tools/list_changed`).** Rejected: session clients only, and it needs
  per-conversation state in an adapter that is deliberately stateless.
- **Only names in `next`, details through `describe_tool`.** Rejected for open steps: an extra call
  before every step, and every extra call is another place for an agent to skip and guess. Kept for
  tools found by search.
- **One MCP endpoint per domain.** Not enough on its own: clients merge every connected server's
  tools, and an end-to-end agent needs every product it may be handed to; handoffs, `next` and
  lookups cross servers. It remains useful for narrow agents, on top of either mode.
- **FrontDoor for every host.** Rejected: small hosts would lose typed tool definitions and
  per-tool approval prompts for no gain. This answers 0011's objection to an opt-in setting: the
  setting exists for the scale problem it solves.
- **An `EntryPoint` flag on capabilities.** Rejected for now: "what an agent needs first" is a
  property of an agent's use case, not of the capability, and the derived menu plus
  `ListedTools` covers it without a contract change.

## Consequences
- The tool list of a FrontDoor host stays small whatever the number of products and queries.
- Unlisted tools run through `call_tool`, so a client's per-tool approval applies to `call_tool`
  as a whole. It is annotated not read-only and destructive so clients keep asking, `next` carries
  each step's hints, and the instructions tell the agent to confirm before running a step. This is
  the main cost, and the reason the mode is opt-in.
- Unlisted steps lose client-side schema checks; the adapter checks required arguments against
  the tool's own schema before calling the service.
- A domain that forgets `[Lookup]` on an id input gets no search in `next`; the agent falls back to
  `search_tools`, and the adapter's unresolved-lookup report helps owners find it.
