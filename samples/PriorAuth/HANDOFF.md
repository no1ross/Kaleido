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
4. Persist the resolved procedure + target processor to the intake session
5. Forward the original request payload to the target processor:
      POST /radiology/processes/execute  (same ProcessId, same Steps)
6. Return ProcessStepHandlerResult.Success(
       requiredStep: downstreamResult.RequiredStep,
       targetProcessorName: downstreamResult.TargetProcessorName ?? processorName)
```

Key points:
- The `ProcessId` is shared — Radiology receives and operates on the same process instance
- The original `Steps` from the caller's request are forwarded verbatim via `context.OriginalRequest`
- Intake returns the downstream processor's `RequiredStep` (which may itself be null if Radiology handed off further) and always sets `targetProcessorName`
- Intake itself returns no typed result — it is a routing step, not a data-capture step

The target processor name comes from `appsettings.json`:

```json
"ProcessorMappings": {
  "Mri": "radiology",
  "Ct": "radiology"
}
```

This keeps the routing table out of code and allows new modality → processor mappings without a recompile.

---

## How Radiology receives the forwarded request

`Radiology.Artifacts/Process/Handlers/CaptureRequestedServiceHandler.cs`

Radiology's `CaptureRequestedService` handler receives the same step payload Intake received. It:

1. Validates and resolves the procedure code
2. Determines the modality
3. Checks for duplicate or conflicting requested services
4. Persists the requested service to the Radiology database
5. Returns a typed `CaptureRequestedServiceResponse` with:
   - A `questionnaire` definition for the appropriate capture step
   - `requiredStep: "CaptureMriInfo"` (or `"ConfirmCtInsteadOfMri"` for CT)

Radiology's response flows back to Intake's handler as `downstreamResult`, and Intake surfaces it back to the original caller via `targetProcessorName`.

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
    ├── persists procedure + target to intake session
    ├── forwards request to:
    │       POST /radiology/processes/steps/captureRequestedService
    │           │
    │           ▼
    │       Radiology: CaptureRequestedServiceHandler
    │           ├── validates + resolves procedure code
    │           ├── persists requested service
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
    │                  per-step results: { CaptureRequestedService: { questionnaire: ... } }
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
2. Add `"ProcessorMappings:Oncology": "oncology"` to Intake's `appsettings.json`
3. Add the Oncology service route to `serviceRoutes.ts` in the UI with a `processRegistryPath`
4. Add route entries for Oncology's steps to `step-route.ts` in the UI
5. The handoff mechanism in `CaptureRequestedServiceHandler` and `ProcessService` requires no changes

---

## Files involved

| File | Role |
|------|------|
| `Intake.Artifacts/Process/Handlers/CaptureRequestedServiceHandler.cs` | Detects modality, forwards to target, signals `targetProcessorName` |
| `Intake/appsettings.json` | `ProcessorMappings` configuration |
| `Radiology.Artifacts/Process/Handlers/CaptureRequestedServiceHandler.cs` | Receives forwarded request, returns `requiredStep` |
| `src/Process/AspNetCore.Abstractions/Contracts/ProcessExecutionResponse.cs` | `TargetProcessorName` on HTTP step response |
| `src/Process/AspNetCore.Abstractions/Contracts/ProcessStateResponse.cs` | `TargetProcessorName` on HTTP state response |
| `priorauth-ui/src/app/kaleido/services/process-registry.ts` | Compound keying; `getAnyEntryForProcessor()` |
| `priorauth-ui/src/app/kaleido/services/process-service.ts` | Cross-processor state fetch; `currentProcessorName` switching |
| `priorauth-ui/src/app/process/services/process-state-service.ts` | `currentProcessorName` in `ProcessState`; `setProcessFlow()` |
| `priorauth-ui/src/app/kaleido/models/process-state-response.ts` | UI model for the target processor's state response |
| `priorauth-ui/src/configuration/serviceRoutes.ts` | Service registry entries per processor |
| `priorauth-ui/src/app/process/services/step-route.ts` | Step name → Angular route mapping |
