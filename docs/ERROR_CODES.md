# Kaleido error codes

Every Kaleido exception carries a machine-readable `Code`. Framework-defined codes listed below are stable across releases and safe to match in clients or log aggregators; handler-supplied domain codes follow the owning application's contract.

> **Analyzer codes** — the `Kaleido` NuGet package ships with a set of Roslyn analyzers (KAL2xxx) that surface misconfiguration at compile time rather than at runtime. See [Analyzer rules](#analyzer-rules-kal2xxx) below.

## Exception types

| Type | Namespace | HTTP result | Code set |
|---|---|---|---|
| `KaleidoValidationException` | `Kaleido.Exceptions` | 400 Bad Request, or a failed Process step outcome | `QueryableErrorCodes` (`qry_*`), `ProcessorErrorCodes` (`pro_*`), or a handler-supplied domain code |
| `KaleidoConfigurationException` | `Kaleido.Exceptions` | 500 Internal Server Error | the owning area's codes: `ConfigurationErrorCodes` (core), `QueryableErrorCodes`, `ProcessorErrorCodes`, `HttpClientErrorCodes`, `SqliteErrorCodes` |
| `KaleidoFrameworkException` | `Kaleido.Exceptions` | 500 Internal Server Error | `FrameworkErrorCodes` |
| `KaleidoHttpClientException` | `Kaleido.Http.Client` | — (client-side) | `HttpClientErrorCodes` |

Each area owns its codes; there is no single catalog. Code string values are stable regardless of which class declares them.

HTTP middleware exposes a validation exception's `Code` and `Message` in a 400 error body. A Process step handler's validation exception instead produces a failed step outcome; its original code and message appear in `FrameworkMessages` only when the transport enables framework diagnostics. Configuration and framework exception codes remain log-only.

---

## `QueryableErrorCodes` — Queryable startup and request validation

One Queryable-owned catalog (`Kaleido.Queryable`), all prefixed `qry_`. Startup registration codes are raised as `KaleidoConfigurationException` (log-only); request-validation codes as `KaleidoValidationException` (400 body).

| Constant | Code | Meaning |
|---|---|---|
| `MissingAttribute` | `qry_missing_attribute` | Query source or view missing `[QuerySource]`/`[QueryView]`, or with an empty `Version`/`DisplayName`/`Description` (startup) |
| `MissingSource` | `qry_missing_source` | Query view references a type that is not a registered local query source (startup) |
| `DuplicateRegistration` | `qry_duplicate_registration` | Two sources share a type name, or two views of one source share a type name (startup) |
| `InvalidRegistration` | `qry_invalid_registration` | Structurally invalid registration, for example both sync and async shapes, more than one source/view interface, a type that is both a source and a view, or a bad default sort field (startup) |
| `InvalidField` | `qry_invalid_field` | Field referenced in a filter, sort, or parameter does not exist on the query context |
| `UnsupportedOperator` | `qry_unsupported_operator` | Filter condition uses an operator not supported by the field |
| `FieldNotFilterable` | `qry_field_not_filterable` | Filter condition references a field not marked as filterable |
| `FieldNotSortable` | `qry_field_not_sortable` | Sort clause references a field not marked as sortable |
| `InvalidPageSize` | `qry_invalid_page_size` | Requested page size is invalid or exceeds the maximum |
| `InvalidPageOffset` | `qry_invalid_page_offset` | Requested page offset is negative |
| `UnsupportedMatchMode` | `qry_unsupported_match_mode` | Search field uses a match mode not supported by the field |
| `MissingParameter` | `qry_missing_parameter` | Required named query parameter is missing |
| `InvalidParameterType` | `qry_invalid_parameter_type` | Named query parameter value has an incompatible type |
| `FieldNotSearchable` | `qry_field_not_searchable` | Search text provided but no searchable fields are defined |
| `DuplicateSortField` | `qry_duplicate_sort_field` | Same field appears more than once in the sort clause |
| `PagingNotSupported` | `qry_paging_not_supported` | Page request made on a context that does not support paging |
| `InvalidFilterNode` | `qry_invalid_filter_node` | Filter node is structurally invalid |
| `InvalidSearchNode` | `qry_invalid_search_node` | Search node is structurally invalid |
| `EmptyFilterGroup` | `qry_empty_filter_group` | Filter group contains no child expressions |
| `FilterDepthExceeded` | `qry_filter_depth_exceeded` | Filter expression exceeds the maximum nesting depth |
| `EmptySearchGroup` | `qry_empty_search_group` | Search group contains no child expressions |
| `UnsupportedRuntimeType` | `qry_unsupported_runtime_type` | Filter value has a CLR type not supported by the transport layer |
| `MissingFilterField` | `qry_missing_filter_field` | Filter condition is missing its field name |
| `MissingSearchText` | `qry_missing_search_text` | Search request is missing the required search text |
| `InvalidFilterValue` | `qry_invalid_filter_value` | Filter value cannot be converted to the field's declared type |
| `InvalidParameterValue` | `qry_invalid_parameter_value` | Named query parameter value cannot be converted |

---

## `ProcessorErrorCodes` — Process startup and execution

One Processor-owned catalog supplies `pro_*` strings to startup exceptions, planning and step-validation diagnostics, Process events, and opt-in HTTP `FrameworkMessages`. `MessageType` carries severity separately from `Code`. Handler-authored `BusinessMessages` use domain-specific codes; a handler's `KaleidoValidationException.Code` is passed through unchanged rather than replaced by a generic framework code.

Both message collections are present on Process execution responses. `BusinessMessages` contains handler-authored messages; `FrameworkMessages` is empty unless the HTTP host sets `AddHttp(o => o.IncludeFrameworkMessages = true)`. A different transport may encode an empty collection differently, but must preserve the same meaning.

| Constant | Code | Meaning |
|---|---|---|
| `MissingAttribute` | `pro_missing_attribute` | `IProcessStep` type missing `[ProcessStep]`, or `[ProcessStep]` with an empty `Version`/`DisplayName`/`Description`, at startup |
| `MissingHandler` | `pro_missing_handler` | Step has no registered handler |
| `InvalidHandler` | `pro_invalid_handler` | Handler signature is invalid |
| `DuplicateStep` | `pro_duplicate_step` | Two step types share a type name (step names are type names) |
| `InvalidRegistration` | `pro_invalid_registration` | Step registration is structurally invalid (including `[ProcessStep]` on a type that does not implement `IProcessStep`, and an `IInformationStep` declaring properties other than `InformationRequestId` and `Items`) |
| `UnknownStep` | `pro_unknown_step` | Requested step is not registered |
| `InvalidRequest` | `pro_invalid_request` | Step request cannot be hydrated or processed |
| `PropertyNotFound` | `pro_property_not_found` | Requested step property was not found |
| `ConversionFailed` | `pro_conversion_failed` | Request value could not be converted |
| `ValidationFailed` | `pro_validation_failed` | Generic, custom, or object-level step validation failed |
| `Required` | `pro_required` | Required step field is missing |
| `InvalidLength` | `pro_invalid_length` | Step field violates a length constraint |
| `OutOfRange` | `pro_out_of_range` | Step field violates a range constraint |
| `InvalidFormat` | `pro_invalid_format` | Step field violates a format constraint |
| `AlreadyProcessed` | `pro_already_processed` | Previously completed step needs no further execution |
| `ConsistencyViolation` | `pro_consistency_violation` | Candidate violates an execution consistency rule |
| `DependencyNotSatisfied` | `pro_dependency_not_satisfied` | Required step dependency is incomplete |
| `DependencySatisfied` | `pro_dependency_satisfied` | Required step dependency is complete |
| `HandlerExecutionFailed` | `pro_handler_execution_failed` | Step handler execution failed |
| `ExceptionThrown` | `pro_exception_thrown` | Exception interrupted step processing |
| `InvalidRequiredStep` | `pro_invalid_required_step` | Required next step is invalid |
| `RequiredStepNotAllowed` | `pro_required_step_not_allowed` | Required next step cannot be executed (not a legal next step, or not a registered step in this processor) |
| `ExecutionCanceled` | `pro_execution_canceled` | Step execution was cancelled |
| `FrameworkException` | `pro_framework_exception` | Unexpected framework error interrupted a step |
| `ProcessMessage` | `pro_process_message` | Process diagnostic message was produced |
| `RepeatableStep` | `pro_repeatable_step` | Repeatable step remains eligible after prior execution |
| `MissingStepResult` | `missing_step_result` | A process execution result had no entry for the executed step (raised as `KaleidoFrameworkException`) |
| `StepNotAvailable` | `pro_step_not_available` | The submitted step is not a next step: not the pending required step, or not available under the process rules (the initial steps for a new process) |
| `InformationRequestInvalid` | `pro_information_request_invalid` | A handler returned an information request that is not well-formed (no id or items, duplicate item ids, an item without text, a choice without options, a group without items, nesting outside a group) |
| `InformationRequestMissing` | `pro_information_request_missing` | An information step was required without an information request (use `RequireInformation<TNext>`) |
| `InformationResponseMismatch` | `pro_information_response_mismatch` | Answers were submitted for a request that is not pending: another `informationRequestId`, another step, or nothing was asked |
| `InformationResponseUnanswered` | `pro_information_response_unanswered` | A presented question has no answer (every question must be answered) |
| `InformationResponseInvalidAnswer` | `pro_information_response_invalid_answer` | An answer does not fit its question: unknown item, wrong value for the item type, a choice that was not offered, or several answers to a question that does not repeat |

Information-request validation is structural only; see [`INFORMATION_REQUESTS.md`](./INFORMATION_REQUESTS.md#validation).

---

## `ConfigurationErrorCodes` — 500 (startup / DI misconfiguration)

Core service-setup and authorization-settings codes. Area startup codes live with their area (`QueryableErrorCodes`, `ProcessorErrorCodes`, `HttpClientErrorCodes`, `SqliteErrorCodes`).

| Constant | Code | Meaning |
|---|---|---|
| `InvalidServiceName` | `invalid_service_name` | `ServiceName` is null, empty, or invalid |
| `MissingAssembly` | `missing_assembly` | `AddKaleido()` received a null or empty explicit `Assemblies` list; fails before DI registration |
| `ConflictingAuthorization` | `conflicting_authorization` | `[KaleidoAuthorization]` declares `AllowAnonymous` together with `Roles`/`Policy` |
| `AuthenticationNotConfigured` | `authentication_not_configured` | `AuthorizationMode` enforces authorization but no authentication scheme is registered |
| `InvalidAuthorizationMode` | `invalid_authorization_mode` | `AuthorizationMode` is not a defined value |

---

## `FrameworkErrorCodes` — 500 (internal integrity violations)

These indicate a framework bug or broken DI wiring, not a user error.

| Constant | Code | Meaning |
|---|---|---|
| `ReflectionError` | `reflection_error` | Required method or property not found via reflection |
| `TypeMismatch` | `type_mismatch` | Resolved type does not match the expected type |
| `MissingRegistration` | `missing_registration` | Required entry not found in an internal registry |
| `InvalidHandlerResult` | `invalid_handler_result` | Handler returned an unexpected or null result |
| `UnsupportedDataType` | `unsupported_data_type` | `DataTypeMapper` does not support the CLR type |
| `DataConversionError` | `data_conversion_error` | `DataTypeMapper` failed to convert a value |

---

## `HttpClientErrorCodes` — client-side (never an HTTP response)

Runtime client codes are prefixed `httpclient_`; `missing_base_url` is a startup configuration code owned by the client.

| Constant | Code | Meaning |
|---|---|---|
| `NotFound` | `httpclient_not_found` | Context, view, or step not found in remote registry |
| `EmptyResponse` | `httpclient_empty_response` | Remote request succeeded but returned no payload |
| `RequestFailed` | `httpclient_request_failed` | Remote request failed with a non-success HTTP status |
| `ValidationFailed` | `httpclient_validation_failed` | Remote request failed with structured validation errors |
| `InvalidRegistryUrl` | `httpclient_invalid_registry_url` | A URL returned by a remote registry was not a valid absolute http(s) URL |
| `MissingBaseUrl` | `missing_base_url` | A configured client has no `BaseUrl` (startup, `KaleidoConfigurationException`) |

---

## `SqliteErrorCodes` — SQLite provider (`Kaleido.Provider.SQLite`)

The reference provider owns its own code; core knows nothing about it.

| Constant | Code | Meaning |
|---|---|---|
| `InvalidConnectionString` | `invalid_connection_string` | Process context-store connection string cannot be parsed at registration |

---

## Analyzer rules (KAL2xxx)

The `Kaleido` package bundles compile-time checks (`KAL2xxx`) that catch most startup registration failures above in consumer code before it runs. They are build diagnostics, not runtime codes; see [consumer rules in `ANALYZERS.md`](./ANALYZERS.md#consumer-rules--kal2xxx) for the rules, severities and the runtime code each one mirrors.

Aggregate registries (`MapKaleidoHttp(o => o.AggregateRegistry = true)`) require `AddHttpClients()` — enforced at map time by a `KaleidoConfigurationException`, so no analyzer rule covers it.
