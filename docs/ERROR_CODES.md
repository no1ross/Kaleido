# Kaleido error codes

Every Kaleido exception carries a machine-readable `Code`. Framework-defined codes listed below are stable across releases and safe to match in clients or log aggregators; handler-supplied domain codes follow the owning application's contract.

> **Analyzer codes** — the `Kaleido` NuGet package ships with a set of Roslyn analyzers (KAL2xxx) that surface misconfiguration at compile time rather than at runtime. See [Analyzer rules](#analyzer-rules-kal2xxx) below.

## Exception types

| Type | Namespace | HTTP result | Code set |
|---|---|---|---|
| `KaleidoValidationException` | `Kaleido.Exceptions` | 400 Bad Request, or a failed Process step outcome | `QueryableErrorCodes` (`qry_*`), `ProcessorErrorCodes` (`pro_*`), or a handler-supplied domain code |
| `KaleidoConfigurationException` | `Kaleido.Exceptions` | 500 Internal Server Error | the owning area's codes: `ConfigurationErrorCodes` (core), `QueryableErrorCodes`, `ProcessorErrorCodes`, `HttpClientErrorCodes`, `SqliteErrorCodes`, `McpErrorCodes`, `OpenTelemetryErrorCodes` |
| `KaleidoFrameworkException` | `Kaleido.Exceptions` | 500 Internal Server Error | `FrameworkErrorCodes` |
| `KaleidoConcurrencyException` | `Kaleido.Exceptions` | 409 Conflict | `ProcessorErrorCodes.ProcessConflict` (`pro_process_conflict`) |
| `KaleidoAuthorizationException` | `Kaleido.Exceptions` | 401 (unauthenticated) or 403 (denied), answered by the host's authentication scheme (challenge / forbid) | none: carries `Capability` and `CallerIsAuthenticated` ([`AUTHORIZATION.md`](./AUTHORIZATION.md)) |
| `KaleidoHttpClientException` | `Kaleido.Http.Client` | — (client-side) | `HttpClientErrorCodes` |

Each area owns its codes; there is no single catalog. Code string values are stable regardless of which class declares them. Every failed Kaleido endpoint answers with the same body, `KaleidoErrorResponse`: `{ "errors": [{ "code", "message" }] }`. The MCP adapter's `mcp_*` codes are returned inside tool results and are listed in the [MCP README](../src/Kaleido.Http.Mcp/README.md#when-a-call-fails).

A request the HTTP layer cannot read (a body that is not JSON, a value that does not fit its type, a C# `required` member left out) is answered 400 `argument_error` (415 for a non-JSON content type) with a message naming the JSON path, by Kaleido's error handling on the per-step and query endpoints; a `JsonException` is never a 500.

HTTP middleware exposes a validation exception's `Code` and `Message` in a 400 error body. A Process step handler's validation exception instead produces a failed step outcome; its original code and message appear in `FrameworkMessages` only when the transport enables framework diagnostics. A configuration or framework exception thrown during a request is logged with its `Code` and `Message` and answered 500 with its `Code` and a generic message ("The service is not configured correctly." / "An internal error occurred."): the detail names internals, so it stays in the log, and the code links the response to the log entry. One thrown at startup stops the host and never reaches a response. A failed span's status description is likewise the error code (or, for an unexpected failure, the exception type), never the message.

---

## `QueryableErrorCodes` — Queryable startup and request validation

One Queryable-owned catalog (`Kaleido.Queryable`), all prefixed `qry_`. Startup registration codes are raised as `KaleidoConfigurationException` at startup (they stop the host and never reach a response); request-validation codes as `KaleidoValidationException` (400 body).

| Constant | Code | Meaning |
|---|---|---|
| `MissingAttribute` | `qry_missing_attribute` | Query source or view missing `[QuerySource]`/`[QueryView]`, or with an empty `DisplayName`/`Description` (startup) |
| `MissingSource` | `qry_missing_source` | Query view references a type that is not a registered local query source (startup) |
| `DuplicateRegistration` | `qry_duplicate_registration` | Two sources share a type name, or two views of one source share a type name (startup) |
| `InvalidRegistration` | `qry_invalid_registration` | Structurally invalid registration, for example `[QuerySource]`/`[QueryView]` on a type that implements no source or view interface (also KAL2009), both sync and async shapes, more than one source/view interface, a type that is both a source and a view, a pageable view or local source without a valid sortable `DefaultSortField`, `[Pageable]` limits that are not both greater than zero with `DefaultSize` no greater than `MaxSize` (#519), or a `[Filterable]` operator that does not apply to the field's type (also KAL2020) (startup) |
| `InvalidField` | `qry_invalid_field` | Field referenced in a filter, sort, or parameter does not exist on the query context |
| `UnsupportedOperator` | `qry_unsupported_operator` | Filter condition uses an operator not supported by the field, or one that is not a filter operator at all (an unknown name or an undefined number, #519) |
| `InvalidLogicalOperator` | `qry_invalid_logical_operator` | A filter group's logical operator is not `and` or `or` (an unknown name or an undefined number, #519) |
| `InvalidSortDirection` | `qry_invalid_sort_direction` | A sort clause's direction is not `ascending` or `descending` (an unknown name or an undefined number, #519) |
| `FieldNotFilterable` | `qry_field_not_filterable` | Filter condition references a field not marked as filterable |
| `FieldNotSortable` | `qry_field_not_sortable` | Sort clause references a field not marked as sortable |
| `InvalidPageSize` | `qry_invalid_page_size` | Requested page size is invalid or exceeds the maximum |
| `InvalidPageOffset` | `qry_invalid_page_offset` | Requested page offset is negative |
| `UnsupportedMatchMode` | `qry_unsupported_match_mode` | Search field uses a match mode not supported by the field |
| `MissingParameter` | `qry_missing_parameter` | A required query parameter is missing, or the parameters record is missing while it has a `[Required]` member (validated before the query runs, for views, local and delegated sources) |
| `FieldNotSearchable` | `qry_field_not_searchable` | Search text provided but no searchable fields are defined |
| `DuplicateSortField` | `qry_duplicate_sort_field` | Same field appears more than once in the sort clause |
| `PagingNotSupported` | `qry_paging_not_supported` | Page request made on a context that does not support paging |
| `InvalidFilterNode` | `qry_invalid_filter_node` | Filter node is structurally invalid |
| `EmptyFilterGroup` | `qry_empty_filter_group` | Filter group contains no child expressions |
| `FilterDepthExceeded` | `qry_filter_depth_exceeded` | Filter expression exceeds the maximum nesting depth |
| `UnsupportedRuntimeType` | `qry_unsupported_runtime_type` | Filter value has a CLR type not supported by the transport layer |
| `MissingFilterField` | `qry_missing_filter_field` | Filter condition is missing its field name |
| `InvalidFilterValue` | `qry_invalid_filter_value` | Filter value cannot be converted to the field's declared type: not that type, an enum value that names no member, or a number the type cannot hold (#519) |
| `InvalidParameterValue` | `qry_invalid_parameter_value` | A query parameter value cannot be converted, or fails one of its DataAnnotations (e.g. `[Range]`) |

---

## `ProcessorErrorCodes` — Process startup and execution

One Processor-owned catalog supplies `pro_*` strings to startup exceptions, planning and step-validation diagnostics, Process events, and opt-in HTTP `FrameworkMessages`. `MessageType` carries severity separately from `Code`. Handler-authored `BusinessMessages` use domain-specific codes; a handler's `KaleidoValidationException.Code` is passed through unchanged rather than replaced by a generic framework code.

Both message collections are present on Process execution responses. `BusinessMessages` contains handler-authored messages; `FrameworkMessages` is empty unless the HTTP host sets `AddHttp(new() { IncludeFrameworkMessages = true })`. A different transport may encode an empty collection differently, but must preserve the same meaning.

| Constant | Code | Meaning |
|---|---|---|
| `MissingAttribute` | `pro_missing_attribute` | `IProcessStep` type missing `[ProcessStep]`, or `[ProcessStep]` with an empty `DisplayName`/`Description`, at startup |
| `MissingHandler` | `pro_missing_handler` | Step has no registered handler |
| `InvalidHandler` | `pro_invalid_handler` | Handler signature is invalid |
| `DuplicateStep` | `pro_duplicate_step` | Two step types share a type name (step names are type names) |
| `InvalidRegistration` | `pro_invalid_registration` | Step registration is structurally invalid (including `[ProcessStep]` on a type that does not implement `IProcessStep`, and an `IInformationStep` declaring properties other than `InformationRequestId` and `Items`) |
| `UnknownStep` | `pro_unknown_step` | Requested step is not registered; on `POST /processes/execute` it rejects the whole request before anything runs (400) |
| `ProcessNotFound` | `pro_process_not_found` | The request names a process that does not exist and is not a handoff from another processor; nothing is started (400, ADR 0009) |
| `ProcessConflict` | `pro_process_conflict` | Another request saved the process since this one loaded it (optimistic concurrency); nothing of this request was saved. Read the process state again and decide on the next step (409, `KaleidoConcurrencyException`) |
| `IdempotencyKeyReused` | `pro_idempotency_key_reused` | An `Idempotency-Key` was sent again with a different request; a key identifies one call - use a new key for a new call (422) |
| `InvalidIdempotencyKey` | `pro_invalid_idempotency_key` | The `Idempotency-Key` header is longer than 200 characters, or shorter than 16 from a caller without an identity, who shares the key space with every other such caller and must send a random key (400) |
| `IdempotencyInProgress` | `pro_idempotency_in_progress` | The first request with this `Idempotency-Key` is still running, so this one did not run; retry with the same key after `Retry-After` to get its answer (409) |
| `IdempotencyOutcomeUnknown` | `pro_idempotency_outcome_unknown` | The first request with this `Idempotency-Key` did not finish with an answer - it never finished within its lease (e.g. the service stopped), or failed after its step may have run (a 5xx, an unexpected error, a cancellation) - so it may or may not have run and is not run again with this key until `IdempotencyUnknownOutcomeRetention` passes; read the process state and decide, or continue with a new key (409) |
| `InvalidRequest` | `pro_invalid_request` | An execute request names a step more than once (400), or a step reached the runtime as something other than its step type |
| `ValidationFailed` | `pro_validation_failed` | Generic, custom, or object-level step validation failed |
| `Required` | `pro_required` | Required step field is missing |
| `InvalidLength` | `pro_invalid_length` | Step field violates a length constraint |
| `OutOfRange` | `pro_out_of_range` | Step field violates a range constraint |
| `InvalidFormat` | `pro_invalid_format` | Step field violates a format constraint |
| `AlreadyProcessed` | `pro_already_processed` | Previously completed step needs no further execution |
| `DependencyNotSatisfied` | `pro_dependency_not_satisfied` | Required step dependency is incomplete |
| `InvalidRequiredStep` | `pro_invalid_required_step` | Required next step is invalid |
| `RequiredStepNotAllowed` | `pro_required_step_not_allowed` | Required next step cannot be executed (not a legal next step, or not a registered step in this processor) |
| `ProcessEnded` | `pro_process_ended` | The process has ended here (complete, business failure, handed off): no step of this service runs on it; a hand-back from the service holding it excepted ([ADR 0033](decisions/0033-process-lifecycle-and-admission.md)) |
| `OutcomePendingRecovery` | `pro_outcome_pending_recovery` | 503: the step ran, but the process could not be saved; its outcome is kept and saved when the store is available again. Do not send the step again ([ADR 0034](decisions/0034-unsaved-step-outcomes.md)) |
| `OutcomeNotSaved` | `pro_outcome_not_saved` | 500: the step ran, but its outcome was not saved and could not be kept; or 409: another request changed the process meanwhile. Do not send the step again; the process must be reconciled ([ADR 0034](decisions/0034-unsaved-step-outcomes.md)) |
| `NotPermitted` | `pro_not_permitted` | A **notice**, not an error: the process continues with steps the caller may not run (`requiredStep` null or `availableSteps` empty), or a step input's lookup names a query the caller may not use (`lookup` left out). Nothing is named ([AUTHORIZATION.md](AUTHORIZATION.md), #524) |
| `ProcessRecovering` | `pro_process_recovering` | 409: an earlier step's outcome is still being saved; nothing was run. Try again shortly ([ADR 0034](decisions/0034-unsaved-step-outcomes.md)) |
| `InvalidHandOff` | `pro_invalid_hand_off` | A handler handed the process off to the service it runs in; a process violation, so the process keeps its state |
| `ExecutionCanceled` | `pro_execution_canceled` | Step execution was cancelled |
| `FrameworkException` | `pro_framework_exception` | Unexpected framework error interrupted a step |
| `RepeatableStep` | `pro_repeatable_step` | Repeatable step remains eligible after prior execution |
| `MissingStepResult` | `pro_missing_step_result` | A process execution result had no entry for the executed step (raised as `KaleidoFrameworkException`) |
| `StepNotAvailable` | `pro_step_not_available` | The submitted step is not a next step: not the pending required step, or not available under the process rules (the initial steps for a new process) |
| `InformationRequestInvalid` | `pro_information_request_invalid` | A handler returned an information request that is not well-formed (no id or items, duplicate item ids, an item without text, a choice without options, a group without items, nesting outside a group, an `enableWhen` condition that does not test an earlier question or whose operator or value does not fit it) |
| `InformationRequestMissing` | `pro_information_request_missing` | An information step was required without an information request (use `RequireInformation<TNext>`) |
| `InformationResponseStale` | `pro_information_response_stale` | Answers carry the `exchangeId` of an earlier time the questions were put, not of the pending request (a delayed answer, a stale form, a replayed answer - perhaps to the same questionnaire with the same item ids); rejected before any handler runs. Answer the current questions (#442) |
| `InformationResponseMismatch` | `pro_information_response_mismatch` | Answers were submitted for a request that is not pending: another `informationRequestId`, another step, or nothing was asked |
| `InformationResponseUnanswered` | `pro_information_response_unanswered` | A question that applies has no answer (every question enabled by the answers must be answered; ADR 0024) |
| `InformationResponseInvalidAnswer` | `pro_information_response_invalid_answer` | An answer does not fit its question: unknown item, wrong value for the item type, a choice that was not offered, several answers to a question that does not repeat, or an answer to a question that does not apply (its `enableWhen` does not hold) |

Information-request validation is structural only; see [`INFORMATION_REQUESTS.md`](./INFORMATION_REQUESTS.md#validation).

---

## `ConfigurationErrorCodes` — 500 (startup / DI misconfiguration)

Core service-setup and authorization-settings codes. Area startup codes live with their area (`QueryableErrorCodes`, `ProcessorErrorCodes`, `HttpClientErrorCodes`, `SqliteErrorCodes`).

| Constant | Code | Meaning |
|---|---|---|
| `InvalidServiceName` | `invalid_service_name` | `ServiceName` is null, empty, or invalid |
| `MissingAssembly` | `missing_assembly` | `AddKaleido()` received a null or empty explicit `Assemblies` list; fails before DI registration |
| `ConflictingAuthorization` | `conflicting_authorization` | `[KaleidoAuthorization]` declares `AllowAnonymous` together with `Roles`/`Policy` |
| `AuthenticationNotConfigured` | `authentication_not_configured` | `AuthorizationMode` enforces authorization (`Authenticated` is the default) but no authentication scheme is registered: add host authentication, or set `AuthorizationMode = None` explicitly |
| `InvalidAuthorizationMode` | `invalid_authorization_mode` | `AuthorizationMode` is not a defined value |
| `InvalidEventOptions` | `invalid_event_options` | a `Kaleido:Events` setting is out of range (e.g. a non-positive `BatchSize` or `Resilience:AttemptTimeout`) |
| `InvalidOptions` | `invalid_options` | A configured value in the `Kaleido` section cannot be read as its setting's type (e.g. text where a number or a duration is expected, or an unknown enum name); startup, ADR 0030 |
| `InvalidHttpOptions` | `invalid_http_options` | An HTTP transport setting (`Kaleido:Http`, `Kaleido:Http:Map`, or the options passed to `AddHttp`/`MapKaleidoHttp`) is out of range or cannot be read, e.g. a non-positive idempotency lease (startup) |
| `DuplicateRegistration` | `duplicate_registration` | `AddKaleido()` or `AddHttp()` was called a second time on the same service collection; a host runs one Kaleido service, configured in one call of each |
| `ConflictingOutbox` | `conflicting_outbox` | Two outbox choices were registered: a service has one - Kaleido's built-in outbox (with one `AddOutboxStore` storage), the host's own (`AddOutbox`) or `AddDirectPublication` ([ADR 0023](decisions/0023-outbox-integration.md)) |
| `ConflictingStore` | `conflicting_store` | Two different implementations of one store or of the transactions were registered (e.g. `AddSqliteContextStore` and `AddContextStore<T>()`) |
| `StoresNotChosen` | `stores_not_chosen` | When the host starts: a store the service uses is still the in-memory default (process state or idempotency records with process steps; the outbox store with a publisher on Kaleido's outbox). Register durable stores, or call `AddInMemoryStores()` ([ADR 0028](decisions/0028-stores-are-chosen-explicitly.md)) |

---

## `FrameworkErrorCodes` — 500 (internal integrity violations)

These indicate a framework bug or broken DI wiring, not a user error.

| Constant | Code | Meaning |
|---|---|---|
| `ReflectionError` | `reflection_error` | Required method or property not found via reflection |
| `TypeMismatch` | `type_mismatch` | Resolved type does not match the expected type |
| `MissingRegistration` | `missing_registration` | Required entry not found in an internal registry |
| `InvalidHandlerResult` | `invalid_handler_result` | Handler returned an unexpected or null result |
| `UnsupportedDataType` | `unsupported_data_type` | A published input or result is a dictionary whose key type is not `string` (keys become JSON property names); raised at registration as a `KaleidoConfigurationException` (#446). Other unknown CLR types are described as generic objects |

---

## `KaleidoErrorCodes` - shared HTTP error-body codes

Declared in `Kaleido.Http.Abstractions` (`Kaleido.Http` namespace), next to the error body `KaleidoErrorResponse` (ADR 0017).

| Constant | Code | Meaning |
|---|---|---|
| `ArgumentError` | `argument_error` | 400 (415 for a non-JSON content type): a body Kaleido cannot read - malformed JSON, a value that does not fit its type, a `required` member left out (on `POST /processes/execute` the message names the step and the JSON path, and nothing runs), a malformed correlation header such as a non-GUID `X-Kaleido-Process-Id` |
| `FrameworkError` | `framework_error` | 500 for an unexpected failure; the message is generic and never carries the exception's detail |

---

## `HttpClientErrorCodes` — client-side (never an HTTP response)

Every client code is prefixed `httpclient_`, including its startup configuration codes (`httpclient_missing_base_url`, `httpclient_invalid_route_prefix`, `httpclient_invalid_options`).

| Constant | Code | Meaning |
|---|---|---|
| `EmptyResponse` | `httpclient_empty_response` | Remote request succeeded but returned no payload |
| `RequestFailed` | `httpclient_request_failed` | Remote request failed with a non-success HTTP status (including a step, source or view the service does not have, 404, or the caller may not use, 403); the service's own errors are in `Errors` when its body carried any, whatever the status |
| `ValidationFailed` | `httpclient_validation_failed` | Remote request failed with structured validation errors (400 with a Kaleido error body) |
| `Conflict` | `httpclient_conflict` | The remote process was changed by another request (409, `pro_process_conflict` in `Errors`); nothing was saved - read the state again instead of repeating the call |
| `MissingBaseUrl` | `httpclient_missing_base_url` | A configured client has no `BaseUrl` (startup, `KaleidoConfigurationException`) |
| `InvalidRoutePrefix` | `httpclient_invalid_route_prefix` | A configured client's `RoutePrefix` (or, without one, its lowercased name) is not a service name (`[a-z0-9][a-z0-9_-]*`); it must equal the downstream `ServiceName` (startup, `AddHttpClients()`, `KaleidoConfigurationException`; ADR 0027) |
| `InvalidClientOptions` | `httpclient_invalid_options` | A client setting in configuration (`Kaleido:RegistryTtl`, `Kaleido:Resilience`, `Kaleido:Clients:{name}`...) is out of range or cannot be read, e.g. a negative registry TTL or a non-positive timeout (startup, `KaleidoConfigurationException`) |
| `ConfiguredCredentialForCaller` | `httpclient_configured_credential_for_caller` | An `IKaleidoCallCredentialProvider` answered `Configured` for a call made for a caller: a caller's call never goes out with the host's own credentials (`KaleidoConfigurationException`, at the call; ADR 0022) |
| `UnknownClient` | `httpclient_unknown_client` | `GetClient` was asked for a name not configured under `Kaleido:Clients`; the message lists the configured clients (`KaleidoConfigurationException`, at the call) |

---

## `SqliteErrorCodes` — SQLite provider (`Kaleido.Provider.SQLite`)

The reference provider owns its own code; core knows nothing about it.

| Constant | Code | Meaning |
|---|---|---|
| `InvalidConnectionString` | `invalid_connection_string` | A SQLite store's connection string cannot be parsed at registration |
| `ConnectionStringMismatch` | `connection_string_mismatch` | `AddSqliteContextStore`, `AddSqliteOutboxStore` and `AddSqliteIdempotencyStore` were given different connection strings; they share one database |

---

## `OpenTelemetryErrorCodes` - OpenTelemetry provider (`Kaleido.Observability.OpenTelemetry`)

| Constant | Code | Meaning |
|---|---|---|
| `InvalidOptions` | `otel_invalid_options` | A `Kaleido:OpenTelemetry` setting cannot be used (or read): a protocol other than `grpc`/`http/protobuf`, or a malformed `key=value` list (startup, `KaleidoConfigurationException`) |

---

## `McpErrorCodes` - MCP adapter (`Kaleido.Http.Mcp`)

Startup codes surface through `KaleidoConfigurationException`. At request time a tool-call problem is a tool result with `isError: true` and one of these codes, never an HTTP or protocol error; what the agent does with each is in [the adapter's README](../src/Kaleido.Http.Mcp/README.md).

| Constant | Code | Meaning |
|---|---|---|
| `MissingHttp` | `mcp_missing_http` | `AddMcp()` needs `AddHttp()` (the registry) on the same Kaleido builder (startup) |
| `MissingClients` | `mcp_missing_clients` | `AddMcp()` needs Kaleido clients (`Kaleido:Clients` + `AddHttpClients()`) to call services (startup) |
| `MissingCallCredentials` | `mcp_call_credentials_not_configured` | `AddMcp()` needs to know how tool calls are authorized: register `ForwardCallerToken()` or `AddCallCredentials<TProvider>()` before it (startup, ADR 0022) |
| `MissingInstructionsFile` | `mcp_missing_instructions_file` | `Kaleido:Mcp:InstructionsFile` points at a file that does not exist (startup) |
| `InvalidOptions` | `mcp_invalid_options` | A `Kaleido:Mcp` setting is blank, repeated, conflicting, out of range or unreadable (startup; nothing is clamped or replaced) |
| `UnknownTool` | `mcp_unknown_tool` | The tool is not (or no longer) offered to this caller |
| `InvalidArguments` | `mcp_invalid_arguments` | The tool arguments are missing a required value or have the wrong shape |
| `ServiceError` | `mcp_service_error` | The Kaleido service refused or failed the call (its status and errors are included) |
| `ProcessConflict` | `mcp_process_conflict` | The process was changed by another call (409); nothing was saved. Read the state again and decide with the user |
| `ServiceUnavailable` | `mcp_service_unavailable` | The Kaleido service could not be reached, or no Kaleido client is configured for it |
| `InternalError` | `mcp_internal_error` | The adapter failed unexpectedly while running a tool; the tool did not complete |
| `NoPendingInformationRequest` | `mcp_no_pending_information_request` | An answer was submitted but the process has no pending information request |
| `ConfirmationRequired` | `mcp_confirmation_required` | The step needs the user's confirmation and the client cannot show a form; nothing was run |

---

## Analyzer rules (KAL2xxx)

The `Kaleido` package bundles compile-time checks (`KAL2xxx`) that catch most startup registration failures above in consumer code before it runs. They are build diagnostics, not runtime codes; see [consumer rules in `ANALYZERS.md`](./ANALYZERS.md#consumer-rules--kal2xxx) for the rules, severities and the runtime code each one mirrors.

Aggregate registries (`MapKaleidoHttp(new() { AggregateRegistry = true })`) require `AddHttpClients()` — enforced at map time by a `KaleidoConfigurationException`, so no analyzer rule covers it.
