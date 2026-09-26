# Kaleido contributor guide

This is the repository-level contributor guide for Kaleido.

Kaleido is organized into six main framework projects:
- [`src/Kaleido`](./src/Kaleido/README.md) — foundational bootstrap, shared abstractions, metadata primitives, eventing, transport-agnostic correlation context, and the core runtime for both Process and Queryable
- [`src/Kaleido.Http`](./src/Kaleido.Http/README.md) — HTTP transport: endpoint publication, middleware pipeline (`ExceptionMiddleware`, `ObservabilityMiddleware`), correlation context reader, startup filter, and execution services
- [`src/Kaleido.Http.Abstractions`](./src/Kaleido.Http.Abstractions/README.md) — shared HTTP contract types and HTTP-specific correlation primitives (`KaleidoCorrelationHeaders`, `HttpHeaderSanitizer`) used by both server-side and client-side projects
- [`src/Kaleido.Http.Client`](./src/Kaleido.Http.Client/README.md) — typed HTTP clients for consuming remote Process and Queryable endpoints
- [`src/Kaleido.Observability.OpenTelemetry`](./src/Kaleido.Observability.OpenTelemetry/README.md) — optional OpenTelemetry provider: `AddOpenTelemetry()` on `IKaleidoBuilder` for full OTel setup (logging + tracing + metrics + OTLP), and `AddKaleidoInstrumentation()` on `TracerProviderBuilder`/`MeterProviderBuilder` for consumers managing their own OTel pipeline
- [`src/Kaleido.Provider.SQLite`](./src/Kaleido.Provider.SQLite/README.md) — SQLite-backed durable process state store

Read [`ARCHITECTURE.md`](./ARCHITECTURE.md) first for the top-level repository model. Then read the project README for the area you are changing.

## How to navigate the repo

### Repository-level docs
- [`README.md`](./README.md)
- [`ARCHITECTURE.md`](./ARCHITECTURE.md)
- [`AGENTS.md`](./AGENTS.md)

### Source docs
- [`src/AGENTS.md`](./src/AGENTS.md) — source-level contributor guide
- [`src/ARCHITECTURE.md`](./src/ARCHITECTURE.md) — source-level architecture details
- [`src/Kaleido/README.md`](./src/Kaleido/README.md)
- [`src/Kaleido.Http/README.md`](./src/Kaleido.Http/README.md)
- [`src/Kaleido.Http.Abstractions/README.md`](./src/Kaleido.Http.Abstractions/README.md)
- [`src/Kaleido.Http.Client/README.md`](./src/Kaleido.Http.Client/README.md)
- [`src/Kaleido.Observability.OpenTelemetry/README.md`](./src/Kaleido.Observability.OpenTelemetry/README.md)
- [`src/Kaleido.Provider.SQLite/README.md`](./src/Kaleido.Provider.SQLite/README.md)

### Tests and samples
- [`tests/AGENTS.md`](./tests/AGENTS.md)
- [`samples/PriorAuth/AGENTS.md`](./samples/PriorAuth/AGENTS.md)

## Project boundaries

### Kaleido (core)
Owns shared substrate concerns and capability runtimes:
- bootstrap and builder state (`AddKaleido()`, `IKaleidoBuilder`, `KaleidoServiceOptions`)
- shared metadata primitives (`DataTypeMapper`, `ConstraintMapper`)
- shared eventing abstractions and correlation context
- Queryable runtime: context/view registration, validation, dispatch, execution, observability
- Process runtime: step registration, planning, execution, state mutation, observability
- Providers abstraction (`IProcessContextStore`)

### Kaleido.Http
Owns the full HTTP transport layer:
- `AddHttp()` — registers routing, `IHttpContextAccessor`, the middleware pipeline, and HTTP execution services
- `ExceptionMiddleware` — outermost middleware; maps exceptions to JSON error responses
- `ObservabilityMiddleware` — reads inbound correlation headers, populates `IKaleidoCorrelationContextAccessor`, tags the Activity, and echoes correlation headers on the response
- `HttpCorrelationContextReader` — reads and sanitizes inbound HTTP headers into `KaleidoCorrelationContext`
- `KaleidoStartupFilter` — registers middlewares in the correct pipeline order via `IStartupFilter`
- Queryable endpoint mapping (`MapQueryable`) — catalog, registry, query, and metadata endpoints
- Process endpoint mapping (`MapProcessor`) — catalog, registry, metadata, execute, and state endpoints
- Registry endpoint mapping (`MapRegistry`) — aggregated discovery combining Process and Queryable

### Kaleido.Http.Abstractions
Owns shared HTTP contract types and HTTP-specific correlation primitives:
- `KaleidoCorrelationHeaders` — canonical `X-Kaleido-*` header name constants
- `HttpHeaderSanitizer` — RFC 7230-compliant sanitization of HTTP header values
- Process HTTP request/response contracts (`ExecuteProcessRequest`, `ProcessExecutionResponse`, etc.)
- Queryable HTTP request/response contracts (`QueryApiRequest`, `QueryableRecordResponse`, etc.)
- Shared contract types used by both server-side and client-side projects

### Kaleido.Http.Client
Owns typed HTTP clients:
- `IKaleidoProcessClientFactory` / `KaleidoProcessClient` for consuming remote process endpoints
- `IKaleidoQueryableClientFactory` / `KaleidoQueryableClient` for consuming remote queryable endpoints
- `AddHttpClients()` — consumer-facing registration (config-driven `Kaleido:Clients`); `AddProcessClient(...)`/`AddQueryableClient(...)` are internal

### Kaleido.Observability.OpenTelemetry
Owns the OpenTelemetry observability provider (opt-in):
- `AddOpenTelemetry()` on `IKaleidoBuilder` — one-call setup: logging + tracing + metrics + OTLP export
- `AddKaleidoInstrumentation()` on `TracerProviderBuilder` — registers Kaleido `ActivitySource`s for consumers managing their own OTel pipeline
- `AddKaleidoInstrumentation()` on `MeterProviderBuilder` — registers Kaleido `Meter`s with tuned histogram bucket boundaries
- Does **not** own core telemetry instrumentation — that lives in `Kaleido` (BCL `ActivitySource`/`Meter`)

Kaleido core is observability-provider-agnostic. This project is one of many possible providers (`Kaleido.Observability.<Technology>`).

### Kaleido.Provider.SQLite
Owns the SQLite durable state provider:
- `UseSqliteContextStore(...)` extension
- SQLite-backed `IProcessContextStore` implementation

## General contributor rules

- Keep concerns in the correct project.
- `Kaleido` (core) should remain free of transport-specific behavior.
- `Kaleido.Http` should adapt and publish; it should not reimplement runtime logic.
- `Kaleido.Http.Abstractions` is shared — changes here ripple into both server and client projects.
- Keep transport layers thin.
- Prefer explicit registration and discoverability over hidden behavior.
- Match documentation to the actual code and runtime behavior.
- When you change contracts or metadata semantics, review the downstream docs and tests for the affected area.

## Coding patterns

### Primary constructors
- Use primary constructor syntax for simple dependency injection: `class MyClass(IService service)`
- Use parameter names without underscore prefix: `service` not `_service`
- Convert constructors that only do field assignments OR only have `ArgumentNullException.ThrowIfNull` calls
- Do NOT convert constructors with complex logic in the body (loops, conditionals beyond null checks)
- For nested classes in observability types, also convert to primary constructors

### Nullable suppression operators
- Avoid nullable suppression operators (`!`) where possible
- Replace `GetMethod(...)!` with explicit null checks: `GetMethod(...) ?? throw new KaleidoFrameworkException(...)`
- Use `KaleidoFrameworkException` for framework integrity violations (e.g., missing methods via reflection)
- For properties that can legitimately be null, make them nullable (`object?` instead of `object = null!`)
- Use `.OfType<T>()` to filter nulls from collections instead of `!` on each element

### Log levels
Information logs must stay minimal — treat them as the "normal operations" view an operator reads without filtering. Target no more than 2–5 Information logs per request.
- **Information** — boundary signals only: request-in/response-out equivalents (e.g. `ExecutionCompleted` — once per request) and once-per-service-lifetime events (e.g. registry built at startup). Never per-step or per-item logs.
- **Debug** — all internals: step started/completed, context saves, source/view/materialization scopes, downstream fetch details, send/receive plumbing. This is what gets enabled when investigating by correlationId / requestId / processId.
- **Warning** — cancellations, client disconnects, downstream non-success responses, validation failures.
- **Error** — exceptions and failures only.
- Do not promote internals to Information "for visibility" — if it fires more than once per request, it belongs at Debug.

### Exception handling
- Always use custom exceptions from `Kaleido.Exceptions` namespace, never `InvalidOperationException`
- `KaleidoValidationException` — 400 Bad Request; `Code` and `Message` are returned in the HTTP response body
- `KaleidoConfigurationException` — 500; startup/DI misconfiguration; `Code` is log-only, `Message` is safe to surface
- `KaleidoFrameworkException` — 500; internal integrity violation; `Code` is log-only, `Message` is safe to surface
- `KaleidoHttpClientException` — client-side only, never reaches HTTP; carries `Code`, `StatusCode`, and `Errors`
- Error codes are organized by domain:
  - `ValidationErrorCodes` (`KaleidoValidationException.cs`) — `qry_*` codes for queryable validation
  - `ConfigurationErrorCodes` (`KaleidoConfigurationException.cs`) — `pro_*`/`qry_*`/unprefixed for startup errors
  - `FrameworkErrorCodes` (`KaleidoFrameworkException.cs`) — internal integrity violation codes
  - `HttpClientErrorCodes` (`KaleidoClientException.cs`) — `httpclient_*` codes for remote call failures
  - `KaleidoErrorCodes` (`KaleidoErrorResponse.cs`) — shared HTTP error codes (`argument_error`, `framework_error`)

### OperationCanceledException and observability
Never record `OperationCanceledException` as an execution failure — it inflates error metrics and triggers false alerts.

The rule is: **one observability signal per cancellation, at the lowest level that has full context.**

- **Process:** `ProcessExecutor` is the single recording point (`stepObservation.Canceled()`). It has step name, version, and processor name, and is where state is saved on cancellation. All layers above (`ProcessStepInvoker`, `ProcessRuntime`) use `when (exception is not OperationCanceledException)` on their `catch (Exception)` blocks so OCE propagates cleanly without triggering `ExecutionFailed` or `HandlerFailed`.
- **Queryable:** `QueryContextEngine` and `DelegatedQueryViewEngine` each call `observation.Canceled()` in an explicit `catch (OperationCanceledException)` block placed before `catch (Exception)`. These are mutually exclusive code paths (dispatched by `QueryableService`), so only one signal fires per request.
- Do **not** add `Canceled()` calls at higher levels (`ProcessRuntime`, `ProcessStepInvoker`) — you will get duplicate signals for the same cancellation event.

### Record conversion
- Convert immutable data containers with init-only properties to records
- Do NOT convert service classes with behavior to records
- Do NOT convert exception classes to records (they inherit from Exception)

### Collection expressions
- Use collection expressions `[]` for property initializers where type is inferred: `public ICollection<T> Items { get; } = [];`
- For local variables, keep explicit type: `var items = new List<T>();` (collection expressions without explicit type don't compile)
- For dictionaries with comparers, keep old syntax: `new Dictionary<T, U>(StringComparer.OrdinalIgnoreCase)` - collection expressions don't support custom comparers
- For dictionary initializers, use old syntax with `[key] = value` - collection expressions use `=>` which is different

### Using statements
- Use global usings where appropriate to reduce redundant using statements
- Keep using statements minimal and project-specific

## Documentation rules

- Root docs should explain how the projects fit together.
- Project READMEs should explain what lives in that project specifically.
- Contributor guides should focus on invariants, boundaries, and what not to change casually.

## Tests and samples

Tests and samples are important navigation aids:
- [`tests/AGENTS.md`](./tests/AGENTS.md)
- [`samples/PriorAuth/AGENTS.md`](./samples/PriorAuth/AGENTS.md)

Use samples to understand intended consumer usage.
Use tests to understand behavioral expectations and invariants.

## Rule of thumb

- If the concern is bootstrap, shared metadata, eventing, correlation, or the Queryable/Process runtime, it belongs in `Kaleido`.
- If the concern is HTTP transport wiring, middleware, correlation propagation, or endpoint mapping, it belongs in `Kaleido.Http`.
- If the concern is shared HTTP contract types used by both server and client, it belongs in `Kaleido.Http.Abstractions`.
- If the concern is calling a remote Kaleido service over HTTP, it belongs in `Kaleido.Http.Client`.
- If the concern is OpenTelemetry provider wiring (exporters, instrumentation, resource config), it belongs in `Kaleido.Observability.OpenTelemetry`.
- If the concern is durable process state storage via SQLite, it belongs in `Kaleido.Provider.SQLite`.
