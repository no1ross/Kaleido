## Local tooling
- Use `podman`, not `docker`
- When validating compose/container workflows, run `podman compose ...`
- Do not suggest `docker` commands unless explicitly asked

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

## Environment assumptions
- Assume `ASPNETCORE_ENVIRONMENT=Development` for local work unless told otherwise
- Use `npm` for the Angular app
- Preserve existing service ports and compose service names unless the task requires changing them

## Cross-processor handoff convention

When a step handler resolves a downstream processor (e.g. Intake routing to Radiology), it:
1. Calls the downstream processor's `/processes/execute` endpoint with the full original payload and the same `ProcessId`
2. Returns `ProcessStepHandlerResult.HandOff(targetProcessorName: "radiology")` — leaving `RequiredStep` null

The handler itself returns no typed response — the downstream processor is the source of truth for its own state.

**Backend contract**: When a cross-processor handoff occurs, `StepExecutionResponse` (and `ProcessStateResponse`) will have:
- `TargetProcessorName` set to the name of the target processor (e.g. `"radiology"`)
- `RequiredStep` = null — it is intentionally absent; only the target processor knows its own required step

**Consumer pattern**: When `StepExecutionResponse.TargetProcessorName` is set, call `GET /{targetProcessorName}/processes/{processId}` on the **target processor** to obtain authoritative state. That response will contain:
- `RequiredStep` — the next step to execute on the target processor
- `AvailableSteps` — other steps currently available on the target processor
- Per-step results already produced (e.g. questionnaire definitions)

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