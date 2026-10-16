## Local tooling
- Use `podman`, not `docker`
- When validating compose/container workflows, run `podman compose ...`
- Do not suggest `docker` commands unless explicitly asked
- npx @microsoft/aspire-cli dashboard run - to run Aspire for OTel

## PriorAuth layout
- `samples/PriorAuth/Compose/` contains local orchestration files
- `samples/PriorAuth/priorauth-ui/` is the Angular frontend
- `samples/PriorAuth/Services/*/` are the ASP.NET Core services (targeting .NET 8); `Router/` is the gateway with the MCP adapter, `EventCollector/` receives the services' events, and `Seeder/` seeds the shared databases
- `samples/PriorAuth/Common/` holds the steps, handlers, query sources, data and dev auth the services share

## Verification
- For backend changes, prefer `dotnet build` on the affected project
- Run `dotnet test` for any affected backend test projects
- For UI changes, run from `samples/PriorAuth/priorauth-ui`:
  - `npm test`
  - `npm run build`
- If a change touches UI + backend integration, validate both

## Local run workflow
- Prefer the compose file under `samples/PriorAuth/Compose/` (`docker-compose.yml`; `docker-compose.override.yml` is for local overrides and sets nothing by default): it runs the router, every service and the UI as Production (see [Configuration per environment](#configuration-per-environment))
- For backend-only work, `dotnet run` the affected services instead
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

## Process id on every call (MCP and UI)
- A request's queries are tracked with its process: every query of a request must carry its process id (`X-Kaleido-Process-Id`).
- UI: the global `kaleido-correlation-interceptor` already sends it on every request while a process is active; member search etc. still work without one before a process exists.
- MCP: the router uses `Kaleido:Mcp:ToolListing = FrontDoor` (ADR 0016) with `ListedTools = [history_prior_auth_record_list]`. Its instructions tell the agent to start the intake first and pass that processId on every call, searches included; the adapter sends it as the header and fills a search's own `processId` parameter from it.
- Radiology's steps and the provider searches they need are not on the agent's tool list: they arrive in `next` (inputs, hints, the search with the processId pre-filled) and run with `call_tool`.

## The member cannot change once the request is under way
- Radiology's `ValidateMemberStep` / `CaptureMemberStep` exist for a request handed off without a member (service captured first in Intake; `StartRadiologyIntake` then requires Validate Member). They close when the MRI or CT questions are answered (`[AvailableUntil<CaptureMriInfoStep>]`, `[AvailableUntil<ConfirmCtInsteadOfMriStep>]`): the member is settled before those questions in every path.
- After that, Radiology offers only Capture Requested Service, Remove Requested Service and Capture Requesting Provider (which closes the other two). Calling a member step then is refused with `pro_step_not_available`.

## Errors a caller can fix
- Request paths throw `KaleidoValidationException` with the sample's own codes (`Common/Compliance/PriorAuthErrorCodes.cs`), never `InvalidOperationException` (a 500 the caller cannot act on) and never Kaleido's `qry_*`/`pro_*` codes. Step handlers reject with a business message instead of throwing.
- Query parameters that a source needs are `[Required]` (nullable for value types, e.g. `Guid? ProcessId`), so Kaleido rejects a missing one with 400 before the source runs.

## Sensitive data in events
- Member fields are classified with `[Phi]` (name, display name, date of birth) and `[Pii]` (email, phone, address) from `Common/Compliance/PriorAuthDataClassifications.cs`.
- Kaleido redacts classified values in event payloads before the outbox write (ADR 0013); responses to callers keep the real values.
- The sample registers no `AddRedaction`, so classified values are erased in events (a warning is logged once). To pseudonymize or encrypt instead, add `Microsoft.Extensions.Compliance.Redaction` to a host and call `services.AddRedaction(...)` per classification.

## Configuration per environment
- **Containers run as Production** (images are built `Release`; compose sets no `ASPNETCORE_ENVIRONMENT`), so they read only each host's `appsettings.json`: container addresses (`http://member:8080`, `http://eventcollector:8080`), `data/...` paths (`/app/data` in the container) and the dashboard's OTLP endpoint `http://aspire-dashboard:18889`. Swagger is Development-only, so it is off in containers.
- **`dotnet run` is Development**: `appsettings.Development.json` overrides those with `localhost` ports, `../../data/...` paths and `http://localhost:4317`.
- Do not set `ASPNETCORE_ENVIRONMENT=Development` in compose: the containers would read the `localhost` settings.

## Telemetry
- Each Kaleido service configures its export under `Kaleido:OpenTelemetry` (`Endpoint`, `Protocol`, `ResourceAttributes`); `service.name` is its `Kaleido:ServiceName`. See [`src/Kaleido.Observability.OpenTelemetry/README.md`](../../src/Kaleido.Observability.OpenTelemetry/README.md).
- Compose sets no `OTEL_*` variables: the configuration applies as written. A standard `OTEL_*` variable, if set, still wins.
- The EventCollector is not a Kaleido service: it wires OpenTelemetry itself from its own `OpenTelemetry` section, with the same precedence.

## Environment assumptions
- Assume `ASPNETCORE_ENVIRONMENT=Development` for `dotnet run` on this machine, and Production in containers
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
1. Calls the downstream processor's entry step through the Kaleido process client (`ExecuteStepAsync`, i.e. `POST /{processor}/processes/steps/{step}`) with a typed payload built for it (e.g. `StartRadiologyIntakeStep`) and the same `ProcessId`; it does not forward the original request
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
- provider: `8083`
- member: `8084`
- intake: `8085`
- eventcollector: `8086`
- configuration: `8087`
- radiology: `8088`
- history: `8089`
- aspire dashboard: `18888`