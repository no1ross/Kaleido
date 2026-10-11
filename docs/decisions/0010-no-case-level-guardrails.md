# 0010. Kaleido has no case-level guardrails

- **Status:** Accepted
- **Date:** 2026-10-10
- **Related:** #194, ADR 0001, ADR 0009

## Context
Kaleido enforced process ownership: when authorization was enforced, only the caller who created a
process, or a caller sharing one of the creator's roles (`OwnerRoles`), could read, resume or
transfer it, and `POST /{service}/processes/{processId}/transfer` let a role-mate claim it.

That blocks real handovers. A case started through an IVR is taken over by a rep on the phone (a
different user with different roles), or continued later by the provider in a portal: the person
who finishes a case is often not the person who started it. Who may work a given case is also a
business rule (assignment, team, relationship to the member), which ADR 0001 keeps out of Kaleido.

## Decision
- **No case-level guardrails.** Whether a user may open or continue a particular case is a
  business/data-level authorization concern of the underlying systems.
- **Kaleido's security role is limited to** knowing the caller's identity and checking the roles
  and policies declared on the step or query being called. A process may be continued by any caller
  the next step admits; a caller the step does not admit is still refused (401/403).
- **`Owner` stays as a tag** (created by), never enforced. `OwnerRoles` is removed.
- **The transfer endpoint is removed** (`ProcessTransferResponse`, its routes and tests): with no
  ownership there is nothing to transfer.

## Alternatives considered
- **Keep ownership, make it optional.** Rejected: it is a business rule in the framework, and the
  default would still block the common handover.
- **Keep role-mate access.** Rejected: role-mates is a crude stand-in for case assignment, and roles
  describe the user, not the case.

## Consequences
- Applications that need case-level access (assignment, team queues) enforce it in their own
  handlers or data access, where the business knows the rules. Kaleido tags who created a process.
- The SQLite reference store no longer has an `OwnerRoles` column; process stores created by
  earlier versions must be recreated (pre-release).
- Unknown process ids are covered separately: ADR 0009 rejects them unless the call is a handoff.