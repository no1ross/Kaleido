# 0001. Kaleido is a process engine, never a business decision maker

- **Status:** Accepted
- **Date:** 2026-10-08
- **Related:** #142, #194, #193

## Context
Kaleido sits between clients (portals, IVRs, AI agents) and existing systems. Each new
capability tempts the framework into deciding things for the domain: which questions to ask,
who may open a case, which path to take, what wording to use. Every such decision makes
Kaleido harder to adopt, because the right answer differs per business, per client and per
channel.

## Decision
**Kaleido does what it's told; it never makes business decisions.**
- It moves requests between clients and systems, applies **declared** rules (the step graph,
  the roles and policies declared on steps and queries, structural validation), and records
  what happened.
- Authorization is the service owner's. Kaleido only verifies what it was told to verify.
- What to ask, who may access a case, which path to take and what wording to use belong to the
  implementer's code and the underlying systems.

## Alternatives considered
- **Framework-level business features** (case ownership, built-in questionnaires, wording
  engines): these reinvent what every domain already has, and are wrong for most of them.
- **Pluggable policies inside Kaleido for those decisions:** they still move domain decisions
  into the framework's lifecycle and vocabulary.

## Consequences
- New capabilities are mechanisms (carry, record, check shape), never content.
- Case-level ownership enforcement is removed (#194); caller identity is tagged, not tracked
  (#193).
- Docs and reviews test new features against this principle.
