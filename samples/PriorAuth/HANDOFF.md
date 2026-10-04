# Cross-Processor Handoff Pattern

This document explains how the PriorAuth sample implements server-to-server process handoffs — specifically the Intake → Radiology transition — and how the UI consumes the resulting signals.

---

## Overview

In **this PriorAuth sample**, consumers begin a prior-authorization request with Intake. After the consumer supplies a procedure/service code, Intake uses Configuration's product-code mapping to select the dedicated product processor that owns the remaining workflow. Radiology is the first implemented target; Oncology, Sleep, Rehab, and other products can each have their own processors with different steps and domain rules. Intake does not own those product-specific steps or state.

This is an application design choice, **not a required Kaleido architecture**. Kaleido supplies processors, steps, correlation, and a handoff signal; the sample decides how to select a target. Only the Intake → Radiology path is implemented today. A new product needs its own processor and entry step plus a corresponding Intake dispatch path; adding mapping data alone cannot make the current Radiology-specific call work for another processor.

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
  "businessMessages": [],
  "frameworkMessages": []
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

`Common/Intake/Process/Handlers/CaptureRequestedServiceHandler.cs`

```
1. Resolve and validate the submitted procedure code through CodeSet.
2. Query Configuration's product-code-mappings context for its processor name
   (CodeSystem + CodeValue); the seeded mappings currently point to radiology.
3. Return a business failure if the code or processor mapping cannot be found.
4. Load or create the Intake session, preserving any member data already captured.
5. Persist the procedure and resolved processor name; update History.
6. For the currently supported Radiology target, call its typed
   StartRadiologyIntake step with available member + procedure data.
7. If the downstream step fails, return its business failure messages; otherwise return
   ProcessStepHandlerResult.HandOff(processorName).
```

The mapping lives in Configuration data (seeded from `Seeder/assets/configuration/product-code-mappings.json`), **not** in an Intake `ProcessorMappings` setting. Modality (MRI/CT) is resolved later within Radiology; it does not choose the product processor in Intake.

Key points:
- The contract intends to carry the same `ProcessId` through Intake and the target processor. Actual propagation is under investigation (HP-025); do not treat a successful handoff response alone as proof the target has persisted state.
- Intake submits a strongly typed `StartRadiologyIntakeStep` with whatever member data is available plus the validated procedure; it does not forward the original raw request payload.
- `HandOff()` leaves `RequiredStep` null — the consumer must fetch the target processor's state for its authoritative next step.
- The current call is statically typed to `StartRadiologyIntakeStep`. Future products require their own entry step and Intake dispatch logic even if a mapping resolves their processor name.

---

## How Radiology receives the handoff

`Common/Radiology/Process/Handlers/StartRadiologyIntakeHandler.cs`

Radiology's `StartRadiologyIntake` handler is the dedicated entry point for handoffs from Intake. During this step it:

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

### `ProcessorRegistry` — compound keying

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
// ProcessorRegistry.getAnyEntryForProcessor(targetProcessorName)
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
    ├── validates procedure code through CodeSet
    ├── looks up Configuration product-code-mappings → "radiology"
    ├── loads or creates intake session (member may not yet be captured)
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

## Adding another product processor

For a product such as Oncology, Sleep, or Rehab (not implemented in this sample yet):

1. Implement its own processor, domain steps, durable process state, and a typed entry step/handler for the Intake handoff.
2. Add the product's procedure/service codes and processor name to Configuration's `ProductCodeMappings` data. The code mapping chooses the product processor, not an MRI/CT modality.
3. Configure Intake's Kaleido HTTP client for that processor and make its entry-step contract available to Intake.
4. Extend `CaptureRequestedServiceHandler`'s typed dispatch. It currently calls `ExecuteStepAsync<StartRadiologyIntakeStep>` for **every** resolved processor name; configuration alone cannot make that call work for a different product.
5. Register the new processor with the router and consumer service routes, and verify the target's state can be fetched using the handed-off `ProcessId` (HP-025 remains open for the current Radiology path).

The framework handoff signal, `HandOff(processorName)`, does not require a product-specific change.

---

## Files involved

`Common`, `Seeder`, and `priorauth-ui` paths below are relative to `samples/PriorAuth/`; `src` paths are relative to the repository root.

| File | Role |
|------|------|
| `Common/Intake/Process/Handlers/CaptureRequestedServiceHandler.cs` | Resolves the target from the procedure code, calls the currently supported Radiology entry step, signals `HandOff(processorName)` |
| `Common/Intake/Process/Services/ProductCodeMappingClient.cs` | Queries Configuration for a code's target processor |
| `Seeder/assets/configuration/product-code-mappings.json` | Sample procedure-code-to-processor mapping data (currently Radiology) |
| `Common/Radiology/Process/Steps/StartRadiologyIntakeStep.cs` | Typed entry step for the Radiology product processor |
| `Common/Radiology/Process/Handlers/StartRadiologyIntakeHandler.cs` | Receives Intake's validated member/procedure data |
| `src/Kaleido/Processor/Execution/ProcessStepHandler.cs` | `ProcessStepHandlerResult.HandOff(targetProcessorName)` factory |
| `src/Kaleido.Http.Abstractions/Processor/ProcessContracts.cs` | HTTP execution and state response contracts |
| `priorauth-ui/src/app/kaleido/services/process-service.ts` | Fetches target processor state and switches the active processor after handoff |
| `priorauth-ui/src/configuration/serviceRoutes.ts` | Consumer service routes for target processors |
