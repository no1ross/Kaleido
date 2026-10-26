# 0029. Capabilities have no version: the name is the identity

- **Status:** Accepted
- **Date:** 2026-10-25
- **Supersedes in part:** ADR 0026 (the version label, its publication and its recording)
- **Related:** #474, #430, ADR 0026

## Context
ADR 0026 made the `Version` of a step, source or view a label: Kaleido published it in the registry,
recorded it in process state, carried it in events and telemetry, and required it on every
`[ProcessStep]`, `[QuerySource]` and `[QueryView]` - but never routed, branched or validated on it.
A breaking change already took a new name. So the label was required metadata on every capability,
a persisted column, an event field, a span and metric tag and a registry field, and nothing acted on
it. A changed registry already has a new ETag, which is how clients notice a deployment.

## Decision
1. **A capability has no version.** Its name (the type name) is its identity. `Version` is removed
   from the three attributes, the registrations and registries, the registry and wire contracts
   (step contract, step history, source and view responses), the process state (`StepContext`, the
   SQLite `ProcessStepContexts.Version` column), the events (`StepCompleted`, `PlanBuilt`
   candidates) and the telemetry (`kaleido.processor.step_version`).
2. **Kept from ADR 0026:** one live definition per name; a compatible change (wording, a new optional
   input, a new output field) keeps the name; a breaking change publishes a new capability (a new
   name, for example `CaptureMemberStepV2`) and keeps the old one while it is needed; in-flight
   processes continue on the deployed definition - completed steps stay in their history, new steps
   appear pending, a removed step stays in history and can no longer run.
3. **Not affected:** `ProcessorContext.Version` (the optimistic-concurrency counter), the MCP server
   version, the OpenTelemetry service version and the package version - none is a capability version.

## Alternatives considered
- **Keep the label (ADR 0026).** Every capability carries a required value nothing uses; it invites
  readers to believe Kaleido acts on it.
- **Make it optional.** Still published, persisted and tagged, with two shapes to support.
- **Act on it** (route or refuse by version). Rejected in ADR 0026: it needs coexisting versions,
  which a new name already provides.

## Consequences
- Breaking (preview): remove `Version = ...` from every `[ProcessStep]`, `[QuerySource]` and
  `[QueryView]`; KAL2001-KAL2003 require only `DisplayName` and `Description`. Clients and event
  consumers no longer receive a version field. Recreate SQLite development databases (the column is
  gone).
- What a step "was" when it ran is answered by the deployment (its commit) and the event's time, not
  by a hand-maintained label.
