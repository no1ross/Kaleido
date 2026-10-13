# 0015. Step behavior is declared by the implementer; Kaleido publishes it and derives the rest

- **Status:** Accepted
- **Date:** 2026-10-13
- **Related:** #137, ADR 0001, ADR 0013

## Context
Step and view metadata described inputs and outputs, not what calling a step does. The MCP adapter
marked every step only "not read-only", and by the MCP spec that means "destructive, not idempotent"
for all of them - recording member data looked the same to an agent as withdrawing a request. #137
proposed a large annotation bag (read-only, side effects, destructive, idempotent, confirmation,
classification, timeout and retry guidance).

Most of it already exists or can be derived: queries are read-only; retries are safe (every
adapter call carries an `Idempotency-Key`, and the clients' resilience handles timeouts); a step
without `[Repeatable]` is refused once it ran, so it is idempotent in the MCP sense; field
classifications exist since ADR 0013. What Kaleido cannot know is what a handler does in the world -
that is business knowledge (ADR 0001).

## Decision
- **Declared on `[ProcessStep]`**, all false by default (an ordinary step that records into the
  process):
  - `Destructive` - irreversible or loss-causing (cancel, delete, withdraw, submit, pay);
  - `External` - changes something outside the process state (another system's data, a message, an
    order, a payment), wherever that system lives; reading other systems and handing off to another
    Kaleido processor do not count;
  - `RequiresConfirmation` - the user should confirm before a client calls the step. **A hint**:
    published and stated in the MCP description, not enforced at call time.
- **Derived:** idempotence from `[Repeatable]`.
- **Published** in the registry and step summaries (`destructive`, `external`,
  `requiresConfirmation`), and field `classifications` (e.g. `"PriorAuth:PHI"`) on step inputs and
  outputs, query fields, view outputs and view parameters.
- **The MCP adapter maps** them onto `destructiveHint`, `openWorldHint`, `idempotentHint` and the
  tool description, and marks classified fields as sensitive.

## Alternatives considered
- **The full annotation bag.** Rejected: most entries duplicate what exists (idempotency keys,
  resilience) or what Kaleido derives; the rest invite guesses.
- **One `StepEffect` enum.** Rejected: a step can be destructive and external at once (charging a
  card).
- **Enforced confirmation** (the adapter asks the user via elicitation before executing). Rejected
  for now: a hint keeps the adapter simple; enforcement can follow if agents ignore it.
- **Conservative default** (undeclared = destructive, open world). Rejected: agents would treat
  every step as dangerous until annotated.

## Consequences
- Implementers mark the few dangerous steps; everything else stays as it is.
- The declarations are not verified - a wrong one misleads agents (documented).
- Additive contract change: older clients ignore the new fields.
