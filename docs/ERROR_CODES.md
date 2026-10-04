# Kaleido error codes

Every Kaleido exception carries a machine-readable `Code`. Framework-defined codes listed below are stable across releases and safe to match in clients or log aggregators; handler-supplied domain codes follow the owning application's contract.

> **Analyzer codes** — the `Kaleido` NuGet package ships with a set of Roslyn analyzers (KAL2xxx) that surface misconfiguration at compile time rather than at runtime. See [Analyzer rules](#analyzer-rules-kal2xxx) below.

## Exception types

| Type | Namespace | HTTP result | Code set |
|---|---|---|---|
| `KaleidoValidationException` | `Kaleido.Exceptions` | 400 Bad Request, or a failed Process step outcome | `ValidationErrorCodes` (`qry_*`), `ProcessorErrorCodes` (`pro_*`), or a handler-supplied domain code |
| `KaleidoConfigurationException` | `Kaleido.Exceptions` | 500 Internal Server Error | `ConfigurationErrorCodes` (generic/`qry_*`) or `ProcessorErrorCodes` (`pro_*`) |
| `KaleidoFrameworkException` | `Kaleido.Exceptions` | 500 Internal Server Error | `FrameworkErrorCodes` |
| `KaleidoHttpClientException` | `Kaleido.Http.Client` | — (client-side) | `HttpClientErrorCodes` |

HTTP middleware exposes a validation exception's `Code` and `Message` in a 400 error body. A Process step handler's validation exception instead produces a failed step outcome; its original code and message appear in `FrameworkMessages` only when the transport enables framework diagnostics. Configuration and framework exception codes remain log-only.

---

## `ValidationErrorCodes` — 400 Bad Request

Queryable request-validation codes are prefixed `qry_`. Process-specific codes are owned by `ProcessorErrorCodes` below; the wider Queryable catalog consolidation is tracked in [Issue #171](https://github.com/no1ross/Kaleido/issues/171).

| Constant | Code | Meaning |
|---|---|---|
| `QryInvalidField` | `qry_invalid_field` | Field referenced in a filter, sort, or parameter does not exist on the query context |
| `QryUnsupportedOperator` | `qry_unsupported_operator` | Filter condition uses an operator not supported by the field |
| `QryFieldNotFilterable` | `qry_field_not_filterable` | Filter condition references a field not marked as filterable |
| `QryFieldNotSortable` | `qry_field_not_sortable` | Sort clause references a field not marked as sortable |
| `QryInvalidPageSize` | `qry_invalid_page_size` | Requested page size is invalid or exceeds the maximum |
| `QryInvalidPageOffset` | `qry_invalid_page_offset` | Requested page offset is negative |
| `QryUnsupportedMatchMode` | `qry_unsupported_match_mode` | Search field uses a match mode not supported by the field |
| `QryMissingParameter` | `qry_missing_parameter` | Required named query parameter is missing |
| `QryInvalidParameterType` | `qry_invalid_parameter_type` | Named query parameter value has an incompatible type |
| `QryFieldNotSearchable` | `qry_field_not_searchable` | Search text provided but no searchable fields are defined |
| `QryDuplicateSortField` | `qry_duplicate_sort_field` | Same field appears more than once in the sort clause |
| `QryPagingNotSupported` | `qry_paging_not_supported` | Page request made on a context that does not support paging |
| `QryInvalidFilterNode` | `qry_invalid_filter_node` | Filter node is structurally invalid |
| `QryInvalidSearchNode` | `qry_invalid_search_node` | Search node is structurally invalid |
| `QryEmptyFilterGroup` | `qry_empty_filter_group` | Filter group contains no child expressions |
| `QryFilterDepthExceeded` | `qry_filter_depth_exceeded` | Filter expression exceeds the maximum nesting depth |
| `QryEmptySearchGroup` | `qry_empty_search_group` | Search group contains no child expressions |
| `QryUnsupportedRuntimeType` | `qry_unsupported_runtime_type` | Filter value has a CLR type not supported by the transport layer |
| `QryMissingFilterField` | `qry_missing_filter_field` | Filter condition is missing its field name |
| `QryMissingSearchText` | `qry_missing_search_text` | Search request is missing the required search text |
| `QryInvalidFilterValue` | `qry_invalid_filter_value` | Filter value cannot be converted to the field's declared type |
| `QryInvalidParameterValue` | `qry_invalid_parameter_value` | Named query parameter value cannot be converted |

---

## `ProcessorErrorCodes` — Process startup and execution

One Processor-owned catalog supplies `pro_*` strings to startup exceptions, planning and step-validation diagnostics, Process events, and opt-in HTTP `FrameworkMessages`. `MessageType` carries severity separately from `Code`. Handler-authored `BusinessMessages` use domain-specific codes; a handler's `KaleidoValidationException.Code` is passed through unchanged rather than replaced by a generic framework code.

Both message collections are present on Process execution responses. `BusinessMessages` contains handler-authored messages; `FrameworkMessages` is empty unless the HTTP host sets `AddHttp(o => o.IncludeFrameworkMessages = true)`. A different transport may encode an empty collection differently, but must preserve the same meaning.

| Constant | Code | Meaning |
|---|---|---|
| `MissingAttribute` | `pro_missing_attribute` | Step type missing `[ProcessStep]` at startup |
| `MissingHandler` | `pro_missing_handler` | Step has no registered handler |
| `InvalidHandler` | `pro_invalid_handler` | Handler signature is invalid |
| `DuplicateStep` | `pro_duplicate_step` | Registered step names are duplicated |
| `InvalidRegistration` | `pro_invalid_registration` | Step registration is structurally invalid |
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
| `RequiredStepNotAllowed` | `pro_required_step_not_allowed` | Required next step cannot be executed |
| `ExecutionCanceled` | `pro_execution_canceled` | Step execution was cancelled |
| `FrameworkException` | `pro_framework_exception` | Unexpected framework error interrupted a step |
| `ProcessMessage` | `pro_process_message` | Process diagnostic message was produced |
| `RepeatableStep` | `pro_repeatable_step` | Repeatable step remains eligible after prior execution |

---

## `ConfigurationErrorCodes` — 500 (startup / DI misconfiguration)

Cross-cutting codes have no prefix. Queryable startup codes use `qry_`; Process startup codes are in `ProcessorErrorCodes` above.

| Constant | Code | Meaning |
|---|---|---|
| `InvalidServiceName` | `invalid_service_name` | `ServiceName` is null, empty, or invalid |
| `MissingAssembly` | `missing_assembly` | `AddKaleido()` received a null or empty explicit `Assemblies` list; fails before DI registration |
| `InvalidConnectionString` | `invalid_connection_string` | SQLite process context-store connection string cannot be parsed at registration |
| `QryMissingAttribute` | `qry_missing_attribute` | Context or view missing `[QueryContext]`/`[QueryView]` |
| `QryMissingSource` | `qry_missing_source` | Context has no registered source |
| `QryDuplicateSource` | `qry_duplicate_source` | Context has multiple registered sources |
| `QryDuplicateRegistration` | `qry_duplicate_registration` | Duplicate context or view names |
| `QryInvalidRegistration` | `qry_invalid_registration` | Structurally invalid registration |

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

All client codes are prefixed `httpclient_`.

| Constant | Code | Meaning |
|---|---|---|
| `NotFound` | `httpclient_not_found` | Context, view, or step not found in remote registry |
| `EmptyResponse` | `httpclient_empty_response` | Remote request succeeded but returned no payload |
| `RequestFailed` | `httpclient_request_failed` | Remote request failed with a non-success HTTP status |
| `ValidationFailed` | `httpclient_validation_failed` | Remote request failed with structured validation errors |

---

## Analyzer rules (KAL2xxx)

The `Kaleido` NuGet package bundles `Kaleido.Analyzers` — a Roslyn analyzer that surfaces framework misuse at **compile time** rather than at runtime startup. Errors (KAL2001–KAL2003) prevent compilation; warnings and infos (KAL2004–KAL2009) are advisory.

### Attribute validity (KAL2001–KAL2003)

| Rule | Severity | Trigger |
|---|---|---|
| `KAL2001` | Error | `[ProcessStep]` has an empty `Name` or `Version` |
| `KAL2002` | Error | `[QueryContext]` has an empty `Name` or `Version` |
| `KAL2003` | Error | `[QueryView]` has an empty `Name` or `Version` |

These are compile-time equivalents of the `ProcessorErrorCodes.MissingAttribute` / `ConfigurationErrorCodes.QryMissingAttribute` runtime errors. Catching them at compile time prevents the process from failing at startup.

### Handler and step conventions (KAL2004, KAL2007, KAL2008)

| Rule | Severity | Trigger |
|---|---|---|
| `KAL2004` | Warning | `ExecuteAsync` in an `IProcessStepHandler<T>` has a bare `catch (Exception)` without an `OperationCanceledException` filter |
| `KAL2007` | Warning | `[ProcessStep]` class name does not end in `Step` (e.g. `CaptureRequested` instead of `CaptureRequestedStep`) |
| `KAL2008` | Warning | `[ProcessStep]` class has no `IProcessStepHandler<TStep>` in the same compilation |

KAL2004 enforces the cancellation-observability rule from `AGENTS.md`: a bare `catch (Exception)` in a step handler swallows `OperationCanceledException`, inflating error metrics. Add `when (ex is not OperationCanceledException)` or a preceding `catch (OperationCanceledException)`.

KAL2008 is the compile-time equivalent of `ProcessorErrorCodes.MissingHandler`. It fires when the handler is missing from the **same** compilation; cross-assembly handlers suppress the warning.

### Bootstrap conventions (KAL2005, KAL2009)

| Rule | Severity | Trigger |
|---|---|---|
| `KAL2005` | Warning | `o.ServiceName` is assigned a string literal containing uppercase letters, spaces, hyphens, or underscores |
| `KAL2009` | Warning | `AddKaleido(config, o => { ... })` lambda never references `o.Assemblies` |

`ServiceName` is used verbatim as the HTTP route prefix — it must be lowercase with no separators (e.g. `"priorauth"` not `"PriorAuth"`).

`Assemblies` must contain at least one explicit assembly. `AddKaleido()` throws `missing_assembly` at registration for null or empty lists; there is no calling-assembly fallback. KAL2009 warns when an options lambda omits the assignment, but runtime validation is authoritative.

Aggregate registries (`MapKaleidoHttp(o => o.AggregateRegistry = true)`) require `AddHttpClients()` — enforced at map time by a `KaleidoConfigurationException`, so no analyzer rule covers it.
