# Kaleido contributor guide

This is the repository-level contributor guide for Kaleido.

Kaleido is organized into six main framework projects:
- [`src/Kaleido`](./src/Kaleido/README.md) — foundational bootstrap, shared abstractions, metadata primitives, eventing, transport-agnostic correlation context, and the core runtime for both Process and Queryable
- [`src/Kaleido.Http`](./src/Kaleido.Http/README.md) — HTTP transport: endpoint publication, middleware pipeline (`ExceptionMiddleware`, `ObservabilityMiddleware`), correlation context reader, startup filter, and execution services
- [`src/Kaleido.Http.Abstractions`](./src/Kaleido.Http.Abstractions/README.md) — shared HTTP contract types and HTTP-specific correlation primitives (`KaleidoCorrelationHeaders`, `HttpHeaderSanitizer`) used by both server-side and client-side projects
- [`src/Kaleido.Http.Client`](./src/Kaleido.Http.Client/README.md) — typed HTTP clients for consuming remote Process and Queryable endpoints
- [`src/Kaleido.Observability.OpenTelemetry`](./src/Kaleido.Observability.OpenTelemetry/README.md) — optional OpenTelemetry provider: `AddOpenTelemetry()` on `IKaleidoBuilder` for full OTel setup (logging + tracing + metrics + OTLP), and `AddKaleidoInstrumentation()` on `TracerProviderBuilder`/`MeterProviderBuilder` for consumers managing their own OTel pipeline
- [`src/Kaleido.Provider.SQLite`](./src/Kaleido.Provider.SQLite/README.md) — reference implementation of `IProcessorContextStore` (SQLite); consumers write their own durable store

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

### Tooling
- [`tools/analyzers/README.md`](./tools/analyzers/README.md) — analyzer project roles and test locations; rule catalog in `docs/ANALYZERS.md`

### Tests and samples
- [`tests/AGENTS.md`](./tests/AGENTS.md)
- [`samples/PriorAuth/AGENTS.md`](./samples/PriorAuth/AGENTS.md)

## Project boundaries

### Kaleido (core)
Owns shared substrate concerns and capability runtimes:
- bootstrap and builder state (`AddKaleido()`, `IKaleidoBuilder`, `KaleidoServiceOptions`)
- shared metadata primitives (`DataTypeMapper`, `ConstraintMapper`)
- shared eventing abstractions and correlation context
- Queryable runtime: source/view registration, validation, dispatch, execution, observability
- Process runtime: step registration, planning, execution, state mutation, observability
- Providers abstraction (`IProcessorContextStore`)

### Kaleido.Http
Owns the full HTTP transport layer:
- `AddHttp()` — registers routing, `IHttpContextAccessor`, the middleware pipeline, and HTTP execution services
- `ExceptionMiddleware` — outermost middleware; maps exceptions to JSON error responses
- `ObservabilityMiddleware` — reads inbound correlation headers, populates `IKaleidoCorrelationContextAccessor`, tags the Activity, and echoes correlation headers on the response
- `HttpCorrelationContextReader` — reads and sanitizes inbound HTTP headers into `KaleidoCorrelationContext`
- `KaleidoStartupFilter` — registers middlewares in the correct pipeline order via `IStartupFilter`
- Queryable endpoint mapping (`MapQueryable`) — catalog, registry, query, and metadata endpoints
- Process endpoint mapping (`MapProcessor`) — catalog, registry, metadata, execute, and state endpoints
- Registry endpoint mapping (`MapRegistry`, internal) — `GET /{service}/registry` unified discovery combining Process and Queryable; `MapKaleidoHttp(o => o.AggregateRegistry = true)` fans out to `AddHttpClients()` clients

### Kaleido.Http.Abstractions
Owns shared HTTP contract types and HTTP-specific correlation primitives:
- `KaleidoCorrelationHeaders` — canonical `X-Kaleido-*` header name constants
- `HttpHeaderSanitizer` — RFC 7230-compliant sanitization of HTTP header values
- Process HTTP request/response contracts (`ExecuteProcessRequest`, `ProcessExecutionResponse`, etc.)
- Queryable HTTP request/response contracts (`QueryApiRequest`, `QueryableSourceResponse`, etc.)
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
Owns the reference `IProcessorContextStore` implementation:
- `UseSqliteProcessorContextStore(...)` extension
- SQLite-backed `IProcessorContextStore` — a worked example only; production deployments implement `IProcessorContextStore` against their own durable infrastructure

## General contributor rules

- **Kaleido is a process engine; it never makes business decisions** ([ADR 0001](./docs/decisions/0001-process-engine-not-decision-maker.md)). It applies declared rules (the step graph, declared roles/policies, structural validation) and records what happened; what to ask, who may access a case, which path to take and what wording to use belong to the implementer. New capabilities are mechanisms, never content.
- Keep concerns in the correct project.
- `Kaleido` (core) should remain free of transport-specific behavior.
- `Kaleido.Http` should adapt and publish; it should not reimplement runtime logic.
- `Kaleido.Http.Abstractions` is shared — changes here ripple into both server and client projects.
- Keep transport layers thin.
- Prefer explicit registration and discoverability over hidden behavior.
- Match documentation to the actual code and runtime behavior.
- When you change contracts or metadata semantics, review the downstream docs and tests for the affected area.
- Define response semantics before choosing a serialization format: keep distinct message streams separate and represent an empty collection consistently across transports. Options control which messages adapters populate, not JSON-specific field presence.

## Coding patterns

### Public API documentation
- Every public type and public member in `src/` must carry strong XML documentation (`<summary>`, plus `<remarks>`, `<typeparam>`, `<param>`, `<returns>`, `<exception>`, `<example>` where they add meaning). Public APIs are consumed by people, generated docs, and AI agents — the docs are part of the contract.
- Document behavior and intent, not restated signatures: what it is for, invariants, failure modes, and how it relates to neighbouring types.
- Keep docs truthful to the code — verify semantics before writing them, and update them in the same change when behavior changes.

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
- **Warning** — cancellations, downstream non-success responses, validation failures. The runtime observation that owns a cancellation (`ProcessorExecutor`, the Queryable engines) logs it at Warning once. `ExceptionMiddleware`'s client-disconnect catch stays at **Debug** so the same cancellation is not logged twice.
- **Error** — exceptions and failures only.
- Do not promote internals to Information "for visibility" — if it fires more than once per request, it belongs at Debug.

### Exception handling
- Always use custom exceptions from `Kaleido.Exceptions` namespace, never `InvalidOperationException`
- `KaleidoValidationException` — 400 Bad Request when caught by HTTP middleware; in a Process step handler, its original `Code` and `Message` become an opt-in framework message on a failed step outcome
- `KaleidoConfigurationException` — 500; startup/DI misconfiguration; `Code` is log-only, `Message` is safe to surface
- `KaleidoFrameworkException` — 500; internal integrity violation; `Code` is log-only, `Message` is safe to surface
- `KaleidoHttpClientException` — client-side only, never reaches HTTP; carries `Code`, `StatusCode`, and `Errors`
- Error codes are owned by their area — no single catalog; a new code goes in its area's class, never in a shared one. Code string values never change when a constant moves:
  - `ProcessorErrorCodes` (`Processor/ProcessorErrorCodes.cs`) — `pro_*` codes shared across Processor startup, planning, validation, and execution
  - `QueryableErrorCodes` (`Queryable/QueryableErrorCodes.cs`) — `qry_*` codes for Queryable startup registration and request validation
  - `ConfigurationErrorCodes` (`KaleidoConfigurationException.cs`) — core service-setup and authorization-settings codes only
  - `FrameworkErrorCodes` (`KaleidoFrameworkException.cs`) — cross-cutting integrity violation codes
  - `HttpClientErrorCodes` (`Kaleido.Http.Client/KaleidoClientException.cs`) — `httpclient_*` codes for remote call failures, plus the client's startup `missing_base_url`
  - `SqliteErrorCodes` (`Kaleido.Provider.SQLite`) — the provider's own codes; providers never add codes to core
  - `KaleidoErrorCodes` (`KaleidoErrorResponse.cs`) — shared HTTP error codes (`argument_error`, `framework_error`)

### OperationCanceledException and observability
Never record `OperationCanceledException` as an execution failure — it inflates error metrics and triggers false alerts.

The rule is: **one observability signal per cancellation, at the lowest level that has full context.**

- **Process:** `ProcessExecutor` is the single recording point (`stepObservation.Canceled()`). It has step name, version, and processor name, and is where state is saved on cancellation. All layers above (`ProcessStepInvoker`, `ProcessRuntime`) use `when (exception is not OperationCanceledException)` on their `catch (Exception)` blocks so OCE propagates cleanly without triggering `ExecutionFailed` or `HandlerFailed`.
- **Queryable:** `QueryContextEngine` and `DelegatedQuerySourceEngine` each call `observation.Canceled()` in an explicit `catch (OperationCanceledException)` block placed before `catch (Exception)`. These are mutually exclusive code paths (dispatched by `QueryableService`), so only one signal fires per request.
- Do **not** add `Canceled()` calls at higher levels (`ProcessRuntime`, `ProcessStepInvoker`) — you will get duplicate signals for the same cancellation event.
- KAL0021 enforces the "exclude OCE" half: a catch-all that calls an observability member without a `when (… is not OperationCanceledException)` filter or an earlier `catch (OperationCanceledException)` is reported. The one-signal-per-cancellation placement is still a review responsibility.

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

## For code generators

- Read [`docs/PATTERNS.md`](./docs/PATTERNS.md) first — it is the canonical
  convention doc (step/handler shape, sync-vs-async sources, options
  snapshots, exceptions, DI, correlation).
- The vocabulary is: **Processor** = owns/executes steps; **Process** = one
  executing instance. See `src/ARCHITECTURE.md` → Terminology.
- `docs/ANALYZERS.md` has one section per audience, each listing its rules
  and what they prevent:
  - consumer `KAL2xxx` rules, for code that uses Kaleido (services, samples);
  - source `KAL0xxx` rules, for framework code in `src/`;
  - test `KAL1xxx` rules, for `tests/`.

  Generate compliant code; never suppress KAL rules.
- Correlation invariants (one RequestId per request, echo-or-generate,
  automatic outbound propagation) are in `src/ARCHITECTURE.md`.

## Tracking work discovered mid-item

GitHub Issues in `no1ross/Kaleido` are the system of record for active and historical work. At the start of a session, read the relevant open Issue and its linked Issues/PRs; do not use old review documents or private plans as status trackers. Every new change needs an Issue and a corresponding PR. Include `Fixes #N` in new commit messages and PR bodies for the Issue(s) the change resolves.

**Write done criteria in the Issue before starting work.** Anything outside those criteria is a discovered item.

When something new surfaces while working an Issue:
- Create a separate Issue **before** discussing a fix, and link it to the discovery Issue. Record its context, done criteria and relation rather than silently widening the current PR.
- Classify the relation; never dismiss it as "unrelated":
  - `requires`: add a GitHub sub-issue; the parent cannot close until it is resolved
  - `should`: link a related Issue to consider alongside the parent, but do not block it
  - `separate`: link a related Issue for independent scheduling
- **Scope gate:** ask "do the current Issue's done criteria require this?"
  - Yes → `requires`; do it now.
  - No → log it (`should` / `separate`) and **default to deferring** it to a future PR. Pulling it in requires an explicit decision.
- Propose the minimal fix that meets the done criteria first; offer broader redesigns as separate Issues.
- Timebox discovery: if a side thread grows after a couple of exchanges, stop and record what is known in the linked Issue.
- Before completing a PR, list open related and sub-issues in its summary; do not close a parent while a required sub-issue remains open.

## Documentation rules

- Root docs should explain how the projects fit together.
- Project READMEs should explain what lives in that project specifically.
- Contributor guides should focus on invariants, boundaries, and what not to change casually.
- Framework-shaping decisions (contracts, boundaries, principles) get a decision record in [`docs/decisions/`](./docs/decisions/README.md): numbered, never rewritten, superseded by a new record when they change, and linked to their issues/PRs.

## Tests and samples

Tests and samples are important navigation aids:
- [`tests/AGENTS.md`](./tests/AGENTS.md)
- [`samples/PriorAuth/AGENTS.md`](./samples/PriorAuth/AGENTS.md)

Use samples to understand intended consumer usage.
Use tests to understand behavioral expectations and invariants.

## Build and test commands

### SDK and target framework
- **Building Kaleido needs the .NET 10 SDK** (`global.json`, `rollForward: latestFeature`). That's a requirement for framework contributors only.
- **The published packages target `net8.0`** (`Directory.Build.props`), so any consumer on .NET 8 or later can use them. We can require a newer SDK of contributors, but we can't require consumers to move.
- **These are separate settings on purpose.** Don't raise `TargetFramework` just because the SDK is newer; changing it is an explicit owner decision.

### Build
```
dotnet build Kaleido.slnx
```

### Run tests

Test projects use xUnit v3 with **Microsoft Testing Platform (MTP)**. The `--` separator after `dotnet test` is required — it signals dotnet to route through MTP instead of VSTest:

```
dotnet test Kaleido.slnx --
```

Without `--`, dotnet uses VSTest discovery which cannot see MTP projects and reports "Zero tests ran". The CI workflow (`build.yml`) already includes `--` (passing `--coverage` after it), which is why it works on GitHub but not when running `dotnet test` bare locally.

Add `--no-build` when the solution is already built:
```
dotnet test Kaleido.slnx --no-build --
```

### Per-commit quality gate

Every commit must pass this gate on its own. A later commit fixing an earlier one doesn't count.

1. **Fresh build:** `dotnet build Kaleido.slnx --no-incremental` → **0 errors, 0 warnings**.
2. **All tests pass:** `dotnet test Kaleido.slnx --no-build --` → 0 failed, with a non-zero test count.
3. **If the commit touches a UI sample:** `npm run build` (0 errors) and `npm test` in that UI folder.
4. **Record the result in the commit message body**, e.g. `Gate: build 0 errors / 0 warnings; tests 812 passed; priorauth-ui build + test OK`.

Never delete `bin`/`obj` folders inside `node_modules`; if a clean is needed, exclude `node_modules` (see below).

### When tests still report "Zero tests ran"

Stale bin/obj folders are the most common cause after a failed or partial build. Run `full_clean.cmd` from the repo root to nuke all bin/obj folders and restore, then rebuild:
```
full_clean.cmd --no-pause   # deletes bin/, obj/, .vs/, TestResults/ (not inside node_modules), restores Kaleido.slnx, runs npm ci per UI
dotnet build Kaleido.slnx
dotnet test Kaleido.slnx --no-build --
```

**Intent of `full_clean.cmd`:** reset the whole repository to a clean, ready-to-build state. It removes all build output and restores **everything**: the .NET solution *and* every Angular/npm site (`samples/PriorAuth/priorauth-ui`, `samples/kaleido-sample-ecommerce-ui`). After it runs, `dotnet build`, `dotnet test`, `npm run build` and `npm test` should all work with no extra steps. Stop any running `ng serve` first, because `npm ci` replaces `node_modules`.

## Rule of thumb

- If the concern is bootstrap, shared metadata, eventing, correlation, or the Queryable/Process runtime, it belongs in `Kaleido`.
- If the concern is HTTP transport wiring, middleware, correlation propagation, or endpoint mapping, it belongs in `Kaleido.Http`.
- If the concern is shared HTTP contract types used by both server and client, it belongs in `Kaleido.Http.Abstractions`.
- If the concern is calling a remote Kaleido service over HTTP, it belongs in `Kaleido.Http.Client`.
- If the concern is OpenTelemetry provider wiring (exporters, instrumentation, resource config), it belongs in `Kaleido.Observability.OpenTelemetry`.
- If the concern is the `IProcessorContextStore` contract or the SQLite reference implementation, it belongs in `Kaleido.Provider.SQLite`.
