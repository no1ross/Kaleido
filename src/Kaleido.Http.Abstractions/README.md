# Kaleido.Http.Abstractions

This project contains the shared HTTP request/response contract types used across Kaleido's server-side endpoint projects and the client-side consumption project.

See also:
- [`../../ARCHITECTURE.md`](../../ARCHITECTURE.md)
- [`../Kaleido.Http/README.md`](../Kaleido.Http/README.md)
- [`../Kaleido.Http.Client/README.md`](../Kaleido.Http.Client/README.md)

---

## What lives here

### Process HTTP contracts
- `ExecuteProcessRequest` — multi-step execute request body
- `ProcessExecutionResponse` — multi-step execute response (results, required step, available steps)
- `ProcessExecutionStepResponse` — per-step result within an execute response
- `ExecuteStepRequest<TStep>` — typed per-step execute request body
- `StepExecutionResponse` / `StepExecutionResponse<TResult>` — per-step execute response
- `ProcessStateResponse` — process state read response
- `ProcessStepSummary` — lightweight step summary used in registry and state responses
- `ProcessStepInfo` — step reference with execute/metadata URLs (used in required/available step fields)
- `ProcessorRegistryResponse` — full processor registry record (all step metadata)
- `ProcessStepResponse` — detailed step metadata record

For `POST /{service}/processes/steps/{step}`, the JSON request is an `ExecuteStepRequest<TStep>` envelope such as `{"processStep":{"field":"value"}}`. The route selects the step, and an existing process id is supplied in the optional `X-Kaleido-Process-Id` header, not the body. See the [per-step HTTP contract](../Kaleido.Http/README.md#per-step-process-execution-contract) for the typed and untyped response shapes.

### Queryable HTTP contracts
- `QueryApiRequest` / `QueryApiRequest<TParameters>` — query request body (search, filter, sort, page, optional view parameters)
- `QueryApiBody` / `QueryApiFilterNode` / `QueryApiFilterCondition` / `QueryApiFilterGroup` / `QueryApiSort` / `QueryApiPage` — transport-level query body (string enums, raw `JsonElement` values)
- `QueryableRecordResponse` — full context record in the registry response (carries `ServiceName`, `RegistryUrl`)
- `QueryErrorResponse` — structured query validation error response
- `QueryApiBodyExtensions.ToApiBody()` — converts a runtime `QueryBody` to `QueryApiBody` for callers that receive a `QueryBody` and need to forward it over HTTP (e.g. delegated view sources calling a remote query context)

### JSON enum values

Kaleido endpoints and the .NET HTTP clients use `KaleidoJsonOptions.Options` without changing the host application's JSON settings. Property names are camelCase; Kaleido enum-valued contract fields serialize as **camelCase strings**, for example `"outcome":"completed"`, `"status":"validationFailed"` and `"type":"information"`. Numeric values for these enum fields are rejected. Consumers should use the published strings rather than CLR integer ordinals, and handle unfamiliar future values without treating them as numbers. Property-name binding is case-insensitive, but producing the documented casing keeps non-.NET clients consistent.

Queryable query operators and sort directions are already **string properties** on `QueryApiBody`, not serialized core CLR enums: `QueryApiFilterCondition.Operator`, `QueryApiFilterGroup.Operator` and `QueryApiSort.Direction` accept documented names such as `"notEquals"`, `"and"` and `"ascending"` case-insensitively. Use the canonical string names when building a request; custom converters on application-defined payload types may define their own business-data representation.

### Queryable paging and `totalCount`

A Queryable request may supply `{"query":{"page":{"size":10,"offset":0}}}`. `offset` is zero-based. An explicit `page` is accepted only for a context or view registered with `[Pageable]`; otherwise the endpoint returns `qry_paging_not_supported` (400). Omitted `size` uses the configured `DefaultSize`, and omitted `offset` uses zero. Explicit `size` must be greater than zero and no greater than `MaxSize` (`qry_invalid_page_size`); a negative `offset` returns `qry_invalid_page_offset`. An oversized explicit request is rejected, not silently clamped.

For locally executed Queryable contexts and views, `QueryResult<T>.totalCount` depends on whether the request explicitly contains `page` and whether the returned page is full:

| Request | Rows returned | `totalCount` |
|---|---|---|
| No `page` | A pageable query applies its default page size; a non-pageable query is not paged. | `results.Count` — **not necessarily all matching rows** when the default size caps a pageable query. |
| Explicit `page`, full (`results.Count == effective size`) | Up to the requested or defaulted size, from `offset`. | Count of all matching rows from a separate count query. |
| Explicit `page`, partial or empty | Fewer rows than the effective size, including an offset beyond the end. | `results.Count`, **not** a global count when `offset` is nonzero. No separate count query runs. |

For example, with six matching rows and a default page size of three: omitting `page` returns three rows and `totalCount: 3`; `{ "size": 2, "offset": 2 }` returns two rows and `totalCount: 6`; `{ "size": 4, "offset": 4 }` returns two rows and `totalCount: 2`; and an offset beyond the end returns no rows and `totalCount: 0`. Do not calculate a global page count from `totalCount` on the skipped-count paths. A delegated view supplies its own `QueryResult<T>` and is responsible for its reported count.

---

## Who uses this project

- **Server side** (`Kaleido.Http`) — endpoints serialize response contracts and deserialize request contracts
- **Client side** (`Kaleido.Http.Client`) — clients deserialize response contracts and serialize request contracts
- **Test projects** — functional tests assert against these contract types

Changes here ripple into all of these.

---

## Stability expectations

This project defines the published HTTP contract surface. Changes to these types can break:
- existing remote consumers
- the client implementations in `Kaleido.Http.Client`
- functional tests in `Kaleido.Http.FunctionalTests`

Prefer additive changes (new optional fields) over breaking changes (removing or renaming fields).

When a contract must change, also update:
- the matching endpoint in `Kaleido.Http`
- the matching client method in `Kaleido.Http.Client`
- the matching tests in `Kaleido.Http.FunctionalTests`

---

## Where to look

- `Process/Contracts/` — all Process HTTP request/response types
- `Queryable/Contracts/` — all Queryable HTTP request/response types
