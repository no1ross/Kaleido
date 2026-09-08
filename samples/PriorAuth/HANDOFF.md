# Cross-Processor Handoff Pattern

This document explains how the PriorAuth sample implements server-to-server process handoffs — specifically the Intake → Radiology transition — and how the UI consumes the resulting signals.

---

## Overview

A **cross-processor handoff** occurs when one processor (e.g. Intake) determines mid-execution that the process must continue on a different processor (e.g. Radiology). Intake does not own Radiology's steps, state, or domain — it simply delegates and signals the UI which processor to talk to next.

The handoff is fully server-driven. The UI receives a clear signal (`targetProcessorName`) and reacts by fetching state from the target processor before navigating.

---

## Backend contract

### `StepExecutionResponse` (per-step endpoint)

```json
{
  "processId": "...",
  "stepName": "CaptureRequestedService",
  "outcome": "Completed",
  "result": null,
  "requiredStep": null,
  "targetProcessorName": "radiology",
  "availableSteps": [],
  "messages": []
}
```

When `targetProcessorName` is set:
- `requiredStep` is always `null` — Intake does not know Radiology's internal required step
- `availableSteps` reflects only Intake's local steps at this point (typically empty after handoff)
- The consumer **must** call the target processor's state endpoint to get authoritative next-step data

### `ProcessStateResponse` (state endpoint)

The same `targetProcessorName` field is present on `GET /{processor}/processes/{processId}` responses for symmetry, allowing a consumer that calls state directly to detect a pending handoff.

---

## How the handoff is implemented in Intake

`Intake.Artifacts/Process/Handlers/CaptureRequestedServiceHandler.cs`

```
1. Validate the procedure code against the code set service
2. Determine the modality (MRI, CT, ...) via the modality client
3. Look up the target processor name from configuration:
      ProcessorMappings:{modality} → e.g. "radiology"
4. Load the intake session (including the captured member)
5. Guard: if session.Member is null, return Failure(MemberNotCaptured)
6. Persist the resolved procedure + target processor to the intake session
7. Call the target processor's StartRadiologyIntake step via ExecuteStepAsync<StartRadiologyIntakeStep>:
      POST /radiology/processes/steps/startRadiologyIntake
      { memberId, memberEnrollmentId, dateOfService, codeValue, codeSystem }
      (same ProcessId, typed step — member + procedure data from the intake session)
8. If downstreamResult.Outcome == Failed → return Failure(downstreamResult.Messages)
9. Otherwise → return ProcessStepHandlerResult.HandOff(processorName)
```

Key points:
- The `ProcessId` is shared — Radiology receives and operates on the same process instance
- Intake submits a strongly typed `StartRadiologyIntakeStep` carrying member + procedure data resolved during the intake flow; it does not forward the original raw request payload
- `HandOff()` leaves `RequiredStep` null — Intake does not know Radiology's internal required step; the consumer must fetch it from Radiology's state endpoint
- Intake returns no typed result — it is a routing step, not a data-capture step

The target processor name comes from `appsettings.json`:

```json
"ProcessorMappings": {
  "Mri": "radiology",
  "Ct": "radiology"
}
```

This keeps the routing table out of code and allows new modality → processor mappings without a recompile.

---

## How Radiology receives the handoff

`Radiology.Artifacts/Process/Handlers/StartRadiologyIntakeHandler.cs`

Radiology's `StartRadiologyIntake` handler is the dedicated entry point for handoffs from Intake. In a single atomic step it:

1. Validates the member against the member service
2. Resolves the procedure code
3. Determines the modality
4. Upserts the `PriorAuthorization` + `PriorAuthorizationMember` rows
5. Adds the `PriorAuthorizationRequestedService` row
6. Upserts a history record
7. Returns a typed `StartRadiologyIntakeResponse` with:
   - A `questionnaire` definition for the appropriate capture step
   - `requiredStep: "CaptureMriInfo"` (or `"ConfirmCtInsteadOfMri"` for CT)

This is equivalent to Radiology's own `CaptureMember` + `CaptureRequestedService` sequence, collapsed into one step for the handoff path so Intake only needs to make one downstream call.

---

## UI consumption (priorauth-ui)

### `ProcessState.currentProcessorName`

The UI tracks which processor it is currently talking to in `ProcessStateService`:

```typescript
interface ProcessState {
    processId?: string;
    currentProcessorName?: string;  // derived from registry on load; updated on handoff
    requiredStep?: string;
    availableSteps: ProcessStepSummary[];
    ...
}
```

`currentProcessorName` is set automatically on `populateRegistry()` from whichever processor advertises `initialSteps`. No hardcoded processor name strings appear at call sites.

### `ProcessRegistry` — compound keying

The registry is keyed internally by `processorName:stepName` to prevent collisions when different processors have steps with the same name. Public lookups use `(processorName, stepName)` — the processor comes from `ProcessState`, not from call sites.

### `ProcessService.executeStep()`

```typescript
executeStep<TStep, TResponse>(stepName: string, request: ...): Observable<...>
```

1. Reads `currentProcessorName` from `ProcessState`
2. Looks up the step in the registry by `(processorName, stepName)`
3. POSTs to the step's `executeUrl`
4. On response:
   - **If `targetProcessorName` is set** → cross-processor handoff path:
     - Calls `GET /{targetProcessorName}/processes/{processId}` on the target processor
     - Updates `ProcessState` with the target's `requiredStep`, `availableSteps`, and **switches `currentProcessorName`** to the target
     - Navigates to the required step route
   - **Otherwise** → local step path:
     - Updates `ProcessState` from the response directly
     - Navigates to the required step route

After a handoff, all subsequent `executeStep()` calls automatically go to the new processor — no changes required at call sites.

### Cross-processor state fetch

The service resolves the target processor's base URL by looking up any registered entry for that processor name in the registry:

```typescript
// ProcessRegistry.getAnyEntryForProcessor(targetProcessorName)
// → finds the service config (baseUrl, key) for that processor
// → builds: /{service.key}/processes/{processId}
```

This means the target processor must be in the UI's service registry (configured in `serviceRoutes.ts`) and must have at least one step registered.

---

## Step-by-step walkthrough: Intake → Radiology MRI

```
User submits CaptureRequestedService (code: MRI procedure)
    │
    ▼
POST /intake/processes/steps/captureRequestedService
    │
    ▼
Intake: CaptureRequestedServiceHandler
    ├── resolves procedure code → MRI modality
    ├── looks up ProcessorMappings:Mri → "radiology"
    ├── loads intake session (member + procedure)
    ├── persists procedure + target to intake session
    ├── calls StartRadiologyIntake on Radiology:
    │       POST /radiology/processes/steps/startRadiologyIntake
    │       { memberId, memberEnrollmentId, dateOfService, codeValue, codeSystem }
    │           │
    │           ▼
    │       Radiology: StartRadiologyIntakeHandler
    │           ├── validates member
    │           ├── resolves + validates procedure code
    │           ├── upserts PriorAuthorization + Member rows
    │           ├── adds PriorAuthorizationRequestedService row
    │           ├── upserts history record
    │           └── returns requiredStep: "CaptureMriInfo"
    │                       + questionnaire definition
    │
    └── returns to caller:
            targetProcessorName: "radiology"
            requiredStep: null
            result: null
    │
    ▼
UI: ProcessService.executeStep() receives response
    ├── sees targetProcessorName: "radiology"
    ├── calls GET /radiology/processes/{processId}
    │       → returns requiredStep: "CaptureMriInfo"
    │                  availableSteps: [...]
    │                  per-step results: { StartRadiologyIntake: { questionnaire: ... } }
    │
    ├── updates ProcessState:
    │       currentProcessorName: "radiology"
    │       requiredStep: "CaptureMriInfo"
    │       questionnaire: <from result>
    │
    └── navigates to /process/{processId}/capture-mri-info

User completes CaptureMriInfo form
    │
    ▼
POST /radiology/processes/steps/capturemriinfo
    (currentProcessorName is already "radiology" — no special handling needed)
```

---

## Adding a new processor handoff

To add a new modality → processor mapping (e.g. Oncology):

1. Register the Oncology processor and its steps in its own `*.Artifacts` project
2. Create a `StartOncologyIntakeStep` and `StartOncologyIntakeHandler` in `Oncology.Artifacts` (same pattern as `StartRadiologyIntakeStep`)
3. Add `"ProcessorMappings:Oncology": "oncology"` to Intake's `appsettings.json`
4. Add `AddProcessClient(o => { o.Name = "oncology"; ... })` in Intake's `Program.cs`
5. Add `Intake.Artifacts` → `Oncology.Artifacts` project reference so `CaptureRequestedServiceHandler` can submit the typed step
6. Update `CaptureRequestedServiceHandler` to call `ExecuteStepAsync<StartOncologyIntakeStep>` when `processorName == "oncology"`
7. The Intake → target handoff signal (`HandOff(processorName)`) and `ProcessService` require no changes

---

## Files involved

| File | Role |
|------|------|
| `Intake.Artifacts/Process/Handlers/CaptureRequestedServiceHandler.cs` | Detects modality, calls `StartRadiologyIntake` on target, signals `HandOff(processorName)` |
| `Intake/appsettings.json` | `ProcessorMappings` configuration |
| `Radiology.Artifacts/Process/Steps/StartRadiologyIntakeStep.cs` | Entry-point step for handoff from Intake |
| `Radiology.Artifacts/Process/Models/StartRadiologyIntakeResponse.cs` | Response carrying questionnaire data back to Intake |
| `Radiology.Artifacts/Process/Handlers/StartRadiologyIntakeHandler.cs` | Validates member + procedure, upserts rows, returns `requiredStep` |
| `src/Process/Abstractions/Execution/ProcessStepResult.cs` | `ProcessStepHandlerResult.HandOff(targetProcessorName)` factory |
| `src/Process/Abstractions/Execution/ExecutionDecision.cs` | `ExecutionDecision.HandOff(targetProcessorName)` factory |
| `src/Process/Process/Execution/StepExecutionEvaluator.cs` | Checks `TargetProcessorName` before `RequiredStep` so pure handoffs are not dropped |
| `src/Process/AspNetCore.Abstractions/Contracts/ProcessExecutionResponse.cs` | `TargetProcessorName` on HTTP step response |
| `src/Process/AspNetCore.Abstractions/Contracts/ProcessStateResponse.cs` | `TargetProcessorName` on HTTP state response |
| `src/Registry/RegistryEndpointRouteBuilderExtensions.cs` | `MapRegistry()` — unified discovery endpoint |
| `priorauth-ui/src/app/kaleido/services/process-registry.ts` | Compound keying; `getAnyEntryForProcessor()` |
| `priorauth-ui/src/app/kaleido/services/process-service.ts` | Cross-processor state fetch; `currentProcessorName` switching |
| `priorauth-ui/src/app/process/services/process-state-service.ts` | `currentProcessorName` in `ProcessState`; `setProcessFlow()` |
| `priorauth-ui/src/app/kaleido/models/process-state-response.ts` | UI model for the target processor's state response |
| `priorauth-ui/src/configuration/serviceRoutes.ts` | Service registry entries; intake uses unified `registryPath` |
| `priorauth-ui/src/app/registries/registry-catalog.ts` | `loadUnifiedRegistry()` for `registryPath`-bearing services |
| `priorauth-ui/src/app/process/services/step-route.ts` | Step name → Angular route mapping |
