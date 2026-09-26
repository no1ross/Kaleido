# Kaleido Architecture

This document describes the current top-level architecture of the Kaleido repository. It is the entry point for understanding how the major framework projects fit together and where responsibility boundaries live.

Kaleido is a metadata-driven framework for exposing business capabilities through consistent, discoverable contracts.

At the highest level, the repository is organized around six source projects:

- [`Kaleido`](./src/Kaleido/README.md) — foundational bootstrap, shared abstractions, metadata primitives, eventing, correlation context, and the core runtimes for both Process and Queryable
- [`Kaleido.Http`](./src/Kaleido.Http/README.md) — HTTP transport: DI registration (`AddHttp()`), middleware pipeline, endpoint publication, and route mapping for Process, Queryable, and the aggregated Registry
- [`Kaleido.Http.Abstractions`](./src/Kaleido.Http.Abstractions/README.md) — shared HTTP request/response contract types used across server-side and client-side projects
- [`Kaleido.Http.Client`](./src/Kaleido.Http.Client/README.md) — typed HTTP clients for consuming remote Process and Queryable endpoints
- [`Kaleido.Observability.OpenTelemetry`](./src/Kaleido.Observability.OpenTelemetry/README.md) — optional OpenTelemetry provider (logging, tracing, metrics, OTLP)
- [`Kaleido.Provider.SQLite`](./src/Kaleido.Provider.SQLite/README.md) — SQLite-backed durable process state store

See also:
- [`README.md`](./README.md)
- [`AGENTS.md`](./AGENTS.md)

---

## 1. Architectural overview

Kaleido separates foundational runtime concerns from transport and persistence concerns.

### Kaleido (core)
The core project provides everything needed to bootstrap the framework and run Process and Queryable at the application layer:
- bootstrap and builder state (`AddKaleido()`, `IKaleidoBuilder`, `KaleidoServiceOptions`)
- shared metadata/type mapping (`DataTypeMapper`, `ConstraintMapper`)
- validation metadata
- eventing abstractions and correlation context
- Queryable runtime: context/view registration, validation, dispatch (direct, local-view, delegated-view), execution, observability
- Process runtime: step registration, planning, candidate building/validation, execution, state mutation, persistence integration, observability
- Default in-memory `IProcessContextStore`

The core project does not define transport endpoints or ASP.NET Core services.

See: [`src/Kaleido/README.md`](./src/Kaleido/README.md)

### Kaleido.Http
The HTTP project provides the full HTTP transport layer:
- `AddHttp()` — registers routing, `IHttpContextAccessor`, the middleware pipeline, and HTTP execution services
- `ExceptionMiddleware` — outermost middleware; maps exceptions to JSON error responses
- `ObservabilityMiddleware` — reads inbound correlation headers, populates the correlation accessor, tags the Activity, and echoes correlation headers on the response
- `HttpCorrelationContextReader` — reads and sanitizes inbound HTTP headers
- `KaleidoStartupFilter` — registers middlewares in the correct pipeline order
- `MapQueryable()` — catalog, registry, per-context metadata, direct query, and view query endpoints
- `MapProcessor()` — catalog, registry, per-step metadata, execute, step execute, and process state endpoints
- `MapRegistry()` — aggregated discovery combining the local processor and all downstream clients

It depends on `Kaleido.Http.Abstractions` (which depends on `Kaleido`).

See: [`src/Kaleido.Http/README.md`](./src/Kaleido.Http/README.md)

### Kaleido.Http.Abstractions
Shared HTTP contract types used by both the server-side projects and the client project:
- Process contracts: `ExecuteProcessRequest`, `ProcessExecutionResponse`, `ProcessExecutionStepResponse`, `ProcessStepInfo`, `ProcessStateResponse`, `ProcessStepSummary`, `ProcessorRegistryResponse`, etc.
- Queryable contracts: `QueryApiRequest`, `QueryableRecordResponse`, `QueryableRecordSummary`, `QueryErrorResponse`, etc.

Changes here ripple into server-side endpoints (`Kaleido.Http`) and client-side consumers (`Kaleido.Http.Client`).

See: [`src/Kaleido.Http.Abstractions/README.md`](./src/Kaleido.Http.Abstractions/README.md)

### Kaleido.Http.Client
Typed HTTP clients for downstream service consumption:
- `IKaleidoProcessClientFactory` / `KaleidoProcessClient` — registry, step metadata, process state, and step execution
- `IKaleidoQueryableClientFactory` / `KaleidoQueryableClient` — registry, context metadata, view queries, direct context queries
- `AddHttpClients()` — registers both Process and Queryable clients from configuration
- `AddProcessClient(...)`, `AddQueryableClient(...)` — individual client registration (internal)

See: [`src/Kaleido.Http.Client/README.md`](./src/Kaleido.Http.Client/README.md)

### Kaleido.Observability.OpenTelemetry
Optional OpenTelemetry provider (opt-in; core emits BCL `ActivitySource`/`Meter` only):
- `AddOpenTelemetry()` on `IKaleidoBuilder` — one-call setup: logging + tracing + metrics + OTLP export
- `AddKaleidoInstrumentation()` on `TracerProviderBuilder`/`MeterProviderBuilder` — for consumers managing their own OTel pipeline

See: [`src/Kaleido.Observability.OpenTelemetry/README.md`](./src/Kaleido.Observability.OpenTelemetry/README.md)

### Kaleido.Provider.SQLite
SQLite-backed durable process state:
- Replaces the default in-memory `IProcessContextStore` with a SQLite-backed implementation
- Registered via `UseSqliteProcessContextStore(connectionString)`

See: [`src/Kaleido.Provider.SQLite/README.md`](./src/Kaleido.Provider.SQLite/README.md)

---

## 2. Top-level design principles

### Metadata first
Capabilities are described through metadata and registrations rather than ad hoc, hardcoded integration knowledge.

### Explicit registration
Assemblies and framework components are registered intentionally. Discovery happens from known registration input rather than hidden global scanning.

### Strongly typed internals
Runtime components operate on CLR types and internal contracts rather than transport-specific types.

### Thin transport layers
`Kaleido.Http` adapts requests and responses to runtime contracts; it does not reimplement business semantics.

### Clear project boundaries
Core runtime concerns live in `Kaleido`. HTTP transport (DI, middleware, routes) lives in `Kaleido.Http`. Shared contracts live in `Kaleido.Http.Abstractions`. Remote consumption lives in `Kaleido.Http.Client`. Observability providers live in `Kaleido.Observability.*`. Persistence lives in `Kaleido.Provider.SQLite`.

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
- [`src/Kaleido.Observability.OpenTelemetry`](./src/Kaleido.Observability.OpenTelemetry/README.md) — OpenTelemetry provider
- [`src/Kaleido.Provider.SQLite`](./src/Kaleido.Provider.SQLite/README.md) — SQLite process state provider

### Tests
- [`tests/AGENTS.md`](./tests/AGENTS.md)
- `tests/Kaleido.UnitTests` — core runtime unit tests
- `tests/Kaleido.Http.UnitTests` — endpoint route builder unit tests
- `tests/Kaleido.Http.FunctionalTests` — Process and Queryable HTTP functional tests
- `tests/Kaleido.Http.Client.UnitTests` — HTTP client unit tests
- `tests/Kaleido.Http.Abstractions.UnitTests` — shared contract unit tests
- `tests/Kaleido.Provider.SQLite.UnitTests` — SQLite store unit tests
- `tests/Kaleido.IntegrationTests` — cross-runtime integration tests
- `tests/Kaleido.Analyzers.*.UnitTests` — analyzer and source-generator tests

### Samples
- [`samples/PriorAuth`](./samples/PriorAuth)
- [`samples/kaleido-sample-ecommerce-ui`](./samples/kaleido-sample-ecommerce-ui)

---

## 4. Registration model

The repository follows a layered registration model.

### Step 1: Core bootstrap
Applications start with `AddKaleido(IConfiguration, Action<KaleidoServiceOptions>)`, which:
- establishes shared DI baseline services
- validates service identity and options
- returns an `IKaleidoBuilder` with assemblies configured via `KaleidoServiceOptions.Assemblies`
- automatically calls `AddProcessor()` and `AddQueryable()` to register runtimes

### Step 2: Assembly registration
Assemblies are passed via `KaleidoServiceOptions.Assemblies` in the `AddKaleido()` configure callback. Those assemblies become shared registration input for the Queryable and Process runtimes.

### Step 3: Capability registration
- `AddProcessor()` (internal, called automatically) scans registered assemblies for `[ProcessStep]` types and handlers. It builds the step registry and registers runtime services.
- `AddQueryable()` (internal, called automatically) scans registered assemblies for `[QueryContext]` types, view sources, and context sources. It builds the query registry and registers runtime services.

### Step 4: Transport registration (optional)
- `AddHttp()` adds the HTTP transport layer services (middleware pipeline, execution services) for both Process and Queryable.
- `AddHttpClients()` registers typed HTTP clients for downstream services from configuration.
- `MapProcessor()`, `MapQueryable()`, and `MapRegistry()` publish the HTTP endpoints (call only the ones you need).

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

Metadata is derived from CLR types using `DataTypeMapper` and `ConstraintMapper` in the core project.

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
- Read [`src/Kaleido.Provider.SQLite/README.md`](./src/Kaleido.Provider.SQLite/README.md) for durable process state
