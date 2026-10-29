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
- **The repository builds with the .NET 9 SDK** until the .NET 10 SDK is available again (#424); the
  tests run per project (`tools/test/Run-Tests.ps1`). The packages still target `net8.0`.

### Fixed
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
