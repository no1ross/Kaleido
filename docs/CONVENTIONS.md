# Kaleido coding conventions

The coding conventions for framework code in `src/` (and, where they apply, `tools/` and `tests/`). [`AGENTS.md`](../AGENTS.md) points to each section; the build, test and per-commit gate commands are there. Moved out of the root guide so agent tooling loads it whole (#455).

## Public API documentation
- Every public type and public member in `src/` must carry strong XML documentation (`<summary>`, plus `<remarks>`, `<typeparam>`, `<param>`, `<returns>`, `<exception>`, `<example>` where they add meaning). Public APIs are consumed by people, generated docs, and AI agents — the docs are part of the contract.
- Document behavior and intent, not restated signatures: what it is for, invariants, failure modes, and how it relates to neighbouring types.
- Keep docs truthful to the code — verify semantics before writing them, and update them in the same change when behavior changes.
- Every public API change is recorded in the package's `PublicAPI.Unshipped.txt` (RS0016/RS0017 fail the build otherwise; #313), and placed in the per-package checklist of [ADR 0032](./decisions/0032-public-api-at-1-0.md) (role and decision) until 1.0. No public async method defaults its `CancellationToken` (KAL0016), and no two overloads both take optional parameters (RS0026/RS0027). See [the public API baseline](./VERSIONING.md#the-public-api-baseline).

## Primary constructors
- Use primary constructor syntax for simple dependency injection: `class MyClass(IService service)`
- Use parameter names without underscore prefix: `service` not `_service`
- Convert constructors that only do field assignments OR only have `ArgumentNullException.ThrowIfNull` calls
- Do NOT convert constructors with complex logic in the body (loops, conditionals beyond null checks)
- For nested classes in observability types, also convert to primary constructors

## Nullable suppression operators
- Avoid nullable suppression operators (`!`) where possible
- Replace `GetMethod(...)!` with explicit null checks: `GetMethod(...) ?? throw new KaleidoFrameworkException(...)`
- Use `KaleidoFrameworkException` for framework integrity violations (e.g., missing methods via reflection)
- For properties that can legitimately be null, make them nullable (`object?` instead of `object = null!`)
- Use `.OfType<T>()` to filter nulls from collections instead of `!` on each element

## Log levels
Information logs must stay minimal — treat them as the "normal operations" view an operator reads without filtering. Target no more than 2–5 Information logs per request.
- **Information** — boundary signals only: request-in/response-out equivalents (e.g. `ExecutionCompleted` — once per request) and once-per-service-lifetime events (e.g. registry built at startup). Never per-step or per-item logs.
- **Debug** — all internals: step started/completed, context saves, source/view/materialization scopes, downstream fetch details, send/receive plumbing. This is what gets enabled when investigating by correlationId / requestId / processId.
- **Warning** — cancellations, downstream non-success responses, validation failures. The runtime observation that owns a cancellation (`ProcessorExecutor`, the Queryable engines) logs it at Warning once. The exception handler's client-disconnect case (`KaleidoExceptionHandler`) stays at **Debug** so the same cancellation is not logged twice.
- **Error** — exceptions and failures only.
- **Caller errors are never Error, and are logged once.** A request the caller got wrong or must retry - validation (400), authorization (401/403), a concurrent change (409) - is answered to the caller: `KaleidoExceptionHandler` logs it once at Warning; runtime observations record it at Debug (a span event, no failure metric); only a 5xx marks the request's span as an error. The same goes for a downstream's 4xx: the Kaleido client warns once, and callers of the client (the MCP adapter) log at Debug.
- Do not promote internals to Information "for visibility" — if it fires more than once per request, it belongs at Debug.

## Exception handling
- Always use custom exceptions from `Kaleido.Exceptions` namespace, never `InvalidOperationException`
- `KaleidoValidationException` — 400 Bad Request when caught by HTTP middleware; in a Process step handler, its original `Code` and `Message` become an opt-in framework message on a failed step outcome
- `KaleidoConfigurationException` — 500; startup/DI misconfiguration; `Code` and `Message` are logged; when thrown during a request the 500 body carries the `Code` with a generic message, never the `Message` — still keep both free of sensitive data, since logs carry them
- `KaleidoFrameworkException` — 500; internal integrity violation; `Code` and `Message` are logged; the 500 body carries the `Code` with a generic message, never the `Message` — still keep both free of sensitive data, since logs carry them
- A failed span's status description is the error code, or the exception type for anything else (`ActivityExceptionExtensions.SetErrorStatus`), never `exception.Message`; the exception event (OpenTelemetry convention) carries the message and stack trace, as the Error log does
- `KaleidoHttpClientException` — client-side only, never reaches HTTP; carries `Code`, `StatusCode`, and `Errors`
- Error codes are owned by their area — no single catalog; a new code goes in its area's class, never in a shared one. Code string values never change when a constant moves:
  - `ProcessorErrorCodes` (`Processor/ProcessorErrorCodes.cs`) — `pro_*` codes shared across Processor startup, planning, validation, and execution
  - `QueryableErrorCodes` (`Queryable/QueryableErrorCodes.cs`) — `qry_*` codes for Queryable startup registration and request validation
  - `ConfigurationErrorCodes` (`KaleidoConfigurationException.cs`) — core service-setup and authorization-settings codes only
  - `FrameworkErrorCodes` (`KaleidoFrameworkException.cs`) — cross-cutting integrity violation codes
  - `HttpClientErrorCodes` (`Kaleido.Http.Client/KaleidoClientException.cs`) — `httpclient_*` codes for remote call failures and the client's startup checks (`httpclient_missing_base_url`)
  - `SqliteErrorCodes` (`Kaleido.Provider.SQLite`) — the provider's own codes; providers never add codes to core
  - `McpErrorCodes` (`Kaleido.Http.Mcp/McpErrorCodes.cs`) — `mcp_*` codes for the MCP adapter's startup and tool-call failures
  - `OpenTelemetryErrorCodes` (`Kaleido.Observability.OpenTelemetry`) — `otel_*` codes for the provider's startup failures
  - `KaleidoErrorCodes` (`Kaleido.Http.Abstractions/KaleidoErrorResponse.cs`) — shared HTTP error codes (`argument_error`, `framework_error`)

## OperationCanceledException and observability
**A cancellation is the caller's token firing, not an exception type (#517).** An
OperationCanceledException that no token of the caller raised - a downstream timeout, an HTTP client's
own timeout, a token a handler owns - is a failure, recorded and answered as one. So a cancellation is
caught with catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested), before
the catch-all that records failures; on the HTTP transport, only an aborted request
(HttpContext.RequestAborted) is answered with nothing, and any other OperationCanceledException is a 500.
Where the work runs on CancellationToken.None (finalization, outbox delivery), no cancellation of the
caller can reach it, so every exception there is a failure, logged.

Never record a cancellation as an execution failure: it inflates error metrics and triggers false alerts.
The rule is: **one observability signal per cancellation, at the lowest level that has full context.**

- **Process:** ProcessorExecutor is the single recording point (stepObservation.Canceled()); the step is recorded as Canceled and the process keeps its state (#536). ProcessorRuntime passes the caller's cancellation on unrecorded (catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }) and records any other exception as a failure.
- **Process finalization:** cancellation applies up to and during handler invocation only. A request cancelled before a step's handler runs does not invoke it; a handler that observes cancellation leaves the step Canceled. Once a handler has returned, its outcome is final: ProcessorExecutor saves the state and its StepCompleted event together with CancellationToken.None, never the request token, so a late cancellation neither discards the completion nor makes a retry repeat the handler's side effects. Saves of failure, cancellation and runtime events already use CancellationToken.None. Tested against the in-memory and SQLite stores (CancellationFinalizationTests). A failure while saving a completed step is answered as having run and recovered, never recorded as the step failing ([ADR 0034](./decisions/0034-unsaved-step-outcomes.md)).
- **Queryable:** QueryContextEngine and DelegatedQuerySourceEngine each call observation.Canceled() in a catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) placed before catch (Exception). These are mutually exclusive code paths (dispatched by QueryableService), so only one signal fires per request.
- Do **not** add Canceled() calls at higher levels (ProcessorRuntime, ProcessorStepInvoker) — you will get duplicate signals for the same cancellation event.
- KAL0018 (and KAL2004 for handlers) enforces the "exclude cancellation" half: a catch-all that calls an observability member is reported unless its filter provably excludes OperationCanceledException (read by meaning, not by name — see [docs/ANALYZERS.md](./ANALYZERS.md#cancellation-filters)) or an earlier catch (OperationCanceledException) handles the cancellations, unfiltered or filtered on a CancellationToken's IsCancellationRequested. One clause says the same: catch (Exception ex) when (!(ex is OperationCanceledException && token.IsCancellationRequested)) - the form for a catch-all that must also pass CodeQL's generic-catch rule. The one-signal-per-cancellation placement is still a review responsibility.

## Telemetry in the runtimes
The Process and Queryable runtimes emit every signal through their observability seam (`IProcessorObservability` and its observations; `IQueryableObservability`): a runtime class never creates a `Meter`, a counter or a `LoggerMessage` of its own. One class owns the activity, the logs and the metrics, so a signal is defined once, with its names in the `*Telemetry` constants. A new kind of work gets its own observation, for example the recovery of an unsaved step outcome (`BeginRecovery`, #537). Infrastructure without a seam (the event relay and outbox, the store providers) owns its instruments directly.

## Time
Kaleido uses `TimeProvider` where time is a production dependency, not where time is merely inconvenient in a test: *testability can reveal a missing abstraction, but testability alone does not justify an abstraction* (#501).
- A decision that depends on the current time - a lease, an expiry or retention, a retry or backoff, a timeout, a TTL, a circuit's break - makes the clock a dependency of the component, so it reads the injected `TimeProvider` (`time.GetUtcNow()`, `Task.Delay(delay, time, token)`, Polly's `ResiliencePipelineBuilder { TimeProvider = time }`), never `DateTimeOffset.UtcNow` (#460). KAL0021 reports a wall-clock read that is compared or used in date arithmetic.
- A timestamp that is only recorded (`CreatedUtc`, `UpdatedUtc`, `FirstExecuted`, an event's `OccurredOn`) is not a clock dependency: it reads `DateTimeOffset.UtcNow`, and its tests do not assert an exact value. Do not inject a `TimeProvider` to make such an assertion possible.
- The clock stays in the infrastructure component that owns the behavior (the outbox, idempotency, the relay, the caches); it never becomes part of the consumer programming model (handlers, steps, sources). `AddKaleido()` registers `TimeProvider.System` with `TryAdd`, as does each package registration whose services need it, so a host may register its own first.
- Tests of a time decision drive the clock with `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`, test projects only) and test the boundary one tick before, at and after it - never with real waits.

## Record conversion
- Convert immutable data containers with init-only properties to records
- Do NOT convert service classes with behavior to records
- Do NOT convert exception classes to records (they inherit from Exception)

## Collection expressions
- Use collection expressions `[]` for property initializers where type is inferred: `public ICollection<T> Items { get; } = [];`
- For local variables, keep explicit type: `var items = new List<T>();` (collection expressions without explicit type don't compile)
- For dictionaries with comparers, keep old syntax: `new Dictionary<T, U>(StringComparer.OrdinalIgnoreCase)` - collection expressions don't support custom comparers
- For dictionary initializers, use old syntax with `[key] = value` - collection expressions use `=>` which is different

## Using statements
- Use global usings where appropriate to reduce redundant using statements
- Keep using statements minimal and project-specific

## Build details

IDE0005 (unused usings) needs `GenerateDocumentationFile`, so `tests/` and `tools/` generate XML docs with CS1591 off. `dotnet format <solution> style --diagnostics IDE0005 --include tests/` fixes unused usings in bulk; IDE0005 reports one per file at a time, so build again until clean.

CI (`build.yml`) enforces a **coverage gate**: merged line coverage of the shipped `src/` assemblies (tests and analyzers excluded) must be at least the `COVERAGE_FLOOR` (95%), or the build fails. Raise the floor, never lower it. To check locally, run `./tools/test/Run-Tests.ps1 -Coverage` (cobertura files in `TestResults/`) and merge the reports with `reportgenerator` using the workflow's `-assemblyfilters`.

Never delete `bin`/`obj` folders inside `node_modules`; if a clean is needed, exclude `node_modules` (see below).

## When a test project fails to run or reports zero tests

Stale bin/obj folders are the most common cause after a failed or partial build. Run `full_clean.cmd` from the repo root to nuke all bin/obj folders and restore, then rebuild:
```
full_clean.cmd --no-pause   # deletes bin/, obj/, .vs/, TestResults/ (not inside node_modules), restores Kaleido.slnx, runs npm ci per npm project (none today)
dotnet build Kaleido.slnx
./tools/test/Run-Tests.ps1
```

**Intent of `full_clean.cmd`:** reset the whole repository to a clean, ready-to-build state. It removes all build output and restores **everything**: the .NET solution, and `npm ci` in any npm project the repository has (none since the sample UIs moved out with the samples, #95). After it runs, `dotnet build` and `./tools/test/Run-Tests.ps1` should work with no extra steps.
