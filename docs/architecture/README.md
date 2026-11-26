# Architecture map

This is the canonical entry point for Kaleido's architecture by **behavioral owner**. The repository is migrating under #585; these pages describe the areas already reached, not a speculative directory tree. [Decisions](../decisions/README.md) record why a boundary was chosen; a later decision supersedes an earlier one rather than rewriting history.

- [Principles and terminology](principles.md) — cross-project design rules and stable vocabulary.
- [Dependencies](dependencies.md) — the processed subgraph, temporary edges and enforcement status.
- [Foundation](core.md) — bootstrap, shared identity/metadata, correlation and generic store selection.
- [Eventing](events.md) — outbox writer, relay, redaction, options and dispatch seams; placement remains temporary.
- [Authorization](authorization.md) — declared capability rules, caller-filtered Registry discovery and the process-id threat model.
- [Correlation](correlation.md) — request identity, inbound trust, per-hop caller tags and outbound propagation.
- [Process](capabilities/process.md), [authoring patterns](capabilities/process-authoring.md) and [information requests](capabilities/information-requests.md) — step runtime, state and Process event ownership.
- [Queryable](capabilities/queryable.md) and [authoring patterns](capabilities/queryable-authoring.md) — sources, views, query runtime and Queryable event ownership.
- [Registry contracts](../../src/Capabilities/Registry/Kaleido.Registry/README.md), [authority](../../src/Capabilities/Registry/Kaleido.Registry.Server/README.md) and [participant](../../src/Capabilities/Registry/Kaleido.Registry.Client/README.md) — portable capability publication, service/build identity, central snapshot versions and explicit hydration ([ADR 0045](../decisions/0045-registry-publication-build-identity-and-snapshot-versions.md)).
- [Cross-project implementation boundaries](implementation.md) — exceptions, async, DI, statics and test-enforced constraints.
- [HTTP transport patterns](transports/http.md) — shared wire, generic ASP.NET mechanics, Process/Queryable HTTP binding data and the shared outbound pipeline are owned; Process/Queryable ASP.NET execution, Registry-published capability clients and explicit Hosting.Http endpoint composition are now owner-scoped; the MCP adapter now consumes those clients with caller-filtered discovery and service identity ([ADR 0051](../decisions/0051-mcp-uses-service-identity.md)); other parked legacy consumers remain pending.
- Canonical **non-architecture** references stay in [coding conventions](../CONVENTIONS.md), the [error-code catalog](../ERROR_CODES.md) and [versioning/support policy](../VERSIONING.md). Their pages are not duplicated under architecture.

Other capability, transport, observability, provider and composition pages should be added **when their projects are processed**. A project [README](../../src/Core/Kaleido/README.md) explains what the package is and how consumers use it; [AGENTS.md](../../AGENTS.md) states modification constraints. Neither should duplicate these architectural details.
