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

### `totalCount` semantics
`QueryResult<T>.totalCount` means "total matching rows" when `page` is provided in the request. When `page` is absent, `totalCount` equals `results.Count` — the caller received all results (capped by `[Pageable].MaxSize` if present). When `page` is provided and the returned page is partial (`results.Count < page.size`), `totalCount` also equals `results.Count` — no additional rows exist.

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
