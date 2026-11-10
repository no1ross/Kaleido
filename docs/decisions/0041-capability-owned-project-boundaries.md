# 0041. Projects follow capability ownership

- **Status:** Accepted
- **Date:** 2026-11-09
- **Related:** #585, #586, #588, #557; supersedes the package placement in ADR 0039

## Context

ADR 0039 separated a shared Platform package from transports, but `Kaleido` still combined Process, Queryable and Eventing. Platform accumulated Registry, capability and HTTP contracts, while client and server projects also own pieces of Registry behavior. The assembly layout obscured behavioral ownership and made a lower-level package register unrelated capabilities. The existing code and tests remain the source of truth for behavior; old placement is not a constraint.

## Decision

A project owns the behavior it performs, not every capability it uses. `Kaleido` is a dependency-free foundation. Process, Queryable and Registry are distinct top-level capabilities; Eventing is a separate layer above foundation, and capability runtimes may depend on it. Neutral Client/Server and HTTP/gRPC packages provide transport mechanics; capability-specific integrations own capability adaptation. Provider implementations and composed packages do not acquire runtime behavior. Preserve one-way project references and avoid cycles.

The migration proceeds by coherent project/test units from the foundation outward. Start by shallowly extracting Queryable, Process and Eventing, then deeply review the remaining foundation and each capability in dependency-ready order. During the transition, unmigrated dependents may not build; the active project and its tests must pass. Source file and namespace moves are not an excuse to change runtime or wire behavior without an explicit decision. Every project has a focused README, mirrored tests and owner-aligned Add* registration. Project-wide imports live in the project file as MSBuild `Using` items.

The first extraction moves Queryable to `Kaleido.Queryable` with explicit `AddQueryable()`; Process and Eventing remain in `Kaleido` temporarily, so the present package graph is transitional rather than the final model. A relay-only host explicitly registers the Process and Queryable event dispatch it needs, without registering either runtime. gRPC adapters remain non-packable placeholders until implemented; a placeholder must not pretend to register a working transport.

## Alternatives considered

- Preserve the Platform-centric layout and relocate only files inside existing assemblies. Rejected: project references and registration would still express the wrong ownership.
- Put all capability metadata and wire shapes in foundation. Rejected: the foundation would own Registry and transport behavior it does not perform.
- Make a composed package implement the runtimes to avoid moving their code. Rejected: composition packages only assemble lower-level registration.

## Consequences

Moving public types changes package dependencies even when names and serialized shapes stay the same. Move PublicAPI baselines, tests, documentation, sample registrations, agent guidance, CI packaging globs and package-consumer manifests with each owner. Retain stable error-code values, event formats, and existing provider semantics unless separately decided. The proposed final project topology is tracked in #585 and may be revised only after identifying and documenting an actual ownership or dependency conflict. ADR 0040's central-registry and versioned-route decisions are unaffected.
