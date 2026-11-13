# Architecture map

This is the entry point for Kaleido's architecture by **behavioral owner**. The repository is migrating under #585; these pages describe the areas already reached, not a speculative directory tree. The legacy [`ARCHITECTURE.md`](../../ARCHITECTURE.md) and [`src/ARCHITECTURE.md`](../../src/ARCHITECTURE.md) temporarily retain **only unmigrated** sections; move and remove those sections as their projects are processed, then retire both files. [Decisions](../decisions/README.md) record why a boundary was chosen; a later decision supersedes an earlier one rather than rewriting history.

- [Principles and terminology](principles.md) — cross-project design rules and stable vocabulary.
- [Dependencies](dependencies.md) — the processed subgraph, temporary edges and enforcement status.
- [Foundation](core.md) — bootstrap, shared identity/metadata, correlation and generic store selection.
- [Eventing](events.md) — outbox writer, relay, redaction, options and dispatch seams; placement remains temporary.
- [Authorization](authorization.md) — capability policy enforcement, discovery filtering and process-id threat model (Server/Registry migrations still pending).
- [Correlation](correlation.md) — request identity, inbound trust, per-hop caller tags and outbound propagation.
- [Process](capabilities/process.md), [authoring patterns](capabilities/process-authoring.md) and [information requests](capabilities/information-requests.md) — step runtime, state and Process event ownership.
- [Queryable](capabilities/queryable.md) and [authoring patterns](capabilities/queryable-authoring.md) — sources, views, query runtime and Queryable event ownership.
- [Cross-project implementation boundaries](implementation.md) — exceptions, async, DI, statics and test-enforced constraints.
- [HTTP transport patterns](transports/http.md) — shared wire, generic ASP.NET mechanics and local Queryable endpoints are owned; Process HTTP, clients and combined composition remain pending.
- Canonical **non-architecture** references stay in [coding conventions](../CONVENTIONS.md), the [error-code catalog](../ERROR_CODES.md) and [versioning/support policy](../VERSIONING.md). Their pages are not duplicated under architecture.

Other capability, registry, transport, observability, provider and composition pages should be added **when their projects are processed**. A project [README](../../src/Core/Kaleido/README.md) explains what the package is and how consumers use it; [AGENTS.md](../../AGENTS.md) states modification constraints. Neither should duplicate these architectural details.
