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
