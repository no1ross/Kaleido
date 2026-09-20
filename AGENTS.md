# Kaleido contributor guide

This is the repository-level contributor guide for Kaleido.

Kaleido is organized into six main framework projects:
- [`src/Kaleido`](./src/Kaleido/README.md) — foundational bootstrap, shared abstractions, metadata primitives, eventing, correlation context, and the core runtime for both Process and Queryable
- [`src/Kaleido.AspNetCore`](./src/Kaleido.AspNetCore/README.md) — shared and capability-specific ASP.NET Core DI registration, middleware, and transport services
- [`src/Kaleido.Http`](./src/Kaleido.Http/README.md) — HTTP endpoint publication and route mapping for Process and Queryable
- [`src/Kaleido.Http.Abstractions`](./src/Kaleido.Http.Abstractions/README.md) — shared HTTP request/response contract types used by both server-side and client-side projects
- [`src/Kaleido.Http.Client`](./src/Kaleido.Http.Client/README.md) — typed HTTP clients for consuming remote Process and Queryable endpoints
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
- [`src/Kaleido.AspNetCore/README.md`](./src/Kaleido.AspNetCore/README.md)
- [`src/Kaleido.Http/README.md`](./src/Kaleido.Http/README.md)
- [`src/Kaleido.Http.Abstractions/README.md`](./src/Kaleido.Http.Abstractions/README.md)
- [`src/Kaleido.Http.Client/README.md`](./src/Kaleido.Http.Client/README.md)
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

### Kaleido.AspNetCore
Owns ASP.NET Core DI and transport services:
- shared exception middleware and correlation-header parsing
- `AddAspNetCore()` — consolidated ASP.NET Core DI registration for both Process and Queryable
- Queryable ASP.NET Core registration (`AddQueryableAspNetCore`) and value normalization (internal)
- Process ASP.NET Core registration (`AddProcessorAspNetCore`) and execution/state services (internal)

### Kaleido.Http
Owns HTTP endpoint publication:
- Queryable endpoint mapping (`MapQueryable`) — catalog, registry, query, and metadata endpoints
- Process endpoint mapping (`MapProcessor`) — catalog, registry, metadata, execute, and state endpoints
- Registry endpoint mapping (`MapRegistry`) — aggregated discovery combining Process and Queryable

### Kaleido.Http.Abstractions
Owns shared HTTP contract types:
- Process HTTP request/response contracts (`ExecuteProcessRequest`, `ProcessExecutionResponse`, etc.)
- Queryable HTTP request/response contracts (`QueryApiRequest`, `QueryableRecordResponse`, etc.)
- Shared contract types used by both server-side and client-side projects

### Kaleido.Http.Client
Owns typed HTTP clients:
- `IKaleidoProcessClientFactory` / `KaleidoProcessClient` for consuming remote process endpoints
- `IKaleidoQueryableClientFactory` / `KaleidoQueryableClient` for consuming remote queryable endpoints
- `AddProcessClient(...)` and `AddQueryableClient(...)` builder extensions

### Kaleido.Provider.SQLite
Owns the SQLite durable state provider:
- `UseSqliteProcessContextStore(...)` extension
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

### Exception handling
- Always use custom exceptions from `Kaleido.Exceptions` namespace, never `InvalidOperationException`
- `KaleidoFrameworkException` for framework integrity violations
- Domain-specific exceptions for domain validation errors
- Error codes are organized by domain:
  - `KaleidoErrorCodes` (in `KaleidoErrorResponse.cs`) - framework-level HTTP error codes
  - `QueryErrorCodes` (in `QueryableValidationException.cs`) - Queryable validation error codes

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
- If the concern is ASP.NET Core DI wiring or request/response transport services, it belongs in `Kaleido.AspNetCore`.
- If the concern is HTTP endpoint mapping or route generation, it belongs in `Kaleido.Http`.
- If the concern is shared HTTP contract types used by both server and client, it belongs in `Kaleido.Http.Abstractions`.
- If the concern is calling a remote Kaleido service over HTTP, it belongs in `Kaleido.Http.Client`.
- If the concern is durable process state storage via SQLite, it belongs in `Kaleido.Provider.SQLite`.
