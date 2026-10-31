# Changelog

All notable changes to the Kaleido packages. The packages share one version
([`docs/VERSIONING.md`](docs/VERSIONING.md)); during the preview any release may contain breaking
changes, each named in its pull request. The format follows [Keep a Changelog](https://keepachangelog.com/);
from 1.0 the versions follow [Semantic Versioning](https://semver.org).

## [Unreleased] - 1.0.0 previews

The preview builds (`1.0.0-preview.{height}`): published to the repository's GitHub Packages feed,
and tagged previews also to nuget.org.
The changes below are grouped by area; the pull requests carry the details and their breaking-change
sections.

### Breaking changes
- **A caller is never described a step or a query they may not use** (#524, ZeroTrust and
  Authenticated):
  - the registry's relationship lists (`dependencies`, `availableAfter`, `availableUntil`) list only
    the steps the caller may run;
  - the execute, per-step and process-state responses list only the next steps the caller may run;
    a required step they may not run gives `requiredStep: null`, and a list none of which they may
    run is empty; either way a new `notice` (`KaleidoNotice`, code `pro_not_permitted`) says so
    without naming a step;
  - a step input whose lookup names a query the caller may not use keeps the input, without the
    lookup, with a `notice`. A query on another service is checked by the rules its registry
    publishes to this host's client: hosts that check each other's lookups share policy names.
  - The MCP adapter passes the notice on and tells the agent not to guess another step.
- **A step whose outcome cannot be saved is answered as having run, and is recovered** (#537, [ADR 0034](docs/decisions/0034-unsaved-step-outcomes.md)):
  when the save fails after the handler returned, the step is no longer recorded as a failed step
  (with a second save) or answered as a generic error.
  - **Answers:** `503 pro_outcome_pending_recovery` (the outcome is kept and saved when the store is
    back), `500 pro_outcome_not_saved` (it could not be kept) or `409 pro_outcome_not_saved` (another
    request changed the process meanwhile; this was `pro_process_conflict`). A keyed retry gets
    `pro_idempotency_outcome_unknown`.
  - **Recovery:** an in-memory recovery service saves kept outcomes once the store is back; until
    then every step on the process is refused with `409 pro_process_recovering`, so the handler is
    never run twice. A restart loses pending outcomes; each one that is not saved is logged at
    Critical with what to reconcile.
  - **Telemetry:** `kaleido.processor.step.outcome_unsaved`, `kaleido.processor.recover` and the
    `outcomes_unsaved` / `outcomes_recovered` / `outcomes_parked` counters. A step's decision is
    recorded only once it is saved.
  - **MCP:** the agent is told the step ran and not to repeat it, and a failure to read where a
    completed step continues no longer reports the step as failed.
  - **Contributors:** source rule KAL0022 reports one `try` whose `catch` handles the failure of an
    operation with an effect and of a later operation on another dependency together. It found two more
    instances, fixed: after a completed step, the MCP adapter no longer reports the step as failed when
    the client cannot show its questions or the answers cannot be submitted (a note instead), and the
    event relay no longer abandons the rest of a batch when recording one entry's attempt fails.
- **A cancellation is the caller's token firing, not an exception type** (#517). An
  `OperationCanceledException` that no token of the caller raised (a downstream or database timeout,
  an `HttpClient` timeout) is now a failure everywhere:
  - a step whose handler times out is recorded and answered as `exception` (retryable), not `canceled`;
  - a query records a failure, not a cancellation;
  - the HTTP transport answers 500, where it wrote an empty 200;
  - the MCP adapter returns an error result to the agent;
  - the outbox, the relay and the idempotency finalization log it instead of letting it escape.

  A request the client aborted is still not an error. KAL2004 and KAL0018 accept
  `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)` before a
  catch-all.
- **An ended process admits no further step** (#512, #534, [ADR 0033](docs/decisions/0033-process-lifecycle-and-admission.md)):
  admission now reads the process state, not only the step graph. A step sent to a process that is
  `complete`, ended in `businessFailure` or was handed off is refused with the new
  `pro_process_ended` (status `pending`); the state lists no available step, as before. After a
  hand-off, the target owns the process: this service runs none of its steps (a local step used to
  run and erase the target), except when the target hands the process back (its calling processor is
  the target), which reopens it here. `Failure()` is a hard stop: start a new process.
- **`ProcessStepHandlerResult.Complete()` ends a process whatever its rules still offer** (#534):
  `Complete(messages)` and `Complete(response, messages)`, with `IProcessStepHandlerResult.CompletesProcess`.
  A result that completes the process cannot require a next step. The new consumer analyzer
  KAL2022 reports one returned for an entry step.
- **One status model: where the process is, and what happened to the step** (#536):
  - The StepExecutionOutcome enum is removed: it duplicated `StepExecutionStatus`. The step answer
    (`StepExecutionResponse`) carries `Status` (JSON `status`) instead of `Outcome` (`outcome`), with
    the detailed values: `completed`, `rejected`, `validationFailed`, `failed` (business failure),
    `exception` (a defect; retry), `canceled`, or `pending` for a submitted step that did not run.
    Code reading `outcome` reads `status`; a former `failed` is now `failed`, `validationFailed` or
    `exception`. The MCP adapter's own `outcome` field carries these values.
  - `ProcessExecutionState` loses `ProcessViolation`, `Exception` and `Canceled`, which nothing sets:
    a step that breaks a process rule, throws, fails validation or is cancelled leaves the process
    as it was, and only the step's status records it. A request abort no longer moves the process
    to `Canceled`. The remaining values renumber (strings are unchanged); SQLite stores holding a
    removed value must be recreated.
  - `StepExecutionStatus.Skipped` is removed (never recorded), and a validation failure is now
    recorded in the history as `ValidationFailed` (it was `Exception`).
  - The `StepCompleted` event carries `Status` (was `ExecutionStatus` and `Outcome`); it keeps
    `DecisionType`, and `PlanBuilt` keeps `CandidateStatus`: `ExecutionDecisionType` and
    `StepCandidateStatus` are the events' audit vocabulary, not part of the transport answers.
- **Invalid hand-offs, dead step graphs and process violations are no longer silent** (#522):
  - `ProcessStepHandlerResult.HandOff` refuses a null, empty or blank target (`ArgumentException`).
    A hand-off to the service itself is a process violation (`pro_invalid_hand_off`), no longer a
    local continuation.
  - A cycle through `DependsOn` and `AvailableAfter` together (either one alone, or mixed) fails
    startup with `pro_invalid_registration`: its steps could never become available. `AvailableUntil`
    takes no part in cycles.
  - A process violation (an invalid next step or information request, a hand-off to itself) behaves
    like an unexpected error: the step is recorded as `Exception`, the process keeps its state and
    next steps, and the step can be retried once the handler is fixed. It no longer moves the process
    to `ProcessViolation` with the step recorded as completed.
- **`FrameworkErrorCodes.DataConversionError` is removed** (#505): nothing raised
  `data_conversion_error`; value conversion failures are `argument_error` or
  `qry_invalid_filter_value`, as before. Code that names the constant no longer compiles.
- **Two error codes take their area prefix** (#447, [ADR 0032](docs/decisions/0032-public-api-at-1-0.md)):
  `ProcessorErrorCodes.MissingStepResult` is now `pro_missing_step_result` (was `missing_step_result`)
  and `HttpClientErrorCodes.MissingBaseUrl` is now `httpclient_missing_base_url` (was
  `missing_base_url`). Code using the constants is unaffected; code comparing the strings changes. The
  public API was reviewed type by type (kept current until 1.0); every other public type is kept.
- **Information requests have an exchange identity** (#442,
  [ADR 0031](docs/decisions/0031-information-requests-have-an-exchange-identity.md)): Kaleido issues
  `InformationRequest.ExchangeId` every time a handler requires information (a value the handler
  sets is replaced; the rounds of one request share it), and every `IInformationStep` declares
  `ExchangeId` next to `InformationRequestId` and `Items` (KAL2012). Answers must echo the
  `exchangeId` received with the questions; answers to an earlier exchange - even of the same
  questionnaire with the same item ids - are rejected with the new `pro_information_response_stale`
  before any handler runs. The MCP adapter captures the id when it shows questions and submits it;
  the agent now gets an `answersToken` with every set of questions and passes it back.
- **Idempotency admissions are fenced; an unknown outcome is kept** (#441, ADR 0019 amendment): custom
  `IKaleidoIdempotencyStore` implementations change. `IdempotentResult` and `IdempotentResultState`
  move to `Kaleido.Storage`; `IdempotentResult` gains `AdmissionId` (set on admission);
  `CompleteAsync` returns `Task<bool>` and updates only the record still in progress under that
  admission id (never inserts); `ReleaseAsync(key, admissionId)` returns `Task<bool>` and deletes only
  that admission's in-progress record; a new state `OutcomeUnknown`. On HTTP, a 4xx releases the key,
  a 5xx or an exception marks the outcome unknown (a retry gets `pro_idempotency_outcome_unknown` at
  once), and a store failure while finalizing is logged instead of changing the answer. An admission
  in progress and an unknown outcome are kept for the new
  `KaleidoHttpOptions.IdempotencyUnknownOutcomeRetention` (default 7 days), separately from the
  replay `IdempotencyRetention`. The SQLite `IdempotentResults` table gains an `AdmissionId` column:
  recreate development databases.
- **Options are immutable; configuration overrides code** (#445,
  [ADR 0030](docs/decisions/0030-options-are-immutable-configuration-overrides-code.md)): every options
  type is a record with `init` properties, and the registrations take an optional options object
  instead of a configure lambda: `AddKaleido(configuration, new() { ServiceName = "orders", Assemblies = [...] })`,
  `AddHttp(new() { ... })`, `MapKaleidoHttp(new() { ... })`, `AddOpenTelemetry(new() { ... })`,
  `AddMcp(new() { ... })`. The service and MCP options builders are removed.
  Each registration lays its configuration section over a copy of the object from code, so
  **configuration now wins over code** setting by setting (it was the other way round); `Kaleido:Http`
  and `Kaleido:Http:Map` are new sections. Kaleido no longer adjusts what it is given: a blank MCP
  `ServerName`/`ServerVersion`, a blank or repeated `ListedTools` name and `Instructions` together
  with `InstructionsFile` now fail startup (`mcp_invalid_options`) instead of being replaced, trimmed
  or ignored; HTTP and client settings are validated at startup (`invalid_http_options`,
  `httpclient_invalid_options`), and a configured value that cannot be read is an error naming it
  (`invalid_options`). KAL2006 now reads the options object passed to `AddKaleido`.
- **Capabilities have no version** (#474, [ADR 0029](docs/decisions/0029-capabilities-have-no-version.md)):
  remove `Version = ...` from every `[ProcessStep]`, `[QuerySource]` and `[QueryView]` (KAL2001-KAL2003
  now require only `DisplayName` and `Description`). The name is the identity; a breaking change
  takes a new name, as before. The version is gone from the registry and wire contracts (step
  contract, step history, source and view responses), the process state (`StepContext`; recreate
  SQLite development databases), the events (`StepCompleted` and the `PlanBuilt` candidates no
  longer carry a step version) and the telemetry (`kaleido.processor.step_version`).
- **Stores are chosen explicitly** (#438, [ADR 0028](docs/decisions/0028-stores-are-chosen-explicitly.md)):
  a host that leaves a store it uses at the in-memory default no longer starts
  (`stores_not_chosen`): the context and idempotency stores when the service has process steps, the
  outbox store when it publishes through Kaleido's outbox. Register durable stores, or call the new
  `AddInMemoryStores()` for development and tests. Query-only services, the host's own outbox and
  direct publication are not affected.
- **The packages move together** (#431): every dependency on a sibling Kaleido package is an exact
  version (`[x.y.z]`), so upgrading one package upgrades the family, and a mixed set fails at restore
  instead of at run time (the packages share internal seams).
- **One role semantics on every surface** (#433): roles compare ordinally (case-sensitive) and are
  read from each identity's `RoleClaimType`, exactly as ASP.NET's `RequireRole` on the typed routes.
  Before, generic execute and discovery matched roles case-insensitively and only from
  `ClaimTypes.Role`, so a caller could be admitted on one surface and refused on another
  ([Authorization](docs/AUTHORIZATION.md)).
- **Stable MCP tool names** (#435): names are derived from the unfiltered registry, not from what
  the caller may see; when two capabilities (or a capability and a built-in tool) derive the same
  name, each gets `_` plus a hash of its own identity instead of `_step`/`_view`/`_2` numbering, so a
  name can no longer move to another capability when a caller sees fewer of them.
- **A client's route prefix must be a service name** (#434, [ADR 0027](docs/decisions/0027-forward-caller-token-kept-with-guards.md)):
  `AddHttpClients()` refuses a `RoutePrefix` (or lowercased client name) outside `[a-z0-9][a-z0-9_-]*`
  with `httpclient_invalid_route_prefix`. With call credentials registered, a client whose base URL is
  neither `https` nor loopback is logged as a Warning at startup. `ForwardCallerToken()` is documented
  as a single-audience choice; an OAuth-protected MCP endpoint exchanges the token per service.
- **Authorization is enforced by default:** `AuthorizationMode` defaults to `Authenticated`; a host
  without authentication sets `None` explicitly ([ADR 0021](docs/decisions/0021-authenticated-by-default.md), #400).
- **One credential model for outgoing calls:** `IKaleidoCallCredentialProvider`; an MCP host registers
  `ForwardCallerToken()` or its own provider ([ADR 0022](docs/decisions/0022-call-credentials.md), #403).
- **Store contracts split** into `IKaleidoContextStore`, `IKaleidoOutboxStore` and
  `IKaleidoIdempotencyStore` ([ADR 0020](docs/decisions/0020-three-store-contracts.md), #399).
- **Outbox integration** ([ADR 0023](docs/decisions/0023-outbox-integration.md), #408): the context store is
  state only (`SaveAsync(context)`); every event goes to `IKaleidoOutbox` - Kaleido's built-in outbox
  (default), the host's own (`AddOutbox<T>()`) or `AddDirectPublication()` - and the optional
  `IKaleidoTransactions` makes a state change and its event atomic. `IKaleidoOutboxStore.EnqueueEventsAsync`
  is now `AddAsync`. New builder methods `AddContextStore`, `AddIdempotencyStore`, `AddOutboxStore`,
  `AddTransactions`; new startup codes `conflicting_store`, `conflicting_outbox`.
- **Event redaction separate from log redaction** ([ADR 0025](docs/decisions/0025-event-redaction-policy.md), #278):
  `AddEventRedaction(r => ...)` sets the event policy with the standard `IRedactionBuilder`
  API; the host's `AddRedaction` now applies to logs only. **Behavior change:** without
  `AddEventRedaction`, classified values in events are erased (with a one-time warning),
  even when the host has a log policy.
- **Versioning concept** ([ADR 0026](docs/decisions/0026-versioning-concept.md), #197): a version is a label;
  one live version per capability (a breaking change takes a new name); in-flight processes continue on
  the deployed definition. Fix: a step that ran keeps the version it ran with (`StepContext.Version`)
  instead of taking the deployed one at the next request.
- **Conditional questions** ([ADR 0024](docs/decisions/0024-conditional-questions.md), #235):
  `InformationItem.EnableWhen` (`InformationCondition`: question, operator, answer) and `EnableBehavior`.
  Only enabled questions must be answered; an answer to a disabled one is rejected. The MCP adapter asks
  in rounds (forms, and chat with `answersToken`) and never shows the agent the conditions. Additive on
  the wire.
- **Output descriptions for agents** (#213): new consumer rule KAL2021 (step results and view outputs
  need descriptions; an error in the samples). MCP query and step results carry `fieldDescriptions`.
- **SQLite registrations:** `UseSqliteKaleidoContextStore` is replaced by `AddSqliteContextStore`,
  `AddSqliteOutboxStore` and `AddSqliteIdempotencyStore` (one database; `connection_string_mismatch`);
  `ISqliteKaleidoTransaction` lets a host's own outbox join the SQLite transaction (#408).
- **Idempotent execution admission:** the idempotency store's `AdmitAsync`/`CompleteAsync`/`ReleaseAsync`;
  new 409 codes `pro_idempotency_in_progress` and `pro_idempotency_outcome_unknown`
  ([ADR 0019](docs/decisions/0019-idempotent-execution-admission.md), #397).
- **Repeated registration:** a second `AddKaleido()` or `AddHttp()` is a startup error (`duplicate_registration`, #400).
- **Kaleido's HTTP behavior only on Kaleido's endpoints:** no whole-host middleware or startup filter (#394).
- **Error responses:** 500 bodies and span status never carry exception text (#393); `/execute` answers
  400 for a step it cannot build (#392).
- **Public API surface:** core runtime internals and registry descriptors made internal; wire contracts
  self-contained in `Kaleido.Http.Abstractions` ([ADR 0017](docs/decisions/0017-transport-contract-placement.md), #389, #390).
- **Public API baseline** (#313): each package's public API is recorded in `PublicAPI.Shipped.txt` /
  `PublicAPI.Unshipped.txt`; an unrecorded change fails the build. `KaleidoHttpClientException` has
  one constructor: `(code, message, statusCode, errors = null, innerException = null)`.
- **Public API trimmed** (#313): `QueryBody`, `QuerySort` and `QueryPage` are sealed. The properties of
  `ProcessStepHandlerResult` and `ProcessStepHandlerResult<T>` can no longer be set by consumers: a
  result comes from `Success`, `Reject`, `Failure`, `HandOff`, `RequireInformation` or `Next`, so it
  cannot contradict itself (e.g. succeeded and rejected at once).
- **Cancellation tokens are required** (#313): no public async API defaults its `CancellationToken`
  any more (handlers, async sources and views, delegated sources, `IEventPublisher`, the store
  contracts, `IKaleidoOutbox`, `IKaleidoTransactions`, the Kaleido clients, `IRegistrySnapshotStore`),
  so a caller cannot silently drop cancellation. Callers pass their token; an implementation may keep
  a default. Parameters that preceded the token became required too: the by-name
  `ExecuteStepAsync(stepName, processStep, processId, cancellationToken)` (pass `null` for no
  process). `QueryApiRequest` and `QueryApiRequest<TParameters>` are sealed.
- **Analyzer rule IDs renumbered** (#416): rules removed before the first release (#401 and earlier) no
  longer leave gaps; each family is numbered contiguously. A preview's suppressions or `.editorconfig`
  severities by ID need the new number:
  - consumer: KAL2009..KAL2024 are now KAL2006..KAL2021;
  - source: KAL0005..KAL0015 are now KAL0004..KAL0014, KAL0018..KAL0023 are now KAL0015..KAL0020;
  - test: KAL1012 and KAL1013 are now KAL1011 and KAL1012.

### Added
- **Deployment modes are stated** (#449): framework-dependent and self-contained publishing are
  supported; trimmed and NativeAOT publishing are not, since discovery and dispatch use reflection
  ([VERSIONING.md](docs/VERSIONING.md#deployment-modes)).
- **An enforced confirmation round in the MCP adapter** (#439): with `Kaleido:Mcp:RequireConfirmation`
  (off by default), a step declared `RequiresConfirmation` runs only after the user accepts a form the
  adapter shows (in the session, or as an MRTR round), whichever tool reaches it; declining runs
  nothing, and a client without forms gets `mcp_confirmation_required`. The agent's own claim never
  counts. It raises the bar and is documented as not being proof of consent.
- **Service text is untrusted prompt content** (#439): the MCP README's trust boundary says that
  descriptions and messages from aggregated services can try to steer the agent, and the default
  instructions tell the agent to treat them as data, never as instructions.
- **Public distribution** (#432): tagged versions are published to nuget.org with trusted publishing
  (no stored API key) as well as to GitHub Packages; every package has a signed build provenance
  attestation (`gh attestation verify`) and carries the `LICENSE` text next to `NOTICE`
  ([RELEASING.md](RELEASING.md)).
- Shared registry snapshots: `AddDistributedRegistrySnapshots()` stores registry snapshots in the host's `IDistributedCache` (e.g. Redis), so the replicas of a router share one snapshot and one round of downstream checks; an unreachable cache never fails a registry read. Client copies are keyed per consuming service (`kaleido-client:{ServiceName}:{clientName}`) (#275).
- Consumer guide (`docs/guide`): concepts, events and the outbox, persistence, deployment and choosing options, around one orders service whose examples are compiled, checked against the pages in CI (`tools/docs/check-guide-snippets.py`) and run by tests (#409).
- Warnings fail the build in `tests/` and in the samples; IDE code-style rules run in `tests/` and `tools/` (#406).
- The samples count full query pages with one `SELECT COUNT`; the documented EF Core executor also serves non-EF sources in the same host (#352).
- Front-door provenance on events and spans: `Origin`/`OriginTool` end to end, `CallerActor` per hop (#403).
- MCP trust-boundary, instructions-resource and integration documentation; functional trust tests (#403).
- Help links on every consumer analyzer rule; a clean-consumer package test in CI (#401).
- Release workflow publishing to GitHub Packages; `RELEASING.md`; this changelog (#318).

### Changed
- **One controllable clock** (#460): leases, idempotency retention, the event relay's retries,
  timeouts and circuit break, the registry caches' TTLs and the MCP lookup states' lifetime read the
  injected `TimeProvider`. `AddKaleido()` registers `TimeProvider.System` with `TryAdd`, so a host can
  register its own first. Recorded timestamps (`CreatedUtc`, `OccurredOn`) keep the wall clock: the
  clock is a dependency only where Kaleido's behavior depends on the time. KAL0021 reports a
  wall-clock read in `src/` that is compared or used in date arithmetic (#501).
- **The Kaleido clients are built by the container** (#498): each client configured under
  `Kaleido:Clients` is registered once, and its Process and Queryable clients, health check and
  credential handler are keyed services under its name, so a fixed client can be injected with
  `[FromKeyedServices("Radiology")]`; the factories resolve a name chosen at runtime. Sending a call
  is one shared transport, and the registry caches are singletons; no Kaleido service creates
  another. Log messages of client calls now name the client and the service ("Sending POST ... through
  Kaleido client 'Radiology' to service 'radiology'"). KAL0006 accepts a keyed resolution whose key is
  only known at runtime.
- **The MCP server reports the host's version** (#463): `KaleidoMcpOptions.ServerVersion` is optional;
  left unset, the initialize response carries the host application's version (the entry assembly's
  informational version without its `+commit` suffix, else its assembly version) instead of a fixed
  `1.0.0`. A version the host sets is reported as given; raise it when the tools or instructions a
  client sees change meaningfully.
- **The core shapes on the wire are named as wire contracts** (#448, ADR 0017 amendment):
  `InformationRequest`, `ProcessMessage`, `KaleidoCaller`, the Process state enums,
  `AuthorizationMetadata`, `FilterOperator`/`MatchMode` and `QueryResult<T>` travel as they are and
  change additively only after 1.0; snapshot tests fix their JSON. The behavior
  `Kaleido.Http.Abstractions` holds on purpose (registry snapshot stores, header sanitizing,
  `ToApiBody()`) is recorded, and the contributor guides match the code.
- **The full samples live in their own repositories** (#95):
  [Kaleido-Samples-PriorAuth](https://github.com/no1ross/Kaleido-Samples-PriorAuth) and
  [Kaleido-Samples-ECommerce](https://github.com/no1ross/Kaleido-Samples-ECommerce), consuming the
  preview packages. Here, the consumer guide's compiled examples ([`docs/guide`](docs/guide/README.md))
  are the task-focused examples, and the repository has no `samples/` folder: the SQLite starter
  sample was removed. Database files were removed from the repository's history.
- **The repository builds with the .NET 9 SDK**, the organization's standard SDK (#424); the tests run
  per project (`tools/test/Run-Tests.ps1`). The packages still target `net8.0`.

### Fixed
- **HTTP edges** (#521):
  - `POST /{service}/processes/execute` binds its body with Kaleido's own JSON rules, like the per-step
    routes: a malformed body gets Kaleido's 400 with an error body (not the host's page or a bare 400),
    and a host's `HttpJsonOptions` naming no longer rewrites the endpoint's request contract.
  - A client `BaseUrl` that is not an absolute `http`/`https` URL fails at startup with
    `httpclient_invalid_options`, naming the client and the key, instead of a raw `UriFormatException`.
- **Idempotency keys: an unambiguous scope, random keys for callers without an identity, the process id on a replay** (#514):
  - The scope is the tuple {service, identity kind, identity, key}, length-framed and hashed (`v1:` +
    SHA-256), so a separator in a name or a key can no longer join two callers' keys (`alice` +
    `tenant:K` and `alice:tenant` + `K` were one admission). **Stored format change:** records admitted in
    the earlier `service:caller:key` format no longer match; they expire by their retention.
  - A caller without an identity shares one key space: its key must be random, and one shorter than 16
    characters is refused with 400 `pro_invalid_idempotency_key` (a GUID qualifies).
  - A replay sets `X-Kaleido-Process-Id` from the stored answer, as the first answer did.
  - A conflict after the handler ran keeps the key as outcome unknown (#537); only refusals before
    anything ran release it.
- **A caller without a name is authenticated all the same** (#515). Authorization read authentication from
  the caller's display name, so a subject-only or client-credentials token got no roles and counted as
  anonymous in discovery, generic execute and the typed-route checks. The transport now carries the
  principal's `Identity.IsAuthenticated` as `KaleidoCorrelationContext.CallerIsAuthenticated` (new), roles are
  read for any authenticated principal, and authorization uses the flag.
- **The SQLite context store keeps a hand-off's target, recovers from a rolled-back transition, and tells a duplicate process apart without reading the error message** (#513):
  - The target is in its own `ProcessContexts.TargetProcessorName` column, so a hand-off (no required step) keeps it across a reload; `ProcessRequiredSteps.ProcessorName` is removed. **Schema change:** recreate a development `*-process.db`.
  - A rolled-back transition no longer leaves the `DbContext` tracking the process at the version it was not saved at, so the next save in the same scope is not a phantom 409.
  - A new process's row is inserted on its own: a primary-key violation there is a concurrent creation (409); any other constraint failure is a store failure (it was a 409 whenever the message named the table).
- **A step handler's or query source's scope is disposed asynchronously** (#523): a scoped dependency
  that implements only `IAsyncDisposable` (an async client, a pooled context) made the synchronous
  disposal throw after the handler or source had run. It is now disposed on success, failure and
  cancellation; synchronous disposables are disposed as before.
- **KAL2006 and KAL2004 judge Kaleido's symbols, not names** (#465): KAL2006 only reports a call that
  binds to Kaleido's `AddKaleido`, and only counts an assignment to the real
  `KaleidoServiceOptions.Assemblies`, so a method of yours named `AddKaleido` is no longer flagged.
  KAL2004 only judges the `ExecuteAsync` that implements Kaleido's `IProcessStepHandler` (including
  an explicit implementation), not any interface of that name.
- **MCP hosts in one process no longer share translated tools** (#464): the tool catalogs were cached
  in one process-wide dictionary, keyed by a few settings, and cleared entirely at 256 entries. Each
  `AddMcp()` registration now owns its cache, bounded at 256 registry revisions and dropping the least
  recently used one at a time; a registry without a revision is translated every time.
- **The MCP guided-lookup state is protected** (#452): with MRTR, the search results, process and
  arguments the client carries between the lookup's forms were readable Base64 that the adapter then
  trusted to fill the step's inputs. They are now encrypted and signed with ASP.NET Data Protection,
  bound to the caller and valid for 30 minutes; a changed, forged, oversized, expired or another
  caller's state is refused with `mcp_invalid_arguments` and nothing runs. Hosts with several
  instances share a Data Protection key ring: the deployment guide shows the setup, a host without key
  storage is warned at startup (outside Development), and a form answered on an instance without its
  key is logged at Warning with the fix and counted in `kaleido.mcp.lookup_states_refused`
  (reason `unknown_key`); the user is told to search again.
- **The guide's MCP and handoff examples work as written** (#450): the getting-started MCP recipe
  had no call-credential registration, so `AddMcp()` refused to start; it now calls
  `ForwardCallerToken()` and is hosted end to end by a Guide test (tools listed, a tool called). The
  handoff example only returned `HandOff("returns")`, which starts nothing in the target service; its
  handler now opens the return there through a Kaleido client, and a two-service test reads the
  target process under the same id and runs its next step. The MCP README's opening recipe is
  complete (assemblies, authentication).
- **MCP retries and step tool hints** (#440): every non-repeatable step tool advertised
  `idempotentHint: true`, also an initial step, which called again without a `processId` starts
  another process; and an agent's own re-call got a new `Idempotency-Key`. Only a non-repeatable step
  continuing a process is now idempotent, and the adapter derives one key per logical call (same
  non-repeatable step, inputs and process; same answers to one information request), so the repeat
  gets the stored answer. **Breaking:** `IKaleidoProcessorClient.ExecuteAsync` and
  the by-name `ExecuteStepAsync` take an `idempotencyKey` (`null` for a new key per call), so a caller
  can make its own retries safe.
- **KAL2021 covers delegated sources' results** (#454): a delegated source's `TResult` (published in
  the registry, often not its query context) was never checked for descriptions; undescribed
  properties are now reported like those of step results and view outputs.
- **Bounded processor metric dimensions** (#453): `kaleido.processor.executions` and the
  submitted-step-count histogram were tagged with `kaleido.calling.processor`, a value from the
  `X-Kaleido-Calling-Processor` request header, so each distinct value created a new time series.
  **Telemetry change:** the tag is removed from metrics; it stays on spans, logs and events.
- **Direct publication never publishes a rolled-back transition's event** (#451): with
  `AddDirectPublication()`, an event added to a transition whose commit failed stayed pending and
  was published by the next commit in the same request (e.g. after the failure save). A transition
  that does not commit now discards its event.
- **Collection and dictionary metadata** (#446): an enumerable's item type is the `T` of the
  `IEnumerable<T>` it implements (it was the first generic argument: `Dictionary<string,int>` was
  published as an array of strings, a subclass of `List<T>` as an array of objects). A dictionary
  with string keys is now `"object"` with its value type as `ItemType` (MCP: `additionalProperties`);
  **breaking:** a dictionary with any other key type fails registration (`unsupported_data_type`).
- **One error mapping for both Kaleido clients** (#444): the queryable client let a `JsonException`
  escape for a 400 whose body was not JSON (a proxy's HTML page), and both clients read the service's
  error codes only for 400 (and 409): a 403, 404 or 422 lost them. Every non-success answer is now a
  `KaleidoHttpClientException` with the service's errors when it sent any (409 `httpclient_conflict`,
  400 `httpclient_validation_failed`, otherwise `httpclient_request_failed`).
- **Error bodies keep Kaleido's wire format** (#443): the exception handler serialized
  `{"errors":[{"code","message"}]}` with the host's `HttpJsonOptions`, so a host with another naming
  policy (e.g. PascalCase) changed the published error contract; it now uses `KaleidoJsonOptions`.
- **Calls made by step handlers and query sources go out for the caller** (#428): a handler's or a
  source's call to another service was the service's own call (its credential, `ServiceOwn`), because
  their DI scope did not get the request's inbound caller. Steps that need a user behind the call
  (an on-behalf-of policy) refused every such call, and an anonymous caller reached the downstream with
  the service's credential. Both scopes now carry the caller, as they carry the correlation (ADR 0022).
- The PriorAuth UI sends an `Idempotency-Key` with every execute call, reuses it on its own retries and for a double submit, and reloads the state when an earlier attempt's outcome is unknown (#396).
- Query sources and views see the request's correlation (the caller, request id and process id):
  their DI scope started empty, so a source could not filter by caller and a delegated source did not
  forward the request's ids (#413).
- Analyzer packaging and rules, cancellation finalization, SQLite provider, sample handoff authorization,
  client registry cache (#382).

## [1.0.0] - planned

1.0 is the contract freeze ([#241](https://github.com/no1ross/Kaleido/issues/241)): from 1.0 a breaking
change to the public API, the HTTP wire contracts, the error codes or the telemetry names needs a new
major version. Before 1.0.0 is tagged:
- the intentional public API and its compatibility baseline are recorded (#313);
- the remaining pre-1.0 work items of [#284](https://github.com/no1ross/Kaleido/issues/284) are
  resolved or deferred by decision;
- the nuget.org publisher is set up (trusted publishing policy and `NUGET_USER`, [RELEASING.md](RELEASING.md#owner-setup-once-before-the-first-tag); the workflow is in place, #432);
- this section is replaced by the release's entry, listing the changes since the last preview.
