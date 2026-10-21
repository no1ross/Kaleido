# 0026. Versions are labels; one live version per capability; processes continue on the deployed definition

- **Status:** Accepted
- **Date:** 2026-10-21
- **Related:** #197, #195, #220, ADR 0004, ADR 0005

## Context
`Version` is required on `[ProcessStep]`, `[QuerySource]` and `[QueryView]`. It is published in the
registry, recorded in process state and carried in events (`StepVersion`). But what a version *means*
had never been decided:
- what a version change means for in-flight processes, clients and downstream systems;
- whether two versions of one capability can coexist, and how they are addressed;
- what happens to a process started on step 1.0 when 2.0 ships.

One defect followed from the gap: reconciling a stored process with the deployed registry overwrote
every step's recorded version, so after a deployment a process claimed its completed steps ran at 2.0.

## Decision
1. **A version is a label.** It names the published contract of a step, source or view (semver is
   suggested), for audit and for clients. Kaleido records and publishes it, but never routes, branches
   or validates on it.
2. **One live version per capability.** A capability is addressed by its name only: routes, step names
   in state and events, MCP tool names and the registry stay version-free. Deploying a new version
   replaces the old one. To keep an incompatible shape alive, publish it under another name (for
   example a new step type `CaptureMemberStepV2`, or another service), and retire the old one when
   nothing needs it.
3. **Compatible changes keep the name; breaking changes take a new one:**

   | Change | Kind |
   |---|---|
   | Wording (display name, description, prompt); a new optional input; a new output field; a version bump | Compatible: same name, new version |
   | Removing or renaming an input or output; making an input required; changing a type or a value's meaning; changing relationships so stored processes would be stranded | Breaking: a new capability (name), the old one kept while it is needed |

4. **In-flight processes continue on the deployed definition.** No migration engine and no pinning:
   - **History:** a step that ran keeps the version it ran with; each attempt records the version that ran.
   - **Not run yet:** a step that has not run follows the deployed definition (and version).
   - **New steps** appear, pending. Inputs are validated, and relationships applied, by the deployed
     definition.
   - **A removed step** stays in the process's history and can no longer run. Keep a step registered
     while stored processes can still require it, for example by closing it with `AvailableUntil`
     before removing it in a later deployment.
5. **Clients detect changes through the registry.** Versions are published there, and a changed
   registry has a new ETag (clients already revalidate with it). There is no per-call version check;
   an "expected version" request header could be added later without breaking anything.

### Effects
| Area | Effect |
|---|---|
| Persisted state | `StepContext.Version`: the version the step last ran with; for a step that has not run, the deployed one (the reconciliation defect is fixed) |
| Events | Unchanged: `StepCompleted.StepVersion` and `PlanBuilt` candidates carry the version that ran |
| Registry | Unchanged: each capability publishes its one live version |
| Clients | Unchanged: they address capabilities by name, and the registry ETag tells them it changed |
| MCP adapter | Unchanged: tool names are version-free |

## Alternatives considered
- **Addressable coexisting versions** (`v1` and `v2` both registered: a route, header or registry
  entry per version). This changes routes, the registry, clients, state and MCP tool names. The real
  need, keeping an old shape alive, is served by a new name. Rejected.
- **Pin a process to the versions it started with.** It requires coexisting versions (rejected
  above), and old definitions would have to run long after they are replaced. Rejected.
- **Refuse steps whose version changed since the process started** (`pro_step_version_changed`). It
  turns every compatible change into a migration for every host. Rejected; a host that needs it can
  compare versions in its own handler.
- **Version as a compatibility contract enforced by Kaleido.** Semver checks on free-form labels add
  rules without solving addressing. Rejected for now; option 5 leaves room for an opt-in check.

## Consequences
- **Breaking changes cost a new name**, which keeps every route, tool name and stored process
  unambiguous.
- **Removing a step needs care:** retire it in two deployments (close it first, remove it once no
  process can require it).
- **A future remote registry** (#220, deferred) can key its wording overrides by capability name and
  version.
- **Tests:** `ProcessorStateUpdaterTests` covers a process spanning a deployment: its history is kept,
  the next step is recorded at the new version, a new step appears and a removed one stays in history.
