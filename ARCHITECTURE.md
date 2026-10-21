# Kaleido Architecture

This document describes the current top-level architecture of the Kaleido repository. It is the entry point for understanding how the major framework projects fit together and where responsibility boundaries live.

Kaleido is a metadata-driven framework for exposing business capabilities through consistent, discoverable contracts.

At the highest level, the repository is organized around seven source projects:

- [`Kaleido`](./src/Kaleido/README.md) — foundational bootstrap, shared abstractions, metadata primitives, eventing, correlation context, and the core runtimes for both Process and Queryable
- [`Kaleido.Http`](./src/Kaleido.Http/README.md) — HTTP transport: DI registration (`AddHttp()`), middleware pipeline, endpoint publication, and route mapping for Process, Queryable, and the aggregated Registry
- [`Kaleido.Http.Abstractions`](./src/Kaleido.Http.Abstractions/README.md) — shared HTTP request/response contract types used across server-side and client-side projects
- [`Kaleido.Http.Client`](./src/Kaleido.Http.Client/README.md) — typed HTTP clients for consuming remote Process and Queryable endpoints
- [`Kaleido.Http.Mcp`](./src/Kaleido.Http.Mcp/README.md) — MCP adapter: publishes the host's (aggregated) registry to AI agents as MCP tools
- [`Kaleido.Observability.OpenTelemetry`](./src/Kaleido.Observability.OpenTelemetry/README.md) — optional OpenTelemetry provider (logging, tracing, metrics, OTLP)
- [`Kaleido.Provider.SQLite`](./src/Kaleido.Provider.SQLite/README.md) - reference implementations of the store contracts on one SQLite database (process state, the built-in outbox's storage, idempotent requests) and the transactions that make a state change and its event atomic

See also:
- [`README.md`](./README.md)
- [`AGENTS.md`](./AGENTS.md)

---

## 1. Architectural overview

Kaleido separates foundational runtime concerns from transport and persistence concerns. The projects reference each other in one direction:

```text
Kaleido.Http.Mcp ──► Kaleido.Http ────────┐
        │                                 ├──► Kaleido.Http.Abstractions ──► Kaleido
        └──────────► Kaleido.Http.Client ─┘
Kaleido.Observability.OpenTelemetry ──► Kaleido
Kaleido.Provider.SQLite ──────────────► Kaleido
```

- **[`Kaleido`](./src/Kaleido/README.md)** (core) bootstraps the framework and runs Process and Queryable at the application layer: builder state, metadata derived from CLR types, eventing, correlation, both runtimes, the store contract and its in-memory default. It defines no transport endpoints and no ASP.NET Core services.
- **[`Kaleido.Http`](./src/Kaleido.Http/README.md)** is the HTTP transport: DI registration, the middleware pipeline, and the endpoints of the active runtimes and the registry (optionally aggregating downstream services).
- **[`Kaleido.Http.Abstractions`](./src/Kaleido.Http.Abstractions/README.md)** holds the wire contracts used at both ends of each HTTP call; errors from every Kaleido endpoint share one shape. Changes here ripple into the server and the clients.
- **[`Kaleido.Http.Client`](./src/Kaleido.Http.Client/README.md)** calls remote Kaleido services: registry, process state, step execution, source and view queries.
- **[`Kaleido.Http.Mcp`](./src/Kaleido.Http.Mcp/README.md)** publishes the host's (aggregated) registry to AI agents as MCP tools, built per caller; tool calls go to the services through the Kaleido clients with the caller's credentials.
- **[`Kaleido.Observability.OpenTelemetry`](./src/Kaleido.Observability.OpenTelemetry/README.md)** is an optional provider that exports Kaleido's telemetry (core emits BCL `ActivitySource`/`Meter` only).
- **[`Kaleido.Provider.SQLite`](./src/Kaleido.Provider.SQLite/README.md)** is a reference store on SQLite (process state, idempotent results, event outbox).

Each README lists the project's types and entry points; [`src/ARCHITECTURE.md`](./src/ARCHITECTURE.md) describes their internals.

---

## 2. Top-level design principles

### A process engine, never a decision maker
Kaleido does what it's told: it moves requests between clients and existing systems, applies declared rules (the step graph, declared roles and policies, structural validation) and records what happened. Business decisions — what to ask, who may access a case, which path to take, what wording to use — belong to the implementer and the underlying systems. See [ADR 0001](./docs/decisions/0001-process-engine-not-decision-maker.md) and the other [decision records](./docs/decisions/README.md).

### Metadata first
Capabilities are described through metadata and registrations rather than ad hoc, hardcoded integration knowledge.

### Explicit registration
Assemblies and framework components are registered intentionally. Discovery happens from known registration input rather than hidden global scanning.

### Strongly typed internals
Runtime components operate on CLR types and internal contracts rather than transport-specific types.

### Thin transport layers
`Kaleido.Http` adapts requests and responses to runtime contracts; it does not reimplement business semantics.

### Clear project boundaries
Core runtime concerns live in `Kaleido`. HTTP transport (DI, middleware, routes) lives in `Kaleido.Http`. Shared contracts live in `Kaleido.Http.Abstractions`. Remote consumption lives in `Kaleido.Http.Client`. The MCP adapter lives in `Kaleido.Http.Mcp`. Observability providers live in `Kaleido.Observability.*`. Persistence lives in `Kaleido.Provider.SQLite`.

---

## 3. Repository structure

### Root-level docs
- [`README.md`](./README.md) — overall framework overview
- [`ARCHITECTURE.md`](./ARCHITECTURE.md) — this document
- [`AGENTS.md`](./AGENTS.md) — repo-level contributor guide

### Source projects
- [`src/Kaleido`](./src/Kaleido/README.md) — core runtime
- [`src/Kaleido.Http`](./src/Kaleido.Http/README.md) — HTTP endpoint publication
- [`src/Kaleido.Http.Abstractions`](./src/Kaleido.Http.Abstractions/README.md) — shared HTTP contracts
- [`src/Kaleido.Http.Client`](./src/Kaleido.Http.Client/README.md) — typed HTTP clients
- [`src/Kaleido.Http.Mcp`](./src/Kaleido.Http.Mcp/README.md) — MCP adapter
- [`src/Kaleido.Observability.OpenTelemetry`](./src/Kaleido.Observability.OpenTelemetry/README.md) — OpenTelemetry provider
- [`src/Kaleido.Provider.SQLite`](./src/Kaleido.Provider.SQLite/README.md) — SQLite process state provider

### Tests
- [`tests/AGENTS.md`](./tests/AGENTS.md)
- `tests/Kaleido.UnitTests` — core runtime unit tests
- `tests/Kaleido.Http.UnitTests` — endpoint route builder unit tests
- `tests/Kaleido.Http.FunctionalTests` — Process and Queryable HTTP functional tests
- `tests/Kaleido.Http.Client.UnitTests` — HTTP client unit tests
- `tests/Kaleido.Http.Abstractions.UnitTests` — shared contract unit tests
- `tests/Kaleido.Http.Mcp.UnitTests` — MCP adapter unit tests
- `tests/Kaleido.Http.Mcp.FunctionalTests` — MCP adapter end to end (SDK MCP client against a real host)
- `tests/Kaleido.Observability.OpenTelemetry.UnitTests` — OpenTelemetry registration tests
- `tests/Kaleido.Provider.SQLite.UnitTests` — SQLite store unit tests
- `tests/Kaleido.IntegrationTests` — DI and EF Core integration tests
- `tests/Kaleido.Analyzers.*.UnitTests` — analyzer tests

### Samples
- [`samples/PriorAuth`](./samples/PriorAuth) — the reference sample: several services, a router with the MCP adapter, and an Angular UI
- [`samples/ECommerce`](./samples/ECommerce) and [`samples/kaleido-sample-ecommerce-ui`](./samples/kaleido-sample-ecommerce-ui) — a single-service sample with its UI
- [`samples/SQLite`](./samples/SQLite) — a minimal queryable host over CSV records covering every field type, filter operator and match mode (despite the folder name, it uses the default in-memory store)

---

## 4. Registration model

The repository follows a layered registration model.

### Step 1: Core bootstrap
Applications start with `AddKaleido(IConfiguration, Action<KaleidoServiceOptionsBuilder>)`, which:
- establishes shared DI baseline services
- validates service identity and options
- returns an `IKaleidoBuilder` with assemblies configured via `KaleidoServiceOptions.Assemblies`
- automatically calls `AddProcessor()` and `AddQueryable()` to register runtimes

### Step 2: Assembly registration
Assemblies are passed via `KaleidoServiceOptions.Assemblies` in the `AddKaleido()` configure callback. Those assemblies become shared registration input for the Queryable and Process runtimes.

### Step 3: Capability registration
- `AddProcessor()` (internal, called automatically) scans registered assemblies for steps (`IProcessStep`, described by `[ProcessStep]`) and their handlers. It builds the step registry and registers runtime services.
- `AddQueryable()` (internal, called automatically) scans registered assemblies for query sources (`IQuerySource<T>`, `IQuerySourceAsync<T>`, `IDelegatedQuerySource<…>`) and query views (`IQueryViewSource<TSource, …>`, `IQueryViewSourceAsync<TSource, …>`), each described by `[QuerySource]` / `[QueryView]`. It builds the query registry and registers runtime services.

### Step 4: Transport registration (optional)
- `AddHttp()` adds the HTTP transport layer services (middleware pipeline, execution services) for both Process and Queryable.
- `AddHttpClients()` registers typed HTTP clients for downstream services from configuration.
- `MapKaleidoHttp()` publishes all Kaleido HTTP endpoints for the active runtimes plus the registry (the per-surface mapping it uses is internal).
- `AddMcp()` + `MapKaleidoMcp(pattern)` publish the capabilities to AI agents as MCP tools (needs `AddHttp()` and `AddHttpClients()`).

This keeps:
- bootstrap concerns in `Kaleido`
- query concerns in `Kaleido`
- action/orchestration concerns in `Kaleido`
- transport concerns in `Kaleido.Http` (and `Kaleido.Http.Abstractions` for shared contracts)

---

## 5. Metadata and discoverability

A central repository-level goal is runtime discoverability.

The framework exposes metadata so consumers can understand:
- what information exists (Queryable contexts, views, fields, constraints)
- what actions exist (Process steps, input fields, constraints, dependency relationships)
- what contracts and validation rules apply
- how to navigate the available capability surface

Metadata is derived from CLR types and validation attributes in the core project.

---

## 6. Transport model

Transport concerns are layered separately from the core runtime.

- `Kaleido.Http` adds DI registrations, the middleware pipeline, and HTTP route publication
- `Kaleido.Http.Client` allows calling remote Kaleido services over HTTP
- `Kaleido.Http.Abstractions` defines the shared contract types used at both ends of each HTTP call

This keeps transport-specific code thin and separate from the runtime.

---

## 7. Contributor guidance

When working in this repository:
- start with the relevant project README before changing internals
- keep core runtime concerns in `Kaleido`, not in transport projects
- keep `Kaleido.Http` focused on routing and endpoint adaptation, not business logic
- keep `Kaleido.Http.Abstractions` stable — changes here ripple to both server and client
- verify that documentation matches the code, not the other way around

For contributor-oriented guidance, see:
- [`AGENTS.md`](./AGENTS.md)
- [`src/AGENTS.md`](./src/AGENTS.md)

---

## 8. Where to look next

- Start with [`src/ARCHITECTURE.md`](./src/ARCHITECTURE.md) for the source-level architecture details
- Read [`src/Kaleido/README.md`](./src/Kaleido/README.md) to understand bootstrap, the Process runtime, and the Queryable runtime
- Read [`src/Kaleido.Http/README.md`](./src/Kaleido.Http/README.md) for HTTP endpoint publication
- Read [`src/Kaleido.Http.Abstractions/README.md`](./src/Kaleido.Http.Abstractions/README.md) for shared HTTP contracts
- Read [`src/Kaleido.Http.Client/README.md`](./src/Kaleido.Http.Client/README.md) for remote service consumption
- Read [`src/Kaleido.Http.Mcp/README.md`](./src/Kaleido.Http.Mcp/README.md) for publishing capabilities to AI agents
- Read [`src/Kaleido.Provider.SQLite/README.md`](./src/Kaleido.Provider.SQLite/README.md) for durable process state
