# 0032. The public API at 1.0: reviewed type by type

- **Status:** Accepted
- **Date:** 2026-10-27
- **Related:** #447, #430, ADR 0017, ADR 0029, ADR 0030, ADR 0031

## Context
The `PublicAPI.Unshipped.txt` baselines hold every public member of the seven packages (about 2,350
entries, most of them record-generated members and constants), and the `Shipped` files are empty
until 1.0. Once they move, every entry is a compatibility promise (`docs/VERSIONING.md`). Before the
freeze each public type was reviewed: is a consumer meant to name, implement or call it, and is its
name, namespace and code right?

The changes this review depended on were made first, each in its own pull request: capabilities
lost their `Version` (ADR 0029, #474); options became immutable records, the builders went and
`KaleidoServiceOptions` was sealed (ADR 0030, #445); `IdempotentResult` and `IdempotentResultState`
moved to `Kaleido.Storage` with the fenced store contract (#441); information requests gained
`ExchangeId` (ADR 0031, #442).

## Decision
Every public type is kept, in one of four roles - **contract** (a consumer implements or calls it),
**seam** (a host or provider plugs in through it), **DTO/options** (data a consumer reads or sets),
**constant** (codes, names, tags). Two code strings are renamed so every code carries its area's
prefix; the constants keep their names:

| Constant | Was | Now |
|---|---|---|
| `ProcessorErrorCodes.MissingStepResult` | `missing_step_result` | `pro_missing_step_result` |
| `HttpClientErrorCodes.MissingBaseUrl` | `missing_base_url` | `httpclient_missing_base_url` |

Nothing is internalized: each type below has a consumer-facing use, and the internal seams
(registries, runtimes, execution entry points) were already internal (#297).

**This is a snapshot, kept current until the release.** More changes land before 1.0; a pull
request that adds, removes or renames public API places it in the checklist below (role and
decision) in the same change, so the list matches the baselines when they move to `Shipped` in the
1.0 release pull request.

### `Kaleido` (core, 118 types)
- [x] **Contracts:** `IProcessStep`, `IInformationStep`, `IProcessStepHandler<…>` (both arities),
  the Queryable source and view interfaces (`IQuerySource`, `IQuerySourceAsync`,
  `IDelegatedQuerySource` ×2, `IQueryViewSource` ×2, `IQueryViewSourceAsync` ×2, `IQueryContext`,
  `IQueryParameters`), `IEventPublisher`, `IKaleidoEventRedactor`, `IKaleidoEvent`, the capability
  attributes (`ProcessStep`, `QuerySource`, `QueryView`, `AvailableAfter`/`AvailableUntil`/`DependsOn`,
  `Repeatable`, `Lookup`, `Filterable`/`Searchable`/`Sortable`/`Pageable`, `KaleidoAuthorization`),
  `ProcessStepHandlerResult` (both arities), `ProcessStepContext`, `QueryExecutionContext`,
  `IQueryRequest` (both arities), `KaleidoServiceCollectionExtensions`, `IKaleidoBuilder`. Keep.
- [x] **Base interfaces that must be public:** `ILocalQuerySource<T>` (base of the two local source
  interfaces) and `IProcessStepHandlerResult` (the common shape of both handler results). Keep.
- [x] **Seams:** `IKaleidoContextStore`, `IKaleidoIdempotencyStore`, `IKaleidoOutboxStore`,
  `IKaleidoOutbox`, `IKaleidoTransactions`/`IKaleidoTransaction`,
  `IKaleidoCorrelationContextAccessor`, `IQueryContextExecutor<TView>` (the EF Core execution seam,
  documented in the guide). Keep.
- [x] **DTO/options:** `KaleidoServiceOptions`, `KaleidoEventOptions`, `KaleidoResilienceSettings`,
  `ProcessorContext`/`StepContext`, `IdempotentResult`/`IdempotentResultState`, `KaleidoOutboxEvent`,
  the information-request model (`InformationRequest`, `InformationItem`, `InformationOption`,
  `InformationAnswer`, `InformationCondition`, `InformationResponseItem` and their enums),
  `ProcessMessage`, `MessageType`, the query model (`QueryBody`, filters, `QuerySort`, `QueryPage`,
  `QueryRequest` ×2, `QueryResult<T>`, `EmptyQueryViewParameters`, the operators and `MatchMode`),
  `KaleidoCorrelationContext`, `KaleidoCaller`, `AuthorizationMetadata`, `KaleidoAuthorizationMode`,
  and the audit events with their contexts and envelope (`ProcessCreated`, `PlanBuilt`,
  `PlanBuiltCandidate`, `PlanBuiltCandidateMessage`, `StepCompleted`, `ExecutionCompleted`,
  `QueryExecuted`, `ProcessEventBase`, `IProcessEvent`, `IQueryEvent`, `ProcessEventContext`,
  `QueryableEventContext`, `KaleidoEventEnvelope<,>`). Keep.
- [x] **Enums the audit events carry:** `ExecutionDecisionType`, `StepCandidateStatus`,
  `StepExecutionStatus`, `ProcessExecutionState`, and `StepProcessingMessage` (the runtime messages on
  `StepCompleted`). Keep: an event consumer reads them. `StepExecutionOutcome` is removed: it duplicated
  `StepExecutionStatus`, which the step answer now carries as `status` (#536).
- [x] **Constants:** `ProcessorErrorCodes` (one value renamed; seven codes nothing raised removed,
  #518), `QueryableErrorCodes` (four codes nothing raised removed, #518),
  `ConfigurationErrorCodes`, `FrameworkErrorCodes` (`DataConversionError` removed: nothing raised it,
  #505); every remaining public error code is raised, checked in CI (`tools/docs/check-error-codes.py`), `KaleidoEventTypes`, and the telemetry names
  (`ProcessorTelemetry`, `QueryableTelemetry`, `KaleidoEventTelemetry`, `KaleidoTelemetryTags`) - telemetry
  names are a 1.0 contract (VERSIONING.md). Keep.
- [x] **Exceptions:** `KaleidoValidationException`, `KaleidoAuthorizationException`,
  `KaleidoConcurrencyException`, `KaleidoConfigurationException`, `KaleidoFrameworkException`. Keep.

### `Kaleido.Http.Abstractions` (51 types)
- [x] **Wire contracts** (ADR 0017): the Process, Queryable and Registry request/response records
  and metadata, `KaleidoErrorResponse`/`KaleidoError`. Keep.
- [x] **Contracts:** `IKaleidoProcessorClient`(`Factory`), `IKaleidoQueryableClient`(`Factory`),
  `QueryApiBodyExtensions` (`ToApiBody()` for delegated sources). Keep.
- [x] **Seam:** `IRegistrySnapshotStore` with `KaleidoRegistrySnapshotServiceCollectionExtensions`. Keep.
- [x] **Constants:** `KaleidoErrorCodes`, `KaleidoCorrelationHeaders`, `KaleidoIdempotencyHeaders`,
  `KaleidoJsonOptions`. Keep.

### `Kaleido.Http` (5 types)
- [x] `KaleidoHttpServiceCollectionExtensions`, `KaleidoEndpointRouteBuilderExtensions`,
  `KaleidoHttpOptions`, `KaleidoHttpMapOptions`; `IKaleidoRegistryService` (the in-host,
  caller-filtered registry, used by the MCP adapter and available to hosts). Keep.

### `Kaleido.Http.Client` (11 types)
- [x] `KaleidoHttpClientsServiceCollectionExtensions`, `IKaleidoCallCredentialProvider` and its
  request/credential/kind/purpose types, `KaleidoClientOptions`/`KaleidoClientEntry`/
  `KaleidoResilienceOptions`, `KaleidoHttpClientException`. Keep.
- [x] `HttpClientErrorCodes` - `MissingBaseUrl` renamed to `httpclient_missing_base_url`.

### `Kaleido.Http.Mcp` (5 types)
- [x] `KaleidoMcpServiceCollectionExtensions`, `KaleidoMcpEndpointRouteBuilderExtensions`,
  `KaleidoMcpOptions`, `McpToolListing`, `McpErrorCodes`. Keep.

### `Kaleido.Observability.OpenTelemetry` (7 types)
- [x] `KaleidoObservabilityOpenTelemetryExtensions` (`AddOpenTelemetry`, `AddKaleidoInstrumentation`),
  `KaleidoOpenTelemetryOptions`, `OpenTelemetryErrorCodes`, and the per-area
  `Add*Instrumentation` extensions (`Processor`, `Queryable`, `Mcp`, `Sqlite`) for hosts that build
  their own pipeline. Keep.

### `Kaleido.Provider.SQLite` (4 types)
- [x] `SqliteKaleidoServiceCollectionExtensions`, `SqliteKaleidoContextDbContext` (the host creates
  its schema with it), `ISqliteKaleidoTransaction` (joining the transaction from a host's outbox),
  `SqliteErrorCodes`. Keep.

### Documentation
- [x] Every public type and member carries XML documentation (CS1591 is enforced in `src/`); a
  search for placeholder summaries found none.

## Alternatives considered
- **Internalize the enums only events use.** Event consumers (a warehouse, an audit view) read them;
  internal enums would leave the events' values without a documented type. Rejected.
- **Rename the constants too** (`ProcessorErrorCodes.ProMissingStepResult`). The constant names
  already say their area; only the strings, which callers see, were inconsistent. Not done.

## Consequences
- Breaking (preview): code comparing the two strings changes; code using the constants does not.
- The move of the baselines to `PublicAPI.Shipped.txt` and `EnablePackageValidation` with the 1.0
  baseline happen in the 1.0 release pull request (RELEASING.md), not here.
