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

## Conditional questions
- Configured questionnaire items carry `EnableWhen` (binding key, operator, answer). `QuestionnaireTranslation` maps them onto the information request (binding key -> item id; operator names or FHIR symbols). An unknown operator makes Kaleido reject the request as not well-formed (`pro_information_request_invalid`) instead of asking the question always.
- Kaleido decides which questions apply (ADR 0024). The MCP adapter asks them round by round, and the UI (`information-conditions.ts`) shows a question once it applies and never sends the answers of the others.
- A handler gets no answer for a question that did not apply. `CaptureMriInfoHandler` reads a missing Laterality (BodyPart Spine) as `Laterality.None`.

## Counting query results
- Every service whose query sources read an EF Core database (CodeSet, Configuration, History, Member, Provider, ReferenceData, Radiology) calls `AddPriorAuthEfCoreQueries()` (`Common/Queryable`): a full page is counted with one `SELECT COUNT(*)` instead of reading every matching row (visible in the EF command log in Aspire). Kaleido's default executor counts an async query by enumerating it (docs/PATTERNS.md, #352).
- The executor serves every source of the service: a query that is not EF Core's (Radiology's delegated provider searches) uses LINQ.

## What a view returns
- View output records describe every property (`[Description]`): KAL2024 is an error in the samples. The descriptions reach the agent with the data (`fieldDescriptions` in MCP results).

## Sensitive data in events
- Member fields are classified with `[Phi]` (name, display name, date of birth) and `[Pii]` (email, phone, address) from `Common/Compliance/PriorAuthDataClassifications.cs`.
- Kaleido redacts classified values in event payloads before the outbox write (ADR 0013); responses to callers keep the real values.
- Every event-publishing service calls `AddPriorAuthEventRedaction()` (`Common/Compliance/PriorAuthEventRedaction.cs`): events keep PII and erase PHI. It is the event policy only (`AddEventClassificationRedaction`, ADR 0025); log redaction (`AddRedaction`) would be configured separately. To pseudonymize instead, map a classification to an HMAC or encrypting redactor there.

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
  - Exceptions: `StartRadiologyIntakeStep` needs `radiology` **and** `OnBehalfOfUser`; `GenerateSnapshotStep`, `UpsertPriorAuthRecordStep`, and ReferenceData `PlanQueryContextSource` need `InternalCaller`.
- **Roles describe the user; the actor claim describes the call; the principal type says whether a user is behind it.** No user ever gets an `internal` or `intake` role. Every token carries `kaleido_principal` (`user` or `service`), signed into it.
  - Direct user call: name + the user's roles, `kaleido_principal = user`.
  - On-behalf-of hop (`DevCallCredentialProvider`, the services' `IKaleidoCallCredentialProvider`, ADR 0022): the same name + roles, plus `act = {calling service}` (the standard actor claim; Kaleido records it as the caller's actor on events and spans), `kaleido_principal = user`.
  - Pure service token (`IssueServiceToken`, used by the router's registry clients and by outbound calls with no inbound user): `svc-{service}`, service app roles (`radiology, admin`), plus the actor claim, `kaleido_principal = service`.
- **`InternalCaller` policy** = actor claim present. Use `Policy = DevAuthPolicies.InternalCaller` for capabilities that must never be called directly by a consumer; a service acting on its own passes it.
- **`OnBehalfOfUser` policy** = actor claim present **and** `kaleido_principal = user`. Use it for a handoff that must carry a real user: combine it with `Roles` for the user's role (`StartRadiologyIntakeStep` = `radiology` + `OnBehalfOfUser`). A pure service token never passes it, even though its app roles include `radiology`, so the router's aggregate (fetched with its service token) does not list that step; no consumer could call it directly anyway.
- **Personas** (router `/auth/login`):
  - `alice`: no domain role. Can use Intake, but is refused at the Radiology handoff.
  - `bob`: `radiology`.
  - `carol`: `admin, radiology`.
- The router registers `ForwardCallerToken()`: MCP tool calls send the user's token unchanged (never an actor: that would satisfy `InternalCaller`), and its registry fetches keep the router's service token. Anonymous callers stay anonymous on every hop: Kaleido never upgrades them to a service token.

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