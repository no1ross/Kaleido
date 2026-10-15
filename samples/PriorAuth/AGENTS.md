## Local tooling
- Use `podman`, not `docker`
- When validating compose/container workflows, run `podman compose ...`
- Do not suggest `docker` commands unless explicitly asked
- npx @microsoft/aspire-cli dashboard run - to run Aspire for OTel

## PriorAuth layout
- `samples/PriorAuth/Compose/` contains local orchestration files
- `samples/PriorAuth/priorauth-ui/` is the Angular frontend
- `samples/PriorAuth/*/` folders are ASP.NET Core services targeting .NET 8
- `samples/PriorAuth/*.Artifacts/` projects contain sample contracts/artifacts

## Verification
- For backend changes, prefer `dotnet build` on the affected project
- Run `dotnet test` for any affected backend test projects
- For UI changes, run from `samples/PriorAuth/priorauth-ui`:
  - `npm test`
  - `npm run build`
- If a change touches UI + backend integration, validate both

## Local run workflow
- Prefer compose files under `samples/PriorAuth/Compose/`
- Use the smaller compose file for backend-only work
- Use the full compose file when testing the full sample including router/UI/intake
- If only the UI is changing, prefer running the Angular dev server directly
- The PriorAuth seeder is now a local-only workflow; it is no longer run as a compose service
- Seed shared databases locally before starting compose-backed services:
  - `dotnet run --project samples/PriorAuth/Seeder/Kaleido.Samples.PriorAuth.Seeder.csproj -- --domains=ReferenceData,CodeSet,Configuration,ProviderSearch,MemberService`
- Shared SQLite files live under `samples/PriorAuth/data/`
- Compose mounts that host directory into service containers as `/app/data`
- Runtime-created DBs like `eventcollector.db`, `intake.db`, `intake-process.db`, `radiology.db`, and `radiology-process.db` may appear in `samples/PriorAuth/data/` after services start
- The `*-process.db` files are the Kaleido context stores (process state, idempotent results, event outbox). They are created with `EnsureCreated`, which never adds tables to an existing file: after a store schema change (e.g. the `OutboxEvents` table) delete them; they are recreated at startup
- The services' own databases (`intake.db`, `radiology.db`, `history.db`) are created the same way: after an entity change, delete the affected file (and its `*-process.db`, so no process points at missing data)

## History records
- History is the source of truth for finding requests: one record per process (keyed by `ProcessId`) holding the latest snapshot. `PriorAuthorizationId` is set only by the product processor (Radiology); it is empty while the request is in Intake.
- Every Intake/Radiology step that changes what History shows calls its `HistoryClient.RecordAsync(processId)`, which sends the full current snapshot read from the service's own database. The recording processor is the calling processor Kaleido stamps on the call, not a payload field.
- Recording is a hard stop: the step runs in `HistoryRecording.InTransactionAsync`, which commits the service's changes only when the step succeeds. If History does not record (HTTP failure, or an outcome other than completed; logged at Error), the step is rolled back and rejected with `HISTORY_NOT_RECORDED`.
- This is the sample's choice. A production system would feed History from Kaleido's process events (outbox, at-least-once) instead of synchronous calls.

## Sensitive data in events
- Member fields are classified with `[Phi]` (name, display name, date of birth) and `[Pii]` (email, phone, address) from `Common/Compliance/PriorAuthDataClassifications.cs`.
- Kaleido redacts classified values in event payloads before the outbox write (ADR 0013); responses to callers keep the real values.
- The sample registers no `AddRedaction`, so classified values are erased in events (a warning is logged once). To pseudonymize or encrypt instead, add `Microsoft.Extensions.Compliance.Redaction` to a host and call `services.AddRedaction(...)` per classification.

## Environment assumptions
- Assume `ASPNETCORE_ENVIRONMENT=Development` for local work unless told otherwise
- Use `npm` for the Angular app
- Preserve existing service ports and compose service names unless the task requires changing them

## Auth model

The sample's dev auth (`Common/Auth`) stands in for a real IdP. Kaleido itself only reads `ClaimTypes.Role` and evaluates policies.

- **Every host sets `AddKaleido(…, o => o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust)`**, router included. Each leaf defines `DefaultAuthorization` (an explicit service rule), overridden by a capability's `[KaleidoAuthorization]`. A capability with neither is omitted from discovery and endpoint mapping, and denied by generic execution. The router filters the aggregate by the leaf's effective rules.
  - Intake, Member, CodeSet, ReferenceData, History: `Policy = DevAuthPolicies.AuthenticatedUser` (any logged-in caller).
  - Provider, Configuration: `Policy = DevAuthPolicies.InternalCaller` (service-to-service only).
  - Radiology: `Roles = "radiology"`.
  - Exceptions: `StartRadiologyIntakeStep` needs `radiology` **and** `InternalCaller`; `GenerateSnapshotStep`, `UpsertPriorAuthRecordStep`, and ReferenceData `PlanQueryContextSource` need `InternalCaller`.
- **Roles describe the user; the actor claim describes the call.** No user ever gets an `internal` or `intake` role.
  - Direct user call: name + the user's roles.
  - On-behalf-of hop (`DevTokenForwardingHandler`): the same name + roles, plus `kaleido_actor = {calling service}`.
  - Pure service token (`IssueServiceToken`, used by the router's registry clients and by outbound calls with no inbound user): `svc-{service}`, service app roles (`radiology, admin`), plus the actor claim. HP-026 tracks the distinction between this principal and an on-behalf-of user for handoff authorization.
- **`InternalCaller` policy** = actor claim present. Use `Policy = DevAuthPolicies.InternalCaller` for capabilities that must never be called directly by a consumer. Combine it with `Roles` to also require the user's role (e.g. `StartRadiologyIntakeStep` = `radiology` + `InternalCaller`).
- **Personas** (router `/auth/login`):
  - `alice`: no domain role. Can use Intake, but is refused at the Radiology handoff.
  - `bob`: `radiology`.
  - `carol`: `admin, radiology`.
- The router forwards the caller's token unchanged; anonymous requests stay anonymous.

## Cross-processor handoff convention

When a step handler resolves a downstream processor (e.g. Intake routing to Radiology), it:
1. Calls the downstream processor's `/processes/execute` endpoint with the full original payload and the same `ProcessId`
2. Returns `ProcessStepHandlerResult.HandOff(targetProcessorName: "radiology")` — leaving `RequiredStep` null

The handler itself returns no typed response — the downstream processor is the source of truth for its own state.

**Backend contract**: When a cross-processor handoff occurs, `StepExecutionResponse` (and `ProcessStateResponse`) will have:
- `TargetProcessorName` set to the name of the target processor (e.g. `"radiology"`)
- `RequiredStep` = null — it is intentionally absent; only the target processor knows its own required step

**Consumer pattern**: When `StepExecutionResponse.TargetProcessorName` is set, call `GET /{targetProcessorName}/processes/{processId}` on the **target processor** to obtain authoritative state. That response will contain:
- `RequiredStep` — the next step to execute on the target processor (an object; for an information step it carries the pending `informationRequest`, e.g. the MRI questions)
- `AvailableSteps` — other steps currently available on the target processor

**UI pattern** (priorauth-ui): `ProcessService.executeStep()` handles this automatically. When `targetProcessorName` is present in the response, it fetches the target processor's state, updates `currentProcessorName` in `ProcessState`, and navigates to the required step — all transparently to call sites. Subsequent `executeStep()` calls will automatically route to the new processor.

This pattern applies uniformly for any cross-processor handoff (Intake → Radiology, Intake → Oncology, etc.). See `samples/PriorAuth/HANDOFF.md` for a full walkthrough.

## Local ports
- router: `8080`
- referencedata: `8081`
- codeset: `8082`
- providersearch: `8083`
- memberservice: `8084`
- intake: `8085`
- eventcollector: `8086`
- configuration: `8087`
- radiology: `8088`
- history: `8089`
- aspire dashboard: `18888`