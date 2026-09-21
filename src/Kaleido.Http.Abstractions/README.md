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
- `ProcessStepSummary` — lightweight step summary used in catalog and state responses
- `ProcessStepInfo` — step reference with execute/metadata URLs (used in required/available step fields)
- `ProcessorRegistryResponse` — full processor registry record (all step metadata)
- `ProcessStepResponse` — detailed step metadata record

### Queryable HTTP contracts
- `QueryApiRequest` / `QueryApiRequest<TParameters>` — query request body (search, filter, sort, page, optional view parameters)
- `QueryableRecordResponse` — full context record in the registry response
- `QueryableRecordSummary` — lightweight context summary in the catalog response
- `QueryErrorResponse` — structured query validation error response
- `QueryBody` / `QueryPage` / `QueryFilterNode` — query body sub-types

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
