# Kaleido.Platform

This ASP.NET-free shared model contains the registry, Process and Queryable contracts, JSON conventions and HTTP header names consumed by Kaleido's server and client projects. Runtime planning remains in core; endpoint mapping remains in protocol adapters.

> How this project fits into a whole service, with tested examples: the [consumer guide](../../docs/guide/README.md). This README is the reference for the project.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../Http/Http.Server/README.md`](../Http/Http.Server/README.md)
- [`../Http/Http.Client/README.md`](../Http/Http.Client/README.md)

---

## What lives here

### Process and registry contracts
- `ExecuteProcessRequest` — multi-step execute request body
- `ProcessExecutionResponse` — multi-step execute response (results, required step, available steps)
- `ProcessExecutionStepResponse` — per-step result within an execute response
- `ExecuteStepRequest<TStep>` — typed per-step execute request body
- `StepExecutionResponse` / `StepExecutionResponse<TResult>` — per-step execute response
- `ProcessStateResponse` — process state read response
- `ProcessStepSummary` — lightweight step summary used in registry and state responses
- `ProcessRequiredStep` — the step a process requires next: the fields of a `ProcessStepSummary` (including its execute URL) plus the pending information request, if any
- `ProcessorRegistryResponse` — processor metadata, its versioned execute URL, process-state URL template and all steps
- `ProcessStepResponse` — detailed step metadata record
- `ProcessFieldMetadata` / `ProcessFieldLookup` / `ProcessOutputFieldMetadata` / `ProcessStepResultMetadata` — a step's input fields (with their lookup) and its typed result fields

For `POST /{service}/{version}/processes/steps/{step}`, the JSON request is an `ExecuteStepRequest<TStep>` envelope such as `{"processStep":{"field":"value"}}`. The route selects the step, and an existing process id is supplied in the optional `X-Kaleido-Process-Id` header, not the body. Omit it to start a new process; an id that does not exist is rejected (`pro_process_not_found`) unless the call is a handoff from another processor (ADR 0009). See the [per-step HTTP contract](../Http/Http.Server/README.md#per-step-process-execution-contract) for the typed and untyped response shapes.

### Queryable contracts
- `QueryApiRequest` / `QueryApiRequest<TParameters>` — query request body (search, filter, sort, page, optional view parameters)
- `QueryApiBody` / `QueryApiFilterNode` / `QueryApiFilterCondition` / `QueryApiFilterGroup` / `QueryApiSort` / `QueryApiPage` — transport-level query body (string enums, raw `JsonElement` values)
- `QueryableSourceResponse` — a query source in the registry response. It carries:
  - `Name` (what a client passes to address it), `ServiceName`, `ServiceVersion` and the published `QueryUrl`;
  - `DisplayName`, `Description`, `Pageable` and `Authorization`;
  - `Fields` (query rules), `Parameters`, `OutputFields` and `Views`.

  Every source is presented the same way; how the service fulfils it is not part of the contract. `QueryUrl` is null only in a caller-filtered registry, when the caller may use some of the source's views but not the source itself.
- `QueryableViewResponse` — a view of a source (`QueryUrl`, `Parameters`, `OutputFields`, paging)
- `QueryableFieldMetadata` / `QueryableQueryParameter` / `QueryableQueryProperty` / `QueryablePagingMetadata` — a source's query fields, a source's or view's parameters and result fields, and its paging
- Errors use the shared `KaleidoErrorResponse` / `KaleidoError` (with the shared codes in `KaleidoErrorCodes`), defined here: `{ "errors": [{ "code", "message" }] }`; query validation failures are 400 with `qry_*` codes ([`docs/ERROR_CODES.md`](../../docs/ERROR_CODES.md))
- `QueryApiBodyExtensions.ToApiBody()` — converts a runtime `QueryBody` to `QueryApiBody` for callers that receive a `QueryBody` and need to forward it over HTTP (e.g. delegated query sources calling a remote source or view)

### JSON enum values

Kaleido endpoints and the .NET HTTP clients use `KaleidoJsonOptions.Options` without changing the host application's JSON settings. Property names are camelCase; Kaleido enum-valued contract fields serialize as **camelCase strings**, for example `"state":"awaitingStepSelection"`, `"status":"validationFailed"` and `"type":"information"`. Numeric values for these enum fields are rejected. Consumers should use the published strings rather than CLR integer ordinals, and handle unfamiliar future values without treating them as numbers. Property-name binding is case-insensitive, but producing the documented casing keeps non-.NET clients consistent.

Queryable query operators and sort directions are already **string properties** on `QueryApiBody`, not serialized core CLR enums: `QueryApiFilterCondition.Operator`, `QueryApiFilterGroup.Operator` and `QueryApiSort.Direction` accept documented names such as `"notEquals"`, `"and"` and `"ascending"` case-insensitively. Use the canonical string names when building a request; custom converters on application-defined payload types may define their own business-data representation.

### Queryable paging and `totalCount`

A Queryable request may supply `{"query":{"page":{"size":10,"offset":0}}}`. `offset` is zero-based. An explicit `page` is accepted only for a source or view registered with `[Pageable]` (a source's paging applies to direct queries of it, a view's to that view); otherwise the endpoint returns `qry_paging_not_supported` (400). Omitted `size` uses the configured `DefaultSize`, and omitted `offset` uses zero, so `{"page":{}}` and `{"page":{"offset":20}}` are valid explicit pages of `DefaultSize` rows. Explicit `size` must be greater than zero and no greater than `MaxSize` (`qry_invalid_page_size`); a negative `offset` returns `qry_invalid_page_offset`. An oversized explicit request is rejected, not silently clamped.

For locally executed Queryable sources and views, `QueryResult<T>.totalCount` is always the number of matching rows, whichever page was returned, so a consumer can compute the page count from it. A pageable source or view always pages: a request without `page` gets its default page (`offset` 0, `DefaultSize` rows), and the response's `offset` and `pageSize` say which page that was. A source or view that is not pageable returns every matching row.

| Returned page | `totalCount` | Count query |
|---|---|---|
| Full (`results.Count == pageSize`), with or without `page` in the request | All matching rows | Yes |
| Partial, at least one row | `offset + results.Count` (the page ends the results) | No |
| Empty, `offset` past the start | All matching rows | Yes |
| Not pageable | `results.Count` (every row) | No |

For example, with six matching rows and a default page size of three: omitting `page` returns three rows and `totalCount: 6`; `{ "size": 2, "offset": 2 }` returns two rows and `totalCount: 6`; `{ "size": 4, "offset": 4 }` returns two rows and `totalCount: 6`; and an offset beyond the end returns no rows and `totalCount: 6`. A delegated source supplies its own `QueryResult<T>` (its downstream system's paging and count), which Kaleido returns unchanged.

When the total is counted (a full page, or an empty page past the start), the count is a second read of the source after the page. Kaleido reads no snapshot, so rows another writer adds or removes between the two reads can make `totalCount` disagree with the page by that many rows; treat it as the count at the time of the request, not a transactional guarantee. The same value is published as the `kaleido.query.total_count` span tag, the `kaleido.queryable.total_count` histogram and `QueryExecuted.TotalCount`.

---

## Who uses this project

- **Server side** (`Kaleido.Http.Server`, `Kaleido.Registry.Http`) — endpoints serialize response contracts and deserialize request contracts
- **Client side** (`Kaleido.Http.Client`) — clients deserialize response contracts and serialize request contracts
- **Test projects** — functional tests assert against these contract types

Changes here ripple into all of these.

---

## Stability expectations

This project defines published contracts shared by the registry, HTTP adapters and future transports. Changes to these types can break:
- existing remote consumers
- the client implementations in `Kaleido.Http.Client`
- functional tests in `Kaleido.Http.FunctionalTests`

Prefer additive changes (new optional fields) over breaking changes (removing or renaming fields).

When a contract must change, also update:
- the matching endpoint in `Kaleido.Http.Server` or `Kaleido.Registry.Http`
- the matching client method in `Kaleido.Http.Client`
- the matching tests in `Kaleido.Http.FunctionalTests`

Some contracts carry core types as they are: `InformationRequest`, `ProcessMessage`, `KaleidoCaller`,
the Process state enums, `AuthorizationMetadata`, `FilterOperator`/`MatchMode` (field metadata) and
`QueryResult<T>`. They are part of this contract surface too: a change to one of them is a wire
change, and `KaleidoJsonOptionsTests` fixes their JSON. The inventory, and the small amount of
behavior this project holds on purpose (header sanitizing and `ToApiBody()`), are recorded by
the [ADR 0017 amendment](../../docs/decisions/0017-transport-contract-placement.md#amendment-2026-10-26-448-shared-core-shapes-and-the-behavior-abstractions-holds); [ADR 0039](../../docs/decisions/0039-protocol-neutral-package-boundaries.md) supersedes its old package and snapshot-store placement.

---

## Where to look

- `Processor/ProcessContracts.cs` — Process request/response shapes; the client interfaces live in `Kaleido.Client/Processor`.
- `Queryable/QueryableContracts.cs` and `Queryable/QueryApiBody.cs` — Queryable request/response shapes; the client interfaces live in `Kaleido.Client/Queryable`.
- `Registry/KaleidoRegistry.cs` and `Registry/RegistryControlContracts.cs` — central capabilities, availability/status and `RegistryServiceRoute`/`RegistryTransportRoute`. The registry host owns storage; participants hydrate the copy through `Kaleido.Client`.
- `KaleidoJsonOptions.cs`, `KaleidoCorrelationHeaders.cs`, `HttpHeaderSanitizer.cs` — shared JSON and HTTP header behavior.

The published registry contracts are separate from core's internal registry descriptors. `Kaleido.Http.Server` projects runtime metadata into them (#356).
