# Kaleido error codes

Every Kaleido exception carries a stable, machine-readable `Code`. These codes are safe to match on in client code and log aggregators — they will not change between releases.

## Exception types

| Type | Namespace | HTTP result | Code set |
|---|---|---|---|
| `KaleidoValidationException` | `Kaleido.Exceptions` | 400 Bad Request | `ValidationErrorCodes` |
| `KaleidoConfigurationException` | `Kaleido.Exceptions` | 500 Internal Server Error | `ConfigurationErrorCodes` |
| `KaleidoFrameworkException` | `Kaleido.Exceptions` | 500 Internal Server Error | `FrameworkErrorCodes` |
| `KaleidoHttpClientException` | `Kaleido.Http.Client` | — (client-side) | `HttpClientErrorCodes` |

For `Kaleido` exceptions the `Code` and `Message` are both returned in the HTTP error response body.

---

## `ValidationErrorCodes` — 400 Bad Request

All queryable validation codes are prefixed `qry_`. Process validation codes (`pro_`) are reserved for future use.

| Constant | Code | Meaning |
|---|---|---|
| `QryInvalidField` | `qry_invalid_field` | Field referenced in a filter, sort, or parameter does not exist on the query context |
| `QryUnsupportedOperator` | `qry_unsupported_operator` | Filter condition uses an operator not supported by the field |
| `QryFieldNotFilterable` | `qry_field_not_filterable` | Filter condition references a field not marked as filterable |
| `QryFieldNotSortable` | `qry_field_not_sortable` | Sort clause references a field not marked as sortable |
| `QryInvalidPageSize` | `qry_invalid_page_size` | Requested page size is invalid or exceeds the maximum |
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

## `ConfigurationErrorCodes` — 500 (startup / DI misconfiguration)

Cross-cutting codes have no prefix. `pro_` = Process, `qry_` = Queryable.

| Constant | Code | Meaning |
|---|---|---|
| `InvalidServiceName` | `invalid_service_name` | `ServiceName` is null, empty, or invalid |
| `MissingAssembly` | `missing_assembly` | No assemblies configured via `KaleidoServiceOptions.Assemblies` before runtime registration |
| `ProMissingAttribute` | `pro_missing_attribute` | Process step type missing `[ProcessStep]` |
| `ProMissingHandler` | `pro_missing_handler` | Process step has no registered handler |
| `ProInvalidHandler` | `pro_invalid_handler` | Handler does not implement a valid `IProcessStepHandler` |
| `ProDuplicateStep` | `pro_duplicate_step` | Duplicate step names across registered assemblies |
| `ProInvalidRegistration` | `pro_invalid_registration` | Structurally invalid registration (self-ref, circular dep) |
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
