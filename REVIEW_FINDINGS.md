# Kaleido Pre-Release Architecture & Code Review
## Consolidated Findings & Tracking Document

**Document Version:** 2.0  
**Date Created:** September 25, 2026 (v1) — **Merged/Updated:** September 25, 2026 (v2)  
**Review Scope:** Kaleido pre-1.0 release preparation  
**Status:** DRAFT — Ready for Team Review  

**v2.0 Note:** This revision merges a second, code-level analysis pass (per `docs/PRERELEASE_PROMPT.md`, phases 0–15) with the original v1 findings. Every v1 finding was re-validated against the actual source; several were corrected or superseded (see [Prior Findings — v1 Validation](#prior-findings-v1-validation)). New findings are numbered continuing the original ID scheme (CR-004+, HP-005+, MP-005+, LP-004+).

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Prior Findings — v1 Validation](#prior-findings-v1-validation)
3. [Critical Findings](#critical-findings)
4. [High Priority Findings](#high-priority-findings)
5. [Medium Priority Findings](#medium-priority-findings)
6. [Low Priority Findings](#low-priority-findings)
7. [Analyzer Proposals](#analyzer-proposals)
8. [Consolidated Breaking-Change List](#consolidated-breaking-change-list)
9. [Design Confirmations & Verified-Clean Areas](#design-confirmations--verified-clean-areas)
10. [Final Assessment](#final-assessment)
11. [Recommended Actions](#recommended-actions)
12. [Tracking & Approval](#tracking--approval)

---

## Executive Summary

### Overall Assessment

Kaleido demonstrates **strong architectural discipline**: correct project layering, clean DI boundaries, disciplined exception taxonomy, and modern C# throughout. The second-pass review found the *code* healthier than the *documentation*: the most severe problems are stale docs describing a removed project, published-contract behaviors with zero test coverage, and production-unsafe defaults that are only surfaced via one-time log warnings.

| Dimension | v1 | v2 | Status |
|-----------|----|----|--------|
| Architecture | 8/10 | 8/10 | **Strong** — verified layering is correct; two real boundary leaks found |
| Simplicity | — | 6/10 | Registries/metadata pipeline heavier than needed |
| Developer Experience | 7/10 | 5/10 | Quickstart sample does not compile; magic defaults surprise |
| API Design | — | 6/10 | Public surface mostly honest; several leaks/typos/fragile conventions |
| Maintainability | — | 6/10 | Largest duplication blocks identified; brittle test suites resist change |
| Test Quality | 7/10 | 4/10 | Published contracts (error mapping, correlation echo, cancellation) untested |
| Observability | — | 7/10 | Well-instrumented; a documented invariant is violated |
| Documentation | 8/10 | 4/10 | Root docs describe a deleted project; several documented APIs don't exist |
| Release Readiness | 6/10 | 5/10 | Docs + test-contract gaps are release blockers |

### Key Observations

**Strengths:**
- Verified-clean layering: core is transport-free; `Kaleido.Http` is thin; no circular deps
- Security posture is good: no secrets, no unsafe deserialization, parameterized EF Core only, header sanitization applied consistently
- Exception taxonomy (`Kaleido*Exception` + typed error codes) is disciplined; no raw `InvalidOperationException` in `src/`
- Modern C# throughout: primary constructors, file-scoped namespaces, sealed records, collection expressions

**Concerns:**
- Root `README.md`/`ARCHITECTURE.md` describe `Kaleido.AspNetCore`, a project that no longer exists — the quickstart does not compile (CR-004)
- Published HTTP contracts (exception→status mapping, correlation echo, cancellation signaling) have ~zero test coverage (CR-005–007)
- Two production-critical seams default to loss modes (events discarded; unbounded in-memory state) with only a one-time warning (HP-009)
- A malformed correlation header produces HTTP 500 instead of 400 (HP-007)
- The documented "one cancellation signal" invariant is violated in `ProcessRuntime` (CR-007)

### Recommendation

**Kaleido is ~75% ready for 1.0.** The v1 estimate of 85% was optimistic — it assumed docs matched code. Realistic path: fix docs + contract tests + status-code mapping (Week 1–2), then targeted API cleanups while breaking changes are still free (Week 2–3).

---

## Prior Findings — v1 Validation

Each v1 finding was re-checked against source. Statuses: **CONFIRMED**, **PARTIALLY INCORRECT**, **SUPERSEDED** (folded into a new finding), **OPEN**.

### [CR-001] HTTP Abstractions layering — **OPEN, SHARPENED**

v1 asked whether `Kaleido.Http.Abstractions` mixes transport-agnostic and HTTP-specific types. Verified facts:
- The contracts in `Http.Abstractions` genuinely reference core runtime types — they are shared HTTP wire contracts, and `Kaleido.Http.Abstractions → Kaleido` is correct per `src/AGENTS.md`.
- Three concrete boundary leaks were found (new findings): `ProcessResponseFactory` is server-side projection logic sitting in the contract assembly (MP-013); `KaleidoHttpTelemetry` forces the OTel provider to depend on the HTTP contract package (LP-010); `KaleidoClientOptions`/`KaleidoClientEntry` are HTTP client config living in transport-agnostic `Kaleido` core (HP-012).
- `src/AGENTS.md` already states the multi-transport rule: each transport gets its own project (`Kaleido.Grpc`, etc.) with its own header constants. So "multi-transport" is the documented intent; the Option-A split is unnecessary if these leaks are plugged.

**Updated recommendation:** De-scope CR-001 from "split the project" to "fix the three boundary leaks" (HP-012, MP-013, LP-010). Keep Q-001 open as a product question but it no longer blocks release.

### [CR-002] Build hardening — **PARTIALLY INCORRECT**

v1's checklist was wrong on several items. Verified state (`src/Directory.Build.props`, `Directory.Build.props`, `.gitignore`, `global.json`):
- ✅ Deterministic builds ON (`Directory.Build.props:7`)
- ✅ `GenerateDocumentationFile`, Source Link, `.snupkg`, symbol generation, license/repo metadata all present (`src/Directory.Build.props:7–23`)
- ✅ `TreatWarningsAsErrors` + `EnforceCodeStyleInBuild` set — **but only in `src/`**, not `tests/` or `tools/` (MP-024)
- ❌ No `packages.lock.json`, no `--locked-mode` restore in CI (`build.yml`) (MP-024)
- ❌ No `dotnet pack` step or release pipeline despite `IsPackable=true` (MP-024)
- ❌ No coverage gate — CI collects and uploads coverage but enforces nothing (MP-024)
- ❌ `Version`/`PackageVersion` hard-coded `1.0.0` in `src/Directory.Build.props:5-6` — no prerelease versioning strategy (folded into CR-003)

### [CR-003] Versioning & release strategy — **CONFIRMED, SHARPENED**

All six packages hard-code `1.0.0`; no GitVersion/NBGV, no CI-driven versioning. Every build produces identical `1.0.0` nupkgs — prereleases cannot be distinguished or side-by-side published. Recommendation unchanged (RELEASE.md + NBGV or CI-driven `-p:PackageVersion`), now backed by concrete evidence.

### [HP-001] Extension points lack use cases — **CONFIRMED, EXPANDED**

The extension-point audit (Phase 10) verified most seams are earned: `IProcessContextStore`, `IEventPublisher`, `IKaleidoCorrelationContextAccessor/Initializer`, `IQueryContextSource*`, `IQueryViewSource*`, `IDelegateQueryViewSource*`, `KaleidoServiceOptions.Assemblies/TypeFilter` all have clear consumers. The genuinely questionable surface is narrower: public implementations that should be internal (`EventPublisher`, `ValueConverter`, possibly `DataTypeMapper`/`ConstraintMapper` — MP-016), public registry interfaces exposed only because `Map*` resolves them cross-assembly (MP-016), and `IProcessResponseFactory` public "for test mocking" (MP-013). The documentation gap remains real (HP-001 stands).

### [HP-002] Queryable dispatch underdocumented — **PARTIALLY ADDRESSED**

`src/AGENTS.md` now documents the dispatch order as "fixed… published framework semantics" (delegated → local → direct). What remains: the precedence has **no test** (MP-019/TQ-06 — no test registers both a delegated and local view for the same type), and consumer-facing rationale/fallback docs are still absent.

### [HP-003] Analyzer rationale sparse — **CONFIRMED, EXPANDED**

Verified additional doc gaps: `KAL1010` (FixtureEmpty) and `KAL1011` (duplicate ExceptionRecord) exist in `DiagnosticIds.cs` but `docs/ANALYZERS.md` documents only KAL1001–1009. `tools/analyzers/` has no AGENTS/README at all; `tests/AGENTS.md` omits 4 of 9 test projects (MP-023).

### [HP-004] HTTP↔Runtime mapping scattered — **SUPERSEDED**

v1 described `Kaleido.AspNetCore/` containing `QueryableValueNormalizer`/`ProcessExecutionService` — that project no longer exists; these live in `src/Kaleido.Http`. The real mapping concerns found in v2: `GuardQueryAsync` duplicates `ExceptionMiddleware` (MP-007), `ProcessResponseFactory` sits in the wrong assembly (MP-013), `FieldLookup` is triplicated across a project boundary (MP-020), and `WriteResponseHeaders` duplicates the middleware's correlation echo (LP-009). Folded into those findings.

### MP-001..MP-004, LP-001..LP-003 — **UNCHANGED** (still valid as written)

MP-002 caveat: SQLite store coverage is thinner than feared — see MP-021. LP-002 partially addressed by HP-020 (validation bugs found in `ValidatePage`).

---

## Critical Findings

### [CR-004] Root documentation describes a removed project; quickstart does not compile

- **Severity:** Critical
- **Category:** Documentation / Consumer experience
- **Description:** `ARCHITECTURE.md` (lines 7–14, 40–50, 104, 117, 126, 216) lists `Kaleido.AspNetCore` as a current project with APIs (`AddAspNetCore()`, `AddQueryableAspNetCore`, `AddProcessorAspNetCore`, `UseKaleidoExceptionHandling()`); `README.md:188` uses `.AddAspNetCore()` in the Getting-Started sample and `README.md:341` links `src/Kaleido.AspNetCore/README.md`. The directory contains only stale `bin/obj` output — no csproj, no source. The real API is `AddHttp()` (`src/Kaleido.Http/KaleidoHttpServiceCollectionExtensions.cs:16`). Companion staleness: `src/Kaleido/README.md` documents `AddAssembly()`/`AddQueryable()`/`AddProcessor()` as public APIs (all internal/auto-invoked); `src/ARCHITECTURE.md` dependency graph shows `Kaleido.Http → Kaleido.AspNetCore → Kaleido`; `src/Kaleido.Http/README.md:65-70` shows an obsolete registration chain; `src/Kaleido.Provider.SQLite/README.md` includes a non-compiling sample; `src/Core/` likewise holds only bin/obj leftovers.
- **Why It Matters:** The first copy-paste a consumer tries does not compile; the architecture doc — the stated entry point — teaches wrong dependency rules.
- **Recommended Fix:** Regenerate project lists, dependency graph, and all bootstrap samples from the actual csproj graph and `AddKaleido(config, o => { o.ServiceName; o.Assemblies })` + `AddHttp()` + `Map*` model. Delete `src/Core/` and `src/Kaleido.AspNetCore/` stale directories.
- **Estimated Complexity:** Medium
- **Breaking Change:** No — docs only (+ deletion of artifact dirs)
- **Migration Impact:** None
- **Long-Term Benefit:** Trustworthy entry-point docs; accurate boundary model
- **Can Be Automated:** Partially (csproj graph → docs script; snippet-compile CI check)

---

### [CR-005] ExceptionMiddleware exception→response contract is almost untested

- **Severity:** Critical
- **Category:** Testing — missing public-contract tests
- **Description:** `tests/Kaleido.Http.UnitTests/ExceptionMiddlewareTests.cs` (78 lines) has 2 tests: pass-through and `ArgumentException` → 400. There are no tests for `KaleidoValidationException` → 400 with `Code`+`Message`, `KaleidoConfigurationException`/`KaleidoFrameworkException` → 500 (Code log-only), unknown → 500, `BadHttpRequestException`, OCE handling, or response-already-started. Only one functional test asserts a 400 end-to-end; none exercise a 500 JSON body. Per `AGENTS.md`, the status/`Code`/`Message` contract is published behavior.
- **Why It Matters:** A regression leaking a log-only `Code` or flipping a status ships silently — including the HP-007 bug already in the code.
- **Recommended Fix:** Theory-driven matrix exception type → status + deserialized `KaleidoErrorResponse` assertion; one functional 500-shape test.
- **Estimated Complexity:** Small
- **Breaking Change:** No
- **Migration Impact:** None
- **Long-Term Benefit:** Locks the published error contract
- **Can Be Automated:** Yes

---

### [CR-006] ObservabilityMiddleware correlation read/populate/echo is untested

- **Severity:** Critical
- **Category:** Testing — missing critical-path tests
- **Description:** `ObservabilityMiddlewareTests.cs` has a single test ("does not throw when initializer not registered"). The middleware's published duties — inbound header read, `IKaleidoCorrelationContextAccessor` population, `Activity` tagging, response echo — have zero tests, and no functional test asserts `X-Kaleido-*` response headers.
- **Why It Matters:** Correlation propagation is the framework's primary observability contract; a regression (lost echo, wrong header) surfaces only as production tracing gaps.
- **Recommended Fix:** Unit tests for context population + echo + Activity tags; one functional test asserting response headers.
- **Estimated Complexity:** Medium
- **Breaking Change:** No
- **Migration Impact:** None
- **Long-Term Benefit:** Guards the distributed-tracing contract end-to-end
- **Can Be Automated:** Yes

---

### [CR-007] Documented "one cancellation signal" invariant is violated — and unguarded by tests

- **Severity:** Critical
- **Category:** Observability — convention violation + missing tests
- **Description:** `src/Kaleido/Process/ProcessRuntime.cs:246-250` has `catch (OperationCanceledException) { observation.Canceled(); throw; }` at execution level while `ProcessExecutor.cs:174-176` already calls `stepObservation.Canceled()` — the documented single recording point. `AGENTS.md` explicitly forbids `Canceled()` at `ProcessRuntime`/`ProcessStepInvoker`. One disconnect ⇒ two cancellation signals + two Warning logs. Additionally no test anywhere asserts the invariant: `ProcessStepInvokerTests`' mock observability has a no-op `HandlerFailed`; `ExecutionProcessorTests` never asserts `Canceled()` once / `*Failed` never; `ProcessRuntimeTests`/`QueryContextEngineTests` have no OCE tests. Note `eventPublisher.PublishAsync(..., cancellationToken)` at `ProcessRuntime.cs:235-240` can itself throw OCE on an already-canceled token.
- **Why It Matters:** Directly contradicts a published invariant; inflates cancellation metrics and triggers false alerts; nothing prevents recurrence.
- **Recommended Fix:** Remove the `Canceled()` call in `ProcessRuntime` (the `when (ex is not OCE)` filter already propagates). Add tests asserting OCE propagates, `Canceled()` fires once, `*Failed` never fires — per engine/invoker/runtime.
- **Estimated Complexity:** Small (code) + Medium (tests)
- **Breaking Change:** No — telemetry semantics only; dashboards counting execution-level cancellations drop to zero
- **Migration Impact:** Low
- **Long-Term Benefit:** Alerting math correct; invariant locked by tests
- **Can Be Automated:** Yes

---

## High Priority Findings

### [HP-005] `UseSqliteContextStore` vs documented `UseSqliteProcessContextStore` — even the runtime warning is wrong

- **Severity:** High
- **Category:** API naming / documentation drift
- **Description:** Method is `UseSqliteContextStore` (`src/Kaleido.Provider.SQLite/SqliteProcessContextStoreServiceCollectionExtensions.cs:10`). `AGENTS.md:83`, `src/AGENTS.md:69`, both `ARCHITECTURE.md`s, and the provider README all call it `UseSqliteProcessContextStore`. The runtime warning at `src/Kaleido/Process/Context/ProcessContextStore.cs:206` also names the nonexistent method — consumers following the product's own diagnostic get a compile error.
- **Why It Matters:** The canonical "get to durable state" instruction points at a nonexistent API.
- **Recommended Fix:** Rename the method to `UseSqliteProcessContextStore` (symmetric with `IProcessContextStore`); it's a trivially mechanical public break pre-release.
- **Estimated Complexity:** Small
- **Breaking Change:** Yes — Public API rename
- **Migration Impact:** Low (one-line rename at call sites)
- **Long-Term Benefit:** Log guidance, docs, and API agree
- **Can Be Automated:** Yes

### [HP-006] Docs teach an obsolete registration model (`AddAssembly`/`AddQueryable`/`AddProcessor`)

- **Severity:** High
- **Category:** Documentation — nonexistent/internal APIs
- **Description:** `src/Kaleido/README.md` bootstrap samples show `AddKaleido().AddAssembly(...).AddQueryable().AddProcessor(...)`. `AddAssembly` is internal (`KaleidoBuilder.cs:71`); `AddProcessor`/`AddQueryable` run automatically inside `AddKaleido` (`KaleidoServiceCollectionExtensions.cs:41-42`); assemblies come from `KaleidoServiceOptions.Assemblies`. `src/AGENTS.md` and `src/ARCHITECTURE.md` still describe `AddAssembly(...)` as the model.
- **Why It Matters:** The real registration model is only discoverable by reading `AddKaleido` source.
- **Recommended Fix:** Rewrite bootstrap sections to the options-based model in all contributor and project docs.
- **Estimated Complexity:** Medium
- **Breaking Change:** No
- **Migration Impact:** None
- **Long-Term Benefit:** Docs teach the actual single entry point
- **Can Be Automated:** No

### [HP-007] Malformed correlation GUID header produces HTTP 500 instead of 400

- **Severity:** High
- **Category:** Error mapping — wrong status
- **Description:** `HttpCorrelationContextReader.ReadGuid` (`src/Kaleido.Http/Observability/HttpCorrelationContextReader.cs:56`) throws `BadHttpRequestException` inside `ObservabilityMiddleware`; `ExceptionMiddleware` (`ExceptionMiddleware.cs:118-141`) has no branch for it → generic catch → 500 `framework_error` + Error log. Found independently by three review threads (DS-01, OE-03, TD-10).
- **Why It Matters:** Bad client input pages on-call as a server failure and inflates `endpoint_errors` with `framework_error`.
- **Recommended Fix:** `catch (BadHttpRequestException)` → 400 `KaleidoErrorCodes.ArgumentError` in `ExceptionMiddleware`, or throw `KaleidoValidationException` in `ReadGuid`.
- **Estimated Complexity:** Small
- **Breaking Change:** Yes — behavioral (500→400 for malformed headers)
- **Migration Impact:** None for correct clients
- **Long-Term Benefit:** Correct status semantics; clean error budget
- **Can Be Automated:** Yes

### [HP-008] `ServiceName` doubles as route prefix; cross-service lowercase-key contract is undocumented

- **Severity:** High
- **Category:** Consumer experience — hidden coupling
- **Description:** `KaleidoServiceOptions.ServiceName` must be lowercase/no-separators and is reused as the route prefix (`ProcessEndpointRouteBuilderExtensions.cs:39,48`); `AddHttpClients` derives the remote prefix by lowercasing the client config *key* (`KaleidoHttpClientsServiceCollectionExtensions.cs:32`). Misalignment ⇒ silent 404s on every remote call. Contract only documented in `src/ARCHITECTURE.md` (a contributor doc).
- **Why It Matters:** Two services must share an invisible string convention; renaming a service is a wire-breaking change.
- **Recommended Fix:** Document the ServiceName↔key↔RoutePrefix contract in consumer-facing docs; emit a startup log per client showing resolved prefix+BaseUrl; consider decoupling `RoutePrefix` from `ServiceName`.
- **Estimated Complexity:** Medium
- **Breaking Change:** Yes if decoupled — Configuration
- **Migration Impact:** Medium
- **Long-Term Benefit:** Predictable distributed wiring
- **Can Be Automated:** No

### [HP-009] Production-unsafe defaults: all events discarded + unbounded in-memory state store

- **Severity:** High
- **Category:** Consumer experience — unsafe defaults (+ doc drift)
- **Description:** (a) Default `IEventPublisher` is `EventPublisher` (`Eventing/EventPublisher.cs:53`), which discards every event — the framework's *only* audit trail — with a one-time warning; docs still name it `NullEventPublisher` (`src/AGENTS.md:149`, `src/ARCHITECTURE.md`). (b) Default `IProcessContextStore` is an unbounded `ConcurrentDictionary` with no eviction (`ProcessContextStore.cs:193-207`), warning-only. A consumer can ship a service that loses all state on restart and all audit events forever. No health check surfaces either condition (LP-007).
- **Why It Matters:** Kaleido's value proposition is durable, observable processes; the defaults silently undermine both.
- **Recommended Fix:** Document both defaults in root README; fix `NullEventPublisher` references; consider a startup enforce option (e.g., `o.RequireDurableStore`) and/or a degraded health check.
- **Estimated Complexity:** Medium
- **Breaking Change:** Yes if enforcing — Behavioral/Configuration
- **Migration Impact:** Medium
- **Long-Term Benefit:** No silent state/audit loss
- **Can Be Automated:** No

### [HP-010] Moq 4.20.72 ships SponsorLink (supply-chain/privacy); redundant direct `SQLitePCLRaw.lib.e_sqlite3`

- **Severity:** High
- **Category:** Build / supply chain
- **Description:** `tests/Directory.Packages.props:11` pins Moq ≥ 4.20, which executes SponsorLink during build and hashes git email (moq/moq#1372). Also `Kaleido.Provider.SQLite.csproj:5` references `SQLitePCLRaw.lib.e_sqlite3` directly although EF Core Sqlite already brings `bundle_e_sqlite3` transitively — potential duplicate native-asset conflicts in consumer apps.
- **Why It Matters:** Build-time telemetry in the toolchain; native loader conflicts downstream.
- **Recommended Fix:** Pin Moq ≤ 4.18.4 or migrate to NSubstitute; drop the direct SQLitePCLRaw reference unless an override is needed (document why).
- **Estimated Complexity:** Small
- **Breaking Change:** No
- **Migration Impact:** Low
- **Long-Term Benefit:** Telemetry-free builds; clean dependency graph
- **Can Be Automated:** Yes

### [HP-011] `KaleidoClientOptions`/`KaleidoClientEntry` — HTTP config types living in transport-agnostic core

- **Severity:** High
- **Category:** Architecture — boundary leak
- **Description:** `src/Kaleido/KaleidoClientOptions.cs` defines `BaseUrl`/`RoutePrefix`/`Clients` in core, consumed only by `Kaleido.Http.Client` and abused as a DI marker in `MapRegistry` (`RegistryEndpointRouteBuilderExtensions.cs:45`).
- **Why It Matters:** Core's charter is "free of transport-specific behavior"; the leak propagates to every future transport.
- **Recommended Fix:** Move both types to `Kaleido.Http.Client` (keep `Kaleido` namespace for source compat); add an explicit internal `IKaleidoClientsRegistered` marker for `MapRegistry`.
- **Estimated Complexity:** Medium
- **Breaking Change:** Yes — types move assemblies (ABI break; source-compatible if namespace kept)
- **Migration Impact:** Recompile only
- **Long-Term Benefit:** Transport-pure core
- **Can Be Automated:** Yes

### [HP-012] `Assembly.GetCallingAssembly()` fallback is nondeterministic

- **Severity:** High
- **Category:** API correctness — magic behavior
- **Description:** `KaleidoBuilder.cs:52` / `KaleidoServiceOptions.cs:46-48` default `Assemblies` to `GetCallingAssembly()`/`GetEntryAssembly()` — JIT-inlining-sensitive without `NoInlining`; wrapping `AddKaleido` in a helper silently scans the helper's assembly. Related: `AddKaleido` mandates `IConfiguration` and silently binds `Kaleido:*`.
- **Why It Matters:** The most common "my steps aren't discovered" bug class; varies by build config.
- **Recommended Fix:** Prefer explicit `Assemblies`; if the fallback stays, add `MethodImplOptions.NoInlining` + document; consider an analyzer (LP-016, KAL2003-family).
- **Estimated Complexity:** Small–Medium
- **Breaking Change:** Possibly — Behavioral
- **Migration Impact:** Medium
- **Long-Term Benefit:** Deterministic bootstrap; explicit registration per the framework's own principle
- **Can Be Automated:** No (analyzer: partially)

### [HP-013] `AddHttpClients` silently skips misconfigured clients; factories are scoped, defeating the client cache

- **Severity:** High
- **Category:** API ergonomics — fail-slow + incorrect lifetime
- **Description:** `KaleidoHttpClientsServiceCollectionExtensions.cs:27-30` `continue`s past a client entry with no `BaseUrl` — a config typo produces a running app that fails later obscurely. Separately, both client factories + `ICorrelationHeaderStamper` are registered **scoped** (`KaleidoClientExtensions.cs:36-37`) while `KaleidoClientFactoryBase` caches clients in a per-instance dictionary — the cache never survives a scope. Base class also holds an unused `CorrelationAccessor`; `ICorrelationHeaderStamper.Sanitize` is dead API; cached clients' `IDisposable` is never invoked.
- **Why It Matters:** Violates fail-fast philosophy; the registry cache (the clients' main cost avoidance) doesn't work; stale-after-deploy caching is also undocumented (see LP-014).
- **Recommended Fix:** Throw `KaleidoConfigurationException` naming the key; register factories singleton; remove dead members; make `AddProcessClient`/`AddQueryableClient` public (currently internal — MP-026).
- **Estimated Complexity:** Small
- **Breaking Change:** Behavioral (startup throws on bad config — intended)
- **Migration Impact:** Broken configs surface immediately
- **Long-Term Benefit:** Startup correctness; working cache
- **Can Be Automated:** Mostly

### [HP-014] `ValidatePage` silently accepts invalid paging + wrong error message

- **Severity:** High
- **Category:** Validation bug
- **Description:** `QueryRequestValidator.cs:283-311`: `Page` on a non-`[Pageable]` context is silently accepted; `Size <= 0` throws a "exceeds maximum" message (wrong failure); negative `Offset` never validated.
- **Recommended Fix:** Reject `Page` when `pageable is null` (new `qry_*` code), fix message, validate `Offset >= 0`.
- **Estimated Complexity:** Small
- **Breaking Change:** Yes — behavioral (previously accepted requests → 400)
- **Migration Impact:** Low
- **Long-Term Benefit:** Validation matches documented `[Pageable]` semantics
- **Can Be Automated:** Yes

### [HP-015] Endpoint name typo: `KaleidoProcessStepREgistry`

- **Severity:** High (cheap to fix now, permanent if shipped)
- **Category:** Public contract bug
- **Description:** `Kaleido.Http.Abstractions/Process/ProcessRoutes.cs:66` — capital E in `"KaleidoProcessStepREgistry"`; emitted via `WithName()` into route metadata/OpenAPI operation IDs.
- **Recommended Fix:** Fix before release; cover with proposed analyzer KAL0020 (LP-016).
- **Estimated Complexity:** Small
- **Breaking Change:** Yes — observable endpoint name (compile-time const; C# consumers unaffected)
- **Migration Impact:** Link-by-name consumers update string
- **Can Be Automated:** Yes

### [HP-016] Step exceptions are swallowed into HTTP 200; error taxonomy lost at the boundary

- **Severity:** High
- **Category:** Exception strategy — information loss
- **Description:** `ProcessExecutor.cs:214-253` converts *any* exception (including `KaleidoValidationException`) into a step outcome `Status=Exception`; response is 200 with generic `framework_exception` text — `Code` discarded, real message replaced. Companion gap: `KaleidoHttpClientException` on the client side never reads the server `KaleidoErrorResponse` body — `Errors` is dead surface and `httpclient_validation_failed` is defined but unused.
- **Recommended Fix:** Preserve `Code` on `ProcessExecutionOutcome`/`RuntimeMessages`; on non-2xx client paths, deserialize `KaleidoErrorResponse` into `Errors` and map 400→`ValidationFailed`.
- **Estimated Complexity:** Medium
- **Breaking Change:** Possibly — response contract gains a field; client error codes change for 400s
- **Migration Impact:** Low
- **Long-Term Benefit:** Error taxonomy survives step + wire boundaries
- **Can Be Automated:** No

### [HP-017] SQLite store test coverage is thin; no concurrency coverage

- **Severity:** High
- **Category:** Testing — missing critical-path tests
- **Description:** `SqliteProcessContextStoreTests.cs` has 2 tests; round-trip asserts only `ProcessId` (steps, state, timestamps, `RequiredStep` unverified). No concurrency/update/multi-instance/cancellation tests; `tests/AGENTS.md:12` still labels the project "(placeholder)".
- **Recommended Fix:** Full-fidelity round-trip asserts; parallel save/load; update-existing; DI-lifetime validation; fix AGENTS.md label.
- **Estimated Complexity:** Medium
- **Breaking Change:** No
- **Migration Impact:** None
- **Can Be Automated:** Yes

### [HP-018] `ExecutionProcessorTests` (1,395 lines) is severely over-mocked and brittle

- **Severity:** High
- **Category:** Test maintainability
- **Description:** 5–9 hand-wired mocks per test, ~150–250 lines of setup, strict mocks, `VerifyNoOtherCalls` on five mocks, a mocked `IDisposable.Dispose` — internals-coupled assertions that resist refactoring; violates `tests/AGENTS.md` seam-testing guidance.
- **Recommended Fix:** Extract a builder/fixture; assert outcomes not call counts; drop strict/`VerifyNoOtherCalls`.
- **Estimated Complexity:** Large (file rewrite)
- **Breaking Change:** No
- **Migration Impact:** None
- **Can Be Automated:** No

---

## Medium Priority Findings

### [MP-005] `MapProcessor()` throws for query-only services; no per-runtime opt-out

- **Severity:** Medium · **Category:** Surprising failure
- **Description:** `AddKaleido` always registers both runtimes; `MapProcessor()` throws `KaleidoConfigurationException` when no steps exist, yet README shows both `Map` calls unconditionally. No `EnableProcess`/`EnableQueryable` opt-out.
- **Recommended Fix:** No-op-with-warning, or explicit enable flags in `KaleidoServiceOptions`.
- **Complexity:** Small–Medium · **Breaking:** Yes — behavioral · **Migration:** Low · **Automatable:** No

### [MP-006] Middleware pipeline installed invisibly via `IStartupFilter`; no opt-out or ordering control documented

- **Severity:** Medium · **Category:** Hidden behavior
- **Description:** `AddHttp()` registers `KaleidoStartupFilter` (`KaleidoHttpServiceCollectionExtensions.cs:25`) inserting Exception+Observability middleware with no `app.Use*` trace, no reorder seam, no opt-out.
- **Recommended Fix:** Document pipeline position; consider `AddHttp(o => o.Middleware...)` options.
- **Complexity:** Medium · **Breaking:** No (docs); Yes if options added — Configuration · **Automatable:** No

### [MP-007] `GuardQueryAsync` duplicates `ExceptionMiddleware` and bypasses its telemetry

- **Severity:** Medium · **Category:** Duplicate error handling / telemetry gap
- **Description:** `QueryableEndpointRouteBuilderExtensions.cs:418-437` catches `KaleidoValidationException` in the endpoint — identical wire shape but skips `Activity.SetStatus(Error)` and `endpoint_errors` counting (process path goes through middleware). Two pipelines for one exception type.
- **Recommended Fix:** Delete `GuardQueryAsync`; let `ExceptionMiddleware` own 400 mapping.
- **Complexity:** Small · **Breaking:** No · **Automatable:** Yes

### [MP-008] `MapRegistry` unconditionally requires both client factories + uses options type as a marker

- **Severity:** Medium · **Category:** Fragile DI contract
- **Description:** `RegistryEndpointRouteBuilderExtensions.cs:42-51,84-86` — injects both `IKaleido*ClientFactory`; throws if only one client kind registered; `KaleidoClientOptions` presence is sniffed as a lifecycle marker.
- **Recommended Fix:** Resolve via `GetService<T>` in-handler; explicit marker type (see HP-011).
- **Complexity:** Small · **Breaking:** No · **Automatable:** Yes

### [MP-009] Per-request reflection on hot paths

- **Severity:** Medium · **Category:** Performance
- **Description:** `ProcessStepInvoker.cs:86-94` — `GetMethod`+`Invoke` per step. `QueryableService.cs:123-242`, `QueryContextEngine.cs:238-247`, `DelegatedQueryViewEngine.cs:56-66` — `MakeGenericMethod`/`GetMethod`+`Invoke` per query. `DataTypeMapper.cs:144` — `new NullabilityInfoContext()` per property. `StepCandidateBuilder.cs:75-81` — JSON serialize→deserialize round-trip for already-typed in-process step objects.
- **Recommended Fix:** Cache `MethodInfo`/compiled delegates at registration; static `NullabilityInfoContext`; short-circuit `values is T`.
- **Complexity:** Medium · **Breaking:** No (subtle copy-semantics change for the step short-circuit — flag) · **Automatable:** Partially

### [MP-010] `QueryEventFactory` copies the full result set into every event — even with the no-op publisher

- **Severity:** Medium · **Category:** Performance / allocation
- **Description:** `QueryEventFactory.cs:43` — `Records = result.Results.Cast<object?>().ToArray()` on every query; pure waste with the default discarding publisher; scales with page size.
- **Recommended Fix:** Skip construction for no-op publisher or cap payload.
- **Complexity:** Medium · **Breaking:** Possibly — event contract · **Automatable:** No

### [MP-011] "Execution completed" Information signal fires on failed/canceled runs

- **Severity:** Medium · **Category:** Telemetry consistency
- **Description:** `ProcessExecutor` converts step failure/cancel into a normal return; `ProcessRuntime` then emits `ExecutionCompleted` — the single Information log claims success on a degraded run.
- **Recommended Fix:** Outcome-aware completion (`execution.status = completed|degraded|canceled` tag / failedStepCount).
- **Complexity:** Medium · **Breaking:** Additive · **Automatable:** No

### [MP-012] Client-side registry cache never invalidates; `Reset` blocks a thread-pool thread

- **Severity:** Medium · **Category:** Distributed-systems correctness
- **Description:** Clients cache the remote registry for instance lifetime — remote redeploys invisible until restart (undocumented). `HttpClientRegistryCache.Reset` does a synchronous `_lock.Wait()` (`HttpClientRegistryCache.cs:23`) that can block across an in-flight HTTP fetch.
- **Recommended Fix:** Document + consider TTL/refresh; make reset non-blocking (`Interlocked.Exchange`).
- **Complexity:** Small–Medium · **Breaking:** Possibly — `InvalidateRegistry` signature · **Automatable:** No

### [MP-013] `ProcessResponseFactory` — public server-side projection inside the shared contract assembly

- **Severity:** Medium · **Category:** Wrong project boundary / public surface
- **Description:** `Kaleido.Http.Abstractions/Process/ProcessResponseFactory.cs` consumes core runtime DTOs; public "for test mocking." Wrong layer per `src/AGENTS.md` and inconsistent with the queryable side's static `FromRegistryItem` pattern. (Merges TD-19/TD-24, AP-06.)
- **Recommended Fix:** Move to `src/Kaleido.Http` as internal statics (InternalsVisibleTo covers tests).
- **Complexity:** Small · **Breaking:** Yes — public type removal/move · **Automatable:** Yes

### [MP-014] Registry aggregation follows remote-supplied URLs without validation (SSRF surface)

- **Severity:** Medium · **Category:** Security — client trust boundary
- **Description:** `KaleidoProcessClient`/`KaleidoQueryableClient` issue requests to `MetadataUrl`/`QueryUrl`/`ExecuteUrl` verbatim from registry responses — no scheme/host check.
- **Recommended Fix:** Validate resolved URI is same-host/allowed-scheme relative to `BaseAddress`.
- **Complexity:** Small · **Breaking:** Possibly — exotic legit topologies · **Automatable:** Yes

### [MP-015] SQLite provider telemetry is invisible to `AddKaleidoInstrumentation`

- **Severity:** Medium · **Category:** Observability gap
- **Description:** `SqliteTelemetry` defines `ActivitySourceName`/`MeterName`, but `AddKaleidoInstrumentation` registers only Process/Queryable/HTTP — provider spans/meters never export.
- **Recommended Fix:** Add `AddKaleidoSqliteInstrumentation()` per the provider pattern (keeps OTel→SQLite decoupled).
- **Complexity:** Small · **Breaking:** No · **Automatable:** Yes

### [MP-016] Public-surface inconsistencies: implementations, registries, SQLite internals

- **Severity:** Medium · **Category:** Public surface area
- **Description:** Public concrete classes behind `TryAddSingleton<IX,X>` (`EventPublisher`, `ValueConverter`, `DataTypeMapper`, `ConstraintMapper`) vs the internal-implementation convention; six registry interfaces public only because `Map*` resolves them; SQLite provider exposes DbContext/entities/`SqliteTelemetry` publicly with no `DbContextOptionsBuilder` hook; `KaleidoServiceOptions.Validate` public static.
- **Recommended Fix:** Internalize implementations (or annotate registries as infrastructure); internalize SQLite surface + add options-builder overload.
- **Complexity:** Small–Medium · **Breaking:** Yes — visibility reductions · **Automatable:** Mostly

### [MP-017] Analyzer suite gaps: undocumented rules, weak rules, dead test-seam convention

- **Severity:** Medium · **Category:** Tooling/docs
- **Description:** `KAL1010`/`KAL1011` absent from ANALYZERS.md; `KAL1009` high-maintenance — recommend Info; `KAL1003` brittle to layout; `SutFixture` convention enforced by analyzers but used by ~2 test classes and undocumented in `tests/AGENTS.md`; `AnalyzerTestBase` duplicated across two test projects; `Kaleido.Analyzers` csproj `IsPackable=true` with no packaging metadata/`analyzers/dotnet/cs` layout; `Kaleido.Analyzers.UnitTests` reduced to a "moved" stub file.
- **Recommended Fix:** Update ANALYZERS.md + `tools/analyzers` guide; demote KAL1009; decide SutFixture fate (adopt broadly + document, or retire + analyzers); consolidate test harness; set `IsPackable=false` or add proper pack metadata; delete stub project.
- **Complexity:** Medium · **Breaking:** No · **Automatable:** Partially

### [MP-018] `ProcessStepRegistry` 4-pass materialization pipeline is over-engineered

- **Severity:** Medium · **Category:** Removable internal complexity
- **Description:** `TypeDefinition → Definition → Node → Slot` records + public `ProcessStepDependencyGraph` exist only to shuttle data between private methods — ~250 lines collapsible to a single `BuildRegistration`.
- **Recommended Fix:** Collapse the pipeline; verify `ProcessStepDependencyGraph` has no external consumers then internalize/delete.
- **Complexity:** Medium · **Breaking:** Yes if public record removed · **Automatable:** No

### [MP-019] Test gaps on published invariants + dead/stub test files

- **Severity:** Medium · **Category:** Testing
- **Description:** Queryable dispatch precedence never tested as an ordering; functional tests lack 500-shape/correlation-echo/cancellation/durable-store coverage; `Kaleido.Http.Abstractions` has no serialization round-trip/contract tests; `HttpHeaderSanitizerTests.cs` is a 1-line empty file (standalone — see CR note below); `tests/Kaleido.UnitTests/QueryableValueNormalizerTests.cs` empty + wrong project; `DbContextDependencyTests` resolve-but-never-execute and skip `ValidateScopes`; `KaleidoStartupFilterTests` never asserts middleware ordering; `DelegatedQueryViewEngineTests` asserts `new() != null`; brittle literal-JSON assertion in ExceptionMiddlewareTests; zero `[Theory]`s in ~610 tests.
- **Recommended Fix:** See per-item fixes in TQ findings; note the empty sanitizer test file should be filled, not deleted.
- **Complexity:** Medium · **Breaking:** No · **Automatable:** Yes

### [MP-020] Duplication blocks: `FieldLookup`×3, view-registry builders, handler-scan predicates

- **Severity:** Medium · **Category:** Duplicate code
- **Description:** Identical `FieldLookup` in `QueryRequestCompiler`, `QueryRequestValidator`, `QueryableValueNormalizer` (crosses project boundary); `IsProcessStepHandler`/`ImplementsGenericInterfaceFor`/`GetQueryViewInterface` duplicated between registration scans and registries; ~120 near-verbatim metadata-builder lines shared between `QueryViewRegistry` and `DelegatedQueryViewRegistry`.
- **Recommended Fix:** Single `FieldLookup`/`QueryMetadataBuilder` in `Kaleido/Queryable/Metadata`; route interface discovery through existing `QueryViewTypeExtensions`.
- **Complexity:** Small–Medium · **Breaking:** No (all internal) · **Automatable:** Mostly

### [MP-021] Contract-model duplications and shadowed `new` inheritance

- **Severity:** Medium · **Category:** Contract design
- **Description:** `QueryApiRequest` vs `QueryRequest`, `ExecuteProcessRequest`/`ExecuteStepRequest<T>` vs `ProcessRequest`/`ProcessorRequest` — parallel wire/domain vocabularies; `QueryableViewResponse`/`QueryableRecordResponse` re-declare inherited members with `new` making JSON shape depend on static type; `ProcessorRegistryResponse` vs `ProcessorCatalogResponse` near-duplicates; `RepeatableOptions` single-bool wrapper; `EmptyQueryViewParameters` exists only to satisfy arity.
- **Recommended Fix:** Prefer composition/self-contained wire records (Process-side pattern); consistent `*Contract` naming; flatten `RepeatableOptions`.
- **Complexity:** Medium · **Breaking:** Yes — contract shapes · **Automatable:** Partially

### [MP-022] Stale directories and review/build artifacts in repo root

- **Severity:** Medium · **Category:** Repo hygiene / release readiness
- **Description:** `src/Core/` and `src/Kaleido.AspNetCore/` contain only stale `bin/obj` (no csproj/source) yet mislead readers and tooling; root holds `kaleido-context.xml` (~1.1MB dump), `cleanup chatgpt ent.md`, `cleanup copilot.md`; `docs/archive/` holds 20+ session summaries. (`TestResults/` is gitignored — fine.)
- **Recommended Fix:** Delete stale dirs and stray artifacts or move under `docs/archive/`; gitignore the context dump.
- **Complexity:** Small · **Breaking:** No · **Automatable:** Yes

### [MP-023] `tests/AGENTS.md` missing 4 projects; `tools/analyzers` undocumented; `ERROR_CODES.md`/`ANALYZERS.md` not linked

- **Severity:** Medium · **Category:** Documentation coverage
- **Description:** tests table lists 6 of 9 projects (missing IntegrationTests + 3 analyzer test projects); `tools/analyzers` has no README/AGENTS (build/pack/test workflow undocumented); README's Documentation Map links the nonexistent AspNetCore README while omitting `docs/ERROR_CODES.md`, `docs/ANALYZERS.md`, and the OTel README; docs still name `KaleidoProcessClientException`/`KaleidoQueryableClientException` (real: `KaleidoHttpClientException`).
- **Recommended Fix:** Update tables/maps; add `tools/analyzers` guide; fix exception names.
- **Complexity:** Small · **Breaking:** No · **Automatable:** Partially

### [MP-024] Release-pipeline gaps: lock files, pack step, coverage gate, warnings-as-errors scope

- **Severity:** Medium · **Category:** Build/CI hardening (corrected CR-002 detail)
- **Description:** No `packages.lock.json`/`RestoreLockedMode` (plain `dotnet restore` in `build.yml`); no `dotnet pack`/release job despite full nupkg metadata; coverage uploaded but ungated; `TreatWarningsAsErrors`+`EnforceCodeStyleInBuild` only in `src/`.
- **Recommended Fix:** Enable lock-file restore in CI + commit locks; add pack step; coverage threshold; move warnings-as-errors to root props.
- **Complexity:** Medium · **Breaking:** No · **Automatable:** Yes

### [MP-025] Client registration is config-only; programmatic `AddProcessClient`/`AddQueryableClient` are internal

- **Severity:** Medium · **Category:** API ergonomics / discoverability
- **Description:** Public `KaleidoHttpClientOptions` has no public consumer API; `AddHttpClients` always registers both client kinds per entry regardless of downstream capabilities.
- **Recommended Fix:** Make per-kind registrations public on `IKaleidoBuilder`; optional capability flag per config entry.
- **Complexity:** Small · **Breaking:** No — additive · **Automatable:** Yes

### [MP-026] Registry endpoint always 200 with `ClientErrors`; step-name resolution diverges client vs server

- **Severity:** Medium · **Category:** Failure semantics / convention drift
- **Description:** `MapRegistry` always returns 200 — degradation invisible to health checks/gateways (deliberate but under-documented). Client resolves step names by stripping `"Step"` from `typeof(TStep).Name` (`KaleidoProcessClient.cs:218-222`) while the server uses `[ProcessStep].Name` — a custom-named step silently 404s.
- **Recommended Fix:** Document 200-with-errors contract (+ consider `?strict` mode or `Partial` flag); read the attribute in the client first.
- **Complexity:** Small · **Breaking:** Additive/bug-fix · **Automatable:** Yes

---

## Low Priority Findings

### [LP-004] Logging/telemetry micro-inconsistencies
- **Description:** `ExceptionMiddleware` logs client disconnect at Debug while convention assigns cancellations to Warning (`ExceptionMiddleware.cs:23-27` vs `ProcessObservability.cs:368`); failure `ActivityEvent`s lack `exception.*` tags — prefer `activity.AddException(ex)` (`ProcessObservability.cs:343,430,468`; `QueryableObservability.cs:247`); metric tag keys are scattered literals (`"step.name"`, `"query.view"`, `"execution.status"`) vs `kaleido.*` activity constants — centralize in `*Telemetry` classes; `[ExcludeFromCodeCoverage]` on logic-bearing `QueryableObservability.cs:54`; event-type strings are magic literals ("process.created.v1"); `WriteResponseHeaders` duplicates middleware echo (`ProcessExecutionService.cs:138-157`) — keep only server-assigned `ProcessId`.
- **Fix:** Small alignment sweep. **Complexity:** Small · **Breaking:** No · **Automatable:** Yes

### [LP-005] Dependency nits
- **Description:** `Kaleido.csproj` references full `Microsoft.Extensions.Logging` (abstractions suffice); redundant direct ProjectReferences in `Kaleido.Http.Client`/`Observability.OpenTelemetry` csprojs.
- **Fix:** Trim references. **Complexity:** Small · **Breaking:** No · **Automatable:** Yes

### [LP-006] Input-validation hardening
- **Description:** `UseSqliteContextStore` only null-checks the connection string — parse via `SqliteConnectionStringBuilder` + `KaleidoConfigurationException`.
- **Complexity:** Small · **Breaking:** Fails earlier (good) · **Automatable:** Yes

### [LP-007] No health-check story outside SQLite
- **Description:** Only the SQLite provider registers health checks; nothing surfaces "in-memory store / no-op publisher" to readiness probes.
- **Fix:** `KaleidoHealthCheck` reporting degraded for unsafe defaults (ties to HP-009). **Complexity:** Medium · **Breaking:** No · **Automatable:** No

### [LP-008] `HttpHeaderSanitizer` allocates 3× per header per request
- **Description:** `Where().ToArray()` LINQ path (`HttpHeaderSanitizer.cs:24-25`) on up to 5 headers inbound + outbound every call; common case is already-clean.
- **Fix:** Fast path scan → return original; `string.Create` only when mutating. **Complexity:** Small · **Breaking:** No · **Automatable:** No

### [LP-009] Minor code-organization nits
- **Description:** `RegistryEndpointNames` is a non-static `sealed class` (others are `static class`); `ProcessEndpointNames` public but `QueryableEndpointNames`/`QueryableRoutePaths` public in the wrong assembly while `ProcessRoutePaths` is internal — consolidate constants policy; `RouteOptionsMap` subclasses, 6-line factory interfaces, `QueryViewVisibility` warrant colocation; `ProcessStepRegistry.Helpers.cs` asymmetric partial; commented-out `FilterOperator` members (`Enumerations.cs:58-65`); `ProcessorStepRegistryItem` unsealed records; redundant `as` cast in `QueryEventFactory.cs:44`; sort-to-select-one in `ProcessExecutionService.cs:122-130` → single-pass.
- **Complexity:** Small · **Breaking:** Partial (const class shape, visibility) · **Automatable:** Yes

### [LP-010] `KaleidoHttpTelemetry` placement couples OTel provider to HTTP contracts
- **Description:** `Kaleido.Http.Abstractions/KaleidoHttpTelemetry.cs` forces `Kaleido.Observability.OpenTelemetry` to reference the HTTP contract package just for meter names.
- **Fix:** Move/split constants. **Complexity:** Small · **Breaking:** Yes — type moves assemblies · **Automatable:** Yes

### [LP-011] Per-step endpoint contract (`ExecuteStepRequest<TStep>` envelope) undocumented for non-.NET consumers
- **Description:** Per-step execute bodies use a reflection-generated envelope; no consumer doc or OpenAPI output describes it; `.WithTags("Kaleido")`/`.Produces` metadata suggests OpenAPI was intended.
- **Fix:** Contract doc or ship OpenAPI description. **Complexity:** Medium · **Breaking:** No · **Automatable:** Partially

### [LP-012] Small API-consistency fixes
- **Description:** `ProcessStepHandlerResult` non-generic record unsealed + ambiguous `Success` overloads (`Success(string? = null, params …)` vs `Success(params …)`); Process attributes unsealed vs sealed Queryable attributes; `IEventPublisher.PublishAsync<TEvent,TContext>` leaves `TContext` unconstrained (add `IKaleidoEventContext` marker); `AddHttp` descriptor-sniffs `IProcessRegistry` for conditional registration (order-fragile — register unconditionally); `IKaleidoBuilder` grew past its `Services`+`Assemblies` charter (freeze at 4 members).
- **Complexity:** Small · **Breaking:** Yes (sealing/constraint/overload — low real impact) · **Automatable:** Mostly

### [LP-013] Test-style findings
- **Description:** Zero `[Theory]` usage; literal-JSON equality assertion (`ExceptionMiddlewareTests.cs:52-54`) → deserialize + assert fields; folder↔namespace mismatches (`Query/` vs `Queryable/` tree, extra `Processor` segment); inconsistent test-class accessibility.
- **Complexity:** Small · **Breaking:** No · **Automatable:** Yes

### [LP-014] Client registry cache lifetime undocumented
- **Description:** Registry cached for client lifetime; remote redeploys invisible until restart (pairs with MP-012 mechanics).
- **Fix:** Document + recommend lifetimes/TTL. **Complexity:** Small · **Breaking:** No · **Automatable:** No

### [LP-015] `RegistryEndpointNames`/`GetAll()` dead surface + misc dead code
- **Description:** `QueryContextRegistry.GetAll()`/`QueryViewRegistry.GetAll()` public-but-not-on-interface dead methods; `ICorrelationHeaderStamper.Sanitize` never called; `CorrelationAccessor` unused in client factory base.
- **Complexity:** Small · **Breaking:** Internal only · **Automatable:** Yes

### [LP-016] Analyzer proposals — see dedicated section below

---

## Analyzer Proposals

(Phase 14 output. Existing suite assessment: keep KAL0002–0019 suite — no built-in equivalents for the DI-hygiene rules; demote KAL1009 to Info; document KAL1010/1011 — see MP-017.)

| ID Proposal | Purpose | Problem Solved | Example Violation | Severity | Impl. Complexity | Long-Term Value |
|---|---|---|---|---|---|---|
| **KAL0020** | `.WithName(...)` args must reference `*EndpointNames` consts | Convention documented but unenforced — how `KaleidoProcessStepREgistry` shipped (HP-015) | `.WithName("GetSteps")` | Warning | Small | High |
| **KAL0021** | `catch (Exception)` touching observability members must filter OCE | AGENTS.md invariant enforced only by review (see CR-007) | `catch (Exception ex) { obs.Failed(ex); }` | Warning | Medium | Medium-High |
| **KAL2001** (consumer pkg) | `[ProcessStep]`/`[QueryView]` required + non-empty Name/Version on types used as step/view generic args | Startup-time failures for compile-time-knowable facts | `IProcessStepHandler<T>` where `T` lacks `[ProcessStep]` | Error | Medium | High — flagship consumer rule |
| **KAL2002** (consumer) | Consumer step handlers: catch-all must exclude OCE | Same invariant at the consumer seam | as KAL0021 | Warning | Medium | Medium |
| **KAL2003** (consumer) | `ServiceName` literal must satisfy lowercase/no-separator rule at compile time | Startup throw for a literal-known violation | `o.ServiceName = "Intake Service"` | Warning | Small | Medium |
| **KAL2004** (consumer) | `MapRegistry()` without upstream `AddHttpClients()` in the same fluent chain | Runtime guard exists; compile-time hint | `app.MapRegistry()` alone | Info (heuristic) | Medium | Medium |

---

## Consolidated Breaking-Change List

| ID | Current | Proposed | Why Break Now | Scope | Migration |
|----|---------|----------|---------------|-------|-----------|
| HP-005 | `UseSqliteContextStore` | `UseSqliteProcessContextStore` | Docs/warning already say it | Public API | Rename call site |
| HP-007 | Malformed GUID header → 500 | → 400 | Correct semantics pre-1.0 | Behavioral | None for correct clients |
| HP-011 | `KaleidoClientOptions` in core | Move to `Kaleido.Http.Client` | Transport purity | Package (ABI) | Recompile; same namespace |
| HP-012 | `GetCallingAssembly` fallback | Require/explicit or harden | Determinism | Behavioral | Low–Med |
| HP-013 | Scoped factories / silent skip | Singleton + throw on bad config | Cache works; fail-fast | Behavioral | Bad configs surface |
| HP-014 | Lenient `Page` validation | Reject invalid paging | Matches `[Pageable]` semantics | Behavioral | Bad requests → 400 |
| HP-015 | `KaleidoProcessStepREgistry` | Fix typo | Permanent once shipped | Public API (const value) | Link-by-name users |
| HP-016 | Step exceptions → generic outcome | Preserve `Code`; client reads error body | Error taxonomy survives | Contract (additive field) | Clients gain codes |
| MP-013 | `ProcessResponseFactory` public in Abstractions | Internal statics in `Kaleido.Http` | Wrong layer | Public API | None realistic |
| MP-016 | Public impls/registries/SQLite types | Internalize (+ `DbContextOptionsBuilder` hook) | Honest surface | Public API | Recompile |
| MP-018 | 4-pass registry records + public `ProcessStepDependencyGraph` | Collapse; internalize | Simplicity | Public API (one record) | Recompile |
| MP-021 | Shadowed `new` contract records, `RepeatableOptions` | Composition + flatten | Fragile JSON semantics | Contract | Serialization review |
| LP-010 | `KaleidoHttpTelemetry` in Abstractions | Move/split | Decouple provider | Public API | Recompile |
| LP-012 | Unsealed results/attributes, unconstrained `TContext`, options-sniffing | Seal, `IKaleidoEventContext`, unconditional registration | Consistency | Public API | Trivial |
| CR-001(de-scoped) | — | Plug boundary leaks instead of splitting | See v1 validation | — | — |
| EXT-01 | `Load`/`Save` store contract | Versioned CAS + idempotency | Distributed correctness can't be bolted on | Public API/schema | High |
| EXT-02 | Save→publish as separate ops | Transactional outbox / recoverable commit | Doc-only guidance can't reconcile split commits | Public API/schema | High |
| EXT-04 | Full-payload events | Metadata-only default + payload policy | Warnings don't prevent sensitive replication | Event contract | High |
| EXT-06 | `AddKaleido` auto-enables both runtimes | Explicit `AddProcess`/`AddQueryable` (or enable flags) | Magic registration hides accepted defaults | Public API/Behavioral | Medium |
| EXT-07 | `Map*` → `IEndpointRouteBuilder` | Convention/group builders | Auth can't compose today | Public API | Low–Med |
| EXT-08 | Parallel sync/async/delegated source families + registries | One async-first contract + one catalog | Combinatorial API before proven need | Public API | High |
| EXT-09 | Interface-per-stage Process internals | Cohesive internal components | Internal — freedom pre-1.0 | Internal | Low |
| EXT-10 | `IDataTypeMapper` + `IValueConverter` overlap | One conversion service | Two systems diverge | Public API/Internal | Medium |
| EXT-11 | Mandatory `totalCount` | Optional count / `HasMore` | Two provider ops per query | Contract | Medium |
| EXT-16 | Mutable manually-bound options | `ValidateOnStart` + immutable snapshots | Convention + safety | Configuration | Low–Med |
| EXT-18 | Process/Processor, Delegate/Delegated names | One glossary | Consistency window closes at 1.0 | Public API | Medium |

All changes must update: relevant tests, `docs/ERROR_CODES.md` (if codes added), READMEs, and the HTTP contract docs together — per the "shared boundary" rule.

---

## External Review Merge (cleanup chatgpt ent.md + cleanup copilot.md)

Two independent external reviews were merged into this document. **Provenance caveat:** both were performed against `kaleido-context.xml` (a compressed Repomix export), not a live checkout — several of their claims are therefore stale (e.g., Copilot F-01 "no CI evidence" — `.github/workflows/build.yml` exists; F-06 broken doc links — partially stale). Items below were re-validated against source where feasible; unverifiable claims are marked.

### Correlation of external findings to this review

| External ID | Disposition |
|---|---|
| KAL-C01, Copilot F-02 | Partial review scope limitation — N/A here (this review used a full checkout) |
| KAL-C02 | **NEW** → EXT-01 (concurrency/idempotency on `IProcessContextStore`) |
| KAL-C03 | **NEW** → EXT-02 (state/event not atomic — no outbox) |
| KAL-C04 | **NEW, VERIFIED** → EXT-03 (sync `Count()`/`ToList()` behind async API) |
| KAL-C05 | **NEW** → EXT-04 (event payload privacy/redaction policy); perf angle = MP-010 |
| KAL-C06 | Covered by CR-004 + HP-006 + MP-023 |
| KAL-C07 | **NEW, VERIFIED** → EXT-05 (`Kaleido.Analyzers` packable but contains zero rules — only `DiagnosticIds.cs`) |
| KAL-C08 | Covered by CR-003 + MP-024 (+ adds API-compat baseline gate — folded into MP-024) |
| KAL-H01 | Covered by MP-005 (explicit capabilities) — strengthened to High via EXT-06 |
| KAL-H02 | Covered by HP-012 |
| KAL-H03 | Covered by HP-009 |
| KAL-H04 | Extends MP-013/HP-011 — adds: client interfaces also live in `Http.Abstractions` → folded into EXT-08 |
| KAL-H05 | Extends MP-008 — adds `RegistryCache` captured outside DI → folded into MP-008 |
| KAL-H06 | Extends MP-026/MP-012 — adds freshness contract (TTL/ETag/207) → folded into MP-026 |
| KAL-H07 | Covered by MP-025 |
| KAL-H08 | Covered by MP-026 (attribute-vs-suffix step names) |
| KAL-H09 | **NEW** → EXT-07 (`Map*` return `IEndpointRouteBuilder` — can't chain `RequireAuthorization`) |
| KAL-H10 | Covered by MP-016 (+~96 public types corroborates public-surface concern) |
| KAL-H11 | **NEW** → EXT-08 (collapse sync/async/delegated source families + registries into one catalog) |
| KAL-H12 | **NEW** → EXT-09 (Process interface-per-stage decomposition → cohesive components) |
| KAL-H13 | **NEW** → EXT-10 (duplicate conversion systems: `IDataTypeMapper` vs `IValueConverter`) |
| KAL-H14 | Covered by MP-009 |
| KAL-H15 | **NEW** → EXT-11 (mandatory totalCount = 2 provider ops per query) |
| KAL-H16 | Covered by HP-016 + MP-007 |
| KAL-H17 | Covered by MP-019 (+ adds OTel test gap, package smoke tests) |
| KAL-H18 | Covered by MP-017 — **but see conflicting dispositions below** |
| KAL-M01 | **NEW** → EXT-12 (inbound correlation headers trusted as audit identity) |
| KAL-M02 | **NEW** → EXT-13 (registry `refresh` fan-out unbounded/unauthenticated) |
| KAL-M03 | **NEW** → EXT-14 (`ToLower()` case-insensitive filters — collation + index defeat) |
| KAL-M04 | **NEW** → EXT-15 (SQLite deletes/recreates child rows per save) |
| KAL-M05 | Overlapping assembly scans — noted by v2 as minor; elevated here into EXT-09/HP-012 work |
| KAL-M06 | **NEW** → EXT-16 (mutable options, manual binding — use `ValidateOnStart`) |
| KAL-M07 | Covered by LP-007 |
| KAL-M08 | **NEW** → EXT-17 (offset-only paging; keyset option) — post-1.0 candidate |
| KAL-M09 | **NEW** → EXT-18 (Process/Processor glossary; `IDelegate` vs `IDelegated`; `ExecutionProcessor` in `ProcessExecutor.cs`) |
| KAL-M10 | Covered by MP-016 |
| KAL-M11 | Covered by LP-005 |
| KAL-M12 | **NEW** → EXT-19 (CS1591 suppressed + stale XML comments e.g. `/queries` vs `/queryable`) |
| KAL-M13 | Covered by MP-017 + HP-018 |
| KAL-L01–L04 | Merged into LP-009/LP-017 (mojibake in analyzer sources, file/type mismatches, csproj dup `Using` items) |
| Copilot F-03/F-04/F-05 | Folded into MP-016, EXT-08, HP-012 respectively |
| Copilot F-07 | Covered by MP-017 |
| Copilot F-08 | Covered by HP-009 |
| Copilot F-09 | Covered by HP-016 (+ their ProblemDetails direction → folded into HP-016) |
| Copilot F-10 | Covered by MP-015 + LP-007 |
| Copilot F-11 | Covered by HP-010 + MP-024 (adds SBOM/provenance to pipeline scope) |
| Copilot F-12 | Covered by MP-019 |
| Copilot F-13 | 152 `!` operators found by them vs KAL0003 ban — folded into MP-017 (rule/source mismatch) |
| Copilot F-14 | Benchmark guidance — folded into MP-009 |
| Copilot F-15 | Covered by LP-009/MP-018 |
| Copilot F-16 | DTO proliferation — covered by MP-021 |
| Copilot F-17 | Client→core coupling — covered by HP-011 + DS-03(LP-005) |
| Copilot F-18 | Covered by EXT-16 |

### Conflicting guidance to resolve in design review

- **Analyzer dispositions:** v2 review recommends keeping the KAL DI-hygiene suite; both external reviews recommend deleting/demoting several rules (KAL0003 `!`-ban, KAL0015, KAL1001–1004/1006–1009 layout rules; external reviews keep only KAL0004/0007/0009/0013/0014/1005 as errors). Decide one disposition table — see MP-017/Q-006.
- **Analyzer proposals overlap:** their KAL2001–2003 (step/query/DTO validity) ≈ our KAL2001/KAL2003; their KAL0020–0022 (dup registration, in-memory-store warning, cancellation forwarding) ≈ our KAL2004/KAL0021-family. Consolidated into one table below (AN-EXT).

### New findings from external reviews

#### [EXT-01] `IProcessContextStore` has no optimistic concurrency or idempotency contract — **CRITICAL**

- **Severity:** Critical · **Category:** Process correctness / concurrency
- **Description:** Contract is `LoadAsync`/`SaveAsync` only — no version/ETag, CAS result, lease, or conflict outcome. Two concurrent requests for the same process can both plan against the same state, execute side effects, and overwrite each other; the pre-persistence non-repeatable check doesn't close the race. No durable idempotency key/replay contract for retries. (KAL-C02)
- **Recommended Fix:** Versioned snapshot with CAS save; first-class idempotency key + durable request outcome; per-process concurrency/conflict/retry tests on every durable provider.
- **Complexity:** Large · **Breaking:** Yes — Public API/Behavioral/Package/schema · **Migration:** High · **Automatable:** Partly

#### [EXT-02] State save and event publish are not atomic — **CRITICAL**

- **Severity:** Critical · **Category:** Process reliability / eventing
- **Description:** `ExecutionProcessor` saves state then publishes the step-completed event as separate operations; no outbox/commit protocol. A publisher failure after commit leaves durable state without its audit event; retrying can duplicate effects. (KAL-C03)
- **Recommended Fix:** Transactional outbox owned by the store transaction (stable event IDs, retry status, idempotent delivery), or an explicit recoverable commit contract + recovery API; fault-injection tests around the commit boundary.
- **Complexity:** Large · **Breaking:** Yes — Public API/Behavioral/schema · **Migration:** High · **Automatable:** Partly

#### [EXT-03] Queryable async APIs run synchronous provider I/O — **CRITICAL, VERIFIED**

- **Severity:** Critical · **Category:** Correctness / scalability
- **Description:** Verified: `QueryContextExecutor.cs:15-25` — `CountAsync` returns `Task.FromResult(query.Count())`, `ToListAsync` returns `Task.FromResult(query.ToList())`. Against EF Core this is synchronous DB I/O on request threads; cancellation can't interrupt the provider call. Tests only use LINQ-to-Objects so the hazard is invisible. (KAL-C04)
- **Recommended Fix:** Provider-aware async materialization abstraction on the query source (keeps core provider-neutral), or an EF adapter package; keep sync path only for in-memory sources; load tests with a real relational provider.
- **Complexity:** Medium · **Breaking:** Possibly — source-provider contracts · **Migration:** Low–Medium · **Automatable:** Yes

#### [EXT-04] Default event payloads replicate full business records — **CRITICAL**

- **Severity:** Critical · **Category:** Security/privacy
- **Description:** Events carry process requests/responses and full query result sets (`QueryEventFactory.cs:43`); no redaction, field classification, size cap, or metadata-only mode. PII/financial fields can be duplicated into an uncontrolled pipeline. (KAL-C05; perf angle = MP-010)
- **Recommended Fix:** Metadata-only default + explicit opt-in payload policy, redaction hooks, size cap; tests proving sensitive values never enter default events/logs/tags.
- **Complexity:** Large · **Breaking:** Yes — event contract · **Migration:** High · **Automatable:** Partly

#### [EXT-05] `Kaleido.Analyzers` is a packable package containing zero rules — **HIGH, VERIFIED**

- **Severity:** High · **Category:** Packaging
- **Description:** Verified: `tools/analyzers/Kaleido.Analyzers/` contains only `DiagnosticIds.cs` and an empty `AnalyzerReleases.Unshipped.md`, yet `IsPackable=true` + `DevelopmentDependency=true`, and nothing packages `Kaleido.Analyzers.Source`/`.Testing` into `analyzers/dotnet/cs`. Published as-is it's an inert, misleading package. (KAL-C07)
- **Recommended Fix:** Decide: delete the package (repo-only tooling) or ship a deliberate consumer analyzer assembly (KAL2xxx) with proper pack layout + a pack-and-consume smoke test. Folded into MP-017 scope.
- **Complexity:** Medium · **Breaking:** Package · **Migration:** Low · **Automatable:** Yes

#### [EXT-06] `AddKaleido` implicitly enables both capabilities — elevated to High

- **Severity:** High · **Category:** API design (merges MP-005 + KAL-H01)
- **Description:** External review concurs and strengthens: implicit dual registration broadens the graph, hides accepted defaults, and prevents expressing intent. Recommended: `AddKaleido(...).AddProcess()/.AddQueryable()/.AddHttp()/.AddHttpClients()` or explicit `Enable*` options.
- **Complexity:** Medium · **Breaking:** Yes — Public API/Behavioral · **Automatable:** Partly

#### [EXT-07] `Map*` methods return `IEndpointRouteBuilder` — authorization can't compose — **HIGH**

- **Severity:** High · **Category:** Security / endpoint ergonomics
- **Description:** `MapProcessor`/`MapQueryable`/`MapRegistry` return `IEndpointRouteBuilder`, so consumers can't chain `.RequireAuthorization()`/endpoint conventions; docs recommend `MapGroup` wrapping as a workaround. Endpoints execute steps, query data, and expose registry metadata with no framework auth posture. (KAL-H09)
- **Recommended Fix:** Return `IEndpointConventionBuilder`/`RouteGroupBuilder` (or one `MapKaleido()` group); document the unauthenticated default; authorization integration tests (401/403).
- **Complexity:** Medium · **Breaking:** Yes — Public API · **Migration:** Low–Medium · **Automatable:** Yes

#### [EXT-08] Queryable surface proliferates: registries × parallel source families — **HIGH**

- **Severity:** High · **Category:** Bloat / API design
- **Description:** Separate context/local-view/delegated-view/aggregate registries + eight sync/async/generic `QuerySources.cs` shapes + client interfaces in `Http.Abstractions`. (KAL-H11, F-04, part of KAL-H04)
- **Recommended Fix:** One async-first source contract (sync via adapters) + one aggregate read-only catalog; lower-level registries internal.
- **Complexity:** Large · **Breaking:** Yes — Public API · **Migration:** High · **Automatable:** No

#### [EXT-09] Process execution decomposed into interface-per-stage — **HIGH**

- **Severity:** High · **Category:** Bloat / testability
- **Description:** Planner, candidate builder, validator, consistency checker, availability resolver, evaluator, state updater, invoker, event factory, observability — each a one-implementation internal interface; tests mirror the graph (see HP-018). (KAL-H12)
- **Recommended Fix:** Collapse into cohesive internal `ProcessPlanner`/`ProcessExecutor`/`ProcessStateMachine`; test behavior at boundaries.
- **Complexity:** Large · **Breaking:** Internal only · **Automatable:** No

#### [EXT-10] Duplicate conversion systems (`IDataTypeMapper` vs `IValueConverter`) — **HIGH**

- **Severity:** High · **Category:** Technical debt
- **Description:** Two public overlapping conversion stacks plus a third seam in `QueryableValueNormalizer` — divergence risk in formats/null/culture/error codes. (KAL-H13)
- **Recommended Fix:** One internal conversion service (non-throwing result API + throwing wrapper); keep metadata description separate.
- **Complexity:** Medium · **Breaking:** Yes if interfaces removed · **Migration:** Medium · **Automatable:** Partly

#### [EXT-11] Every query runs a mandatory `totalCount` — **HIGH**

- **Severity:** High · **Category:** Performance / contract
- **Description:** `QueryContextEngine` always counts before materializing the page — two provider ops per query; counts can exceed page-retrieval cost. (KAL-H15)
- **Recommended Fix:** Optional `IncludeTotalCount` / `HasMore` semantics; document exact-vs-omitted behavior.
- **Complexity:** Medium · **Breaking:** Yes — contract/behavioral · **Migration:** Medium · **Automatable:** Yes

#### [EXT-12] Inbound correlation headers are trusted as framework identity — **MEDIUM**

- **Severity:** Medium · **Category:** Security — correlation trust
- **Description:** Sanitized `X-Kaleido-*` values feed context, Activity tags, outbound + response headers — caller-controlled provenance (source processor/step/request identity spoofable in traces/audit). (KAL-M01)
- **Recommended Fix:** Document trust boundary; allow regenerate/reject policies for external headers; don't treat caller values as authoritative audit identity.
- **Complexity:** Medium · **Breaking:** Possibly — behavioral/config · **Automatable:** Yes

#### [EXT-13] Registry `refresh` triggers unbounded downstream fan-out — **MEDIUM**

- **Severity:** Medium · **Category:** Availability / abuse resistance
- **Description:** `?refresh` fans out to all downstream clients with no visible bounds, auth, or rate limiting; no documented request/size limits elsewhere either. (KAL-M02)
- **Recommended Fix:** Bounded fan-out defaults, protect refresh (auth/rate-limit), documented resource limits; abuse tests.
- **Complexity:** Medium · **Breaking:** Possibly · **Automatable:** Yes

#### [EXT-14] Case-insensitive filters use culture-sensitive `ToLower()` — **MEDIUM**

- **Severity:** Medium · **Category:** Queryable semantics / perf
- **Description:** `CompiledQueryApplier` builds `LOWER(member) == lowered` — embeds collation choice, can defeat index seeks, varies by provider. (KAL-M03)
- **Recommended Fix:** Explicit case-sensitivity metadata; provider-native case-insensitive translation where available.
- **Complexity:** Medium · **Breaking:** If semantics change · **Automatable:** Yes

#### [EXT-15] SQLite store rewrites all child rows per save — **MEDIUM**

- **Severity:** Medium · **Category:** Persistence perf
- **Description:** `SqliteProcessContextStore` deletes/recreates step/availability rows on every save — write amplification, lock duration, WAL growth scaling with process size. (KAL-M04)
- **Recommended Fix:** After EXT-01 semantics, compare targeted updates vs serialized snapshot column; benchmark.
- **Complexity:** Medium · **Breaking:** Possibly — schema · **Automatable:** Yes

#### [EXT-16] Mutable options objects, manual binding, manual validation — **MEDIUM**

- **Severity:** Medium · **Category:** Configuration conventions
- **Description:** `KaleidoServiceOptions` non-sealed/mutable, manually bound, registered singleton; `KaleidoHttpClientOptions` assembled separately. Unconventional for .NET consumers; mutation-after-startup possible. (KAL-M06, F-18)
- **Recommended Fix:** `AddOptions` + bind + `Validate` + `ValidateOnStart`; immutable snapshots at runtime; seal option types.
- **Complexity:** Medium · **Breaking:** Possibly — config surface · **Automatable:** Partly

#### [EXT-17] Offset-only paging; no keyset/continuation — **LOW→POST-1.0**

- **Severity:** Medium (design) / Low (urgency) · **Category:** Scalability
- **Description:** `Skip/Take` only; large offsets degrade and unstable under writes. (KAL-M08)
- **Recommended Fix:** Optional keyset/continuation contract + deterministic-sort requirement. Post-1.0 acceptable.
- **Complexity:** Large · **Breaking:** No if additive · **Automatable:** Yes

#### [EXT-18] Terminology inconsistencies (Process/Processor; Delegate/Delegated; file/type names) — **MEDIUM**

- **Severity:** Medium · **Category:** Naming/discoverability
- **Description:** `AddProcessor`/`MapProcessor` vs `IProcessRuntime`/`ProcessorContext`; `IDelegateQueryViewSource` vs `DelegatedQueryViewRegistry`; `ExecutionProcessor` lives in `ProcessExecutor.cs` (docs reference nonexistent `ExecutionProcessor.cs`). (KAL-M09, KAL-L02)
- **Recommended Fix:** Adopt a glossary pre-1.0; rename consistently; align file/type names.
- **Complexity:** Medium · **Breaking:** Yes — renames · **Migration:** Medium · **Automatable:** Partly

#### [EXT-19] XML docs generated but CS1591 suppressed; stale XML comments — **MEDIUM**

- **Severity:** Medium · **Category:** Documentation quality
- **Description:** `GenerateDocumentationFile` on, CS1591 suppressed globally; stale comments exist (`/queries` vs actual `/queryable` route text in `KaleidoServiceOptions`). (KAL-M12, F-20)
- **Recommended Fix:** Require XML docs on the retained public API after surface reduction; stale-cref/route checks.
- **Complexity:** Medium · **Breaking:** No · **Automatable:** Yes

### External-only validation matrix (do not skip)

Both external reviews emphasize — correctly — that none of the three reviews executed the code. Required before 1.0 signoff: clean restore/build/test/pack, analyzer self-host proof, `.nupkg` content inspection + consumer smoke install, public-API baseline, vulnerability/secret scans + SBOM, concurrency/idempotency tests, outbox fault injection, relational-provider async/load tests, event redaction tests, authorization tests, rolling-upgrade/registry freshness tests, OTel in-memory exporter tests, benchmarks. Tracked as MP-024/Q-007 scope.

---

## Design Confirmations & Verified-Clean Areas

**Verified clean (checked, no findings):**
- Layering: `Kaleido` has no transport deps; `Kaleido.Http` thin; no circular project deps; `Http.Abstractions → Kaleido` legitimate
- Security: no secrets/connection strings; no `BinaryFormatter`/TypeNameHandling; all SQLite via EF LINQ (no raw SQL); `HttpHeaderSanitizer` applied inbound/outbound/echo; pinned central package versions
- Cancellation handling is correct in `QueryContextEngine`, `DelegatedQueryViewEngine`, `ProcessStepInvoker`, `SqliteProcessContextStore` (explicit OCE catch or `when` filter) — the *only* violation is `ProcessRuntime` (CR-007)
- No raw `InvalidOperationException` in `src/`; no empty catches; `ExecutionProcessor` saves state with `CancellationToken.None` — correct
- `AddOpenTelemetry()`/`AddKaleidoInstrumentation()` two-tier API is well-designed; static ActivitySources/Meters correct
- Test naming convention (`Method_When_Condition_Expected`) consistently followed

**Confirmed intentional:**
- Strong DI enforcement analyzers (KAL0005–0014) — keep; KAL0014 captive-dependency is high-value
- `IProcessContextStore`/`IEventPublisher`/correlation accessor/query source interfaces — earned extension points, appropriately minimal
- `HttpRegistryCache` vs `HttpClientRegistryCache<T>` — intentionally different semantics (documented in code)
- `HttpClientRegistryCache` fetch-once vs force-refresh distinction — by design; needs doc (LP-014), not removal

---

## Final Assessment

### Top 10 Recommended Changes Before 1.0

1. **Fix the docs** — purge `Kaleido.AspNetCore` from README/ARCHITECTURE; make the quickstart compile (CR-004)
2. **Rename `UseSqliteContextStore` → `UseSqliteProcessContextStore`** — matches docs + runtime warning (HP-005)
3. **Fix `ExceptionMiddleware` mapping** — `BadHttpRequestException` → 400 (HP-007)
4. **Remove duplicate `Canceled()` in `ProcessRuntime`** + add OCE invariant tests (CR-007)
5. **Contract-test the error + correlation surfaces** (CR-005, CR-006, CR-008 sanitizer file)
6. **Fix `KaleidoProcessStepREgistry` typo** (HP-015)
7. **Fail-fast client config + singleton factories** (HP-013)
8. **Move `KaleidoClientOptions` out of core** (HP-011)
9. **Moq pin/replace + drop redundant SQLitePCLRaw** (HP-010)
10. **Versioning + lock files + pack step** (CR-003, MP-024)

### Top 10 Simplification Opportunities

1. Collapse `ProcessStepRegistry` 4-pass pipeline (MP-018)
2. Delete `GuardQueryAsync` → middleware owns 400s (MP-007)
3. Consolidate `FieldLookup`×3 + metadata builders (MP-020)
4. `ProcessResponseFactory` → internal statics (MP-013)
5. Explicit marker instead of options-sniffing in `MapRegistry`/`AddHttp` (MP-008, LP-012)
6. Flatten `RepeatableOptions`; drop `EmptyQueryViewParameters` need (MP-021)
7. Internalize public implementations + SQLite surface (MP-016)
8. Consolidate endpoint-name constants policy (LP-009)
9. Cache hot-path reflection (MP-009)
10. Retire or adopt `SutFixture` — end the half-state (MP-017)

### Top 10 Code-Deletion Opportunities

1. `src/Core/` + `src/Kaleido.AspNetCore/` stale bin/obj trees (MP-022)
2. `GuardQueryAsync` + `ValidationErrorResult` (MP-007)
3. `catch (OCE)` in `ProcessRuntime` (CR-007)
4. `QueryContextRegistry.GetAll()`/`QueryViewRegistry.GetAll()` (LP-015)
5. `ICorrelationHeaderStamper.Sanitize`, `CorrelationAccessor` base member (LP-015)
6. Empty/stub test files + `Kaleido.Analyzers.UnitTests` project (MP-019, MP-017)
7. Commented `FilterOperator` members (LP-009)
8. `kaleido-context.xml`, `cleanup *.md` root artifacts (MP-022)
9. `TypeDefinition/Definition/Node/Slot` intermediate records (MP-018)
10. Redundant package references (LP-005)

### Biggest Concerns

- **Architectural:** boundary leaks (`KaleidoClientOptions` in core, `ProcessResponseFactory` in contracts, `KaleidoHttpTelemetry` coupling) — small now, permanent after 1.0
- **Maintainability:** `ExecutionProcessorTests` brittleness + `ProcessStepRegistry` over-materialization
- **Consumer experience:** a quickstart that doesn't compile, plus silent-loss defaults (events, state)
- **Documentation:** root architecture doc describes a deleted project; several documented APIs don't exist
- **Release risk:** shipping the published contract surface (error mapping, endpoint names, correlation headers) with near-zero contract tests

### Scores (explained)

| Dimension | Score | Rationale |
|-----------|-------|-----------|
| Architecture | 8/10 | Verified layering; two real but small boundary leaks; clean provider pattern |
| Simplicity | 6/10 | Registry/metadata machinery heavier than the problem demands; shadowed contracts |
| Developer Experience | 5/10 | Non-compiling quickstart; magic defaults; hidden ServiceName contract |
| API Design | 6/10 | Mostly honest surface; typo'd const, internal-only programmatic APIs, fragile conventions |
| Maintainability | 6/10 | Duplication blocks + brittle mega-test-file; good conventions elsewhere |
| Testability | 4/10 | Strong harness investment (SutFixture, analyzers) but published contracts untested; empty test files |
| Observability | 7/10 | Thoughtful signal design; one invariant violation + minor inconsistencies |
| Documentation | 4/10 | Good contributor docs; root/consumer docs describe deleted APIs |
| Release Readiness | 5/10 | Fix docs + contract tests + two status/API bugs; then ready |

---

## Recommended Actions

### Recommended Release Blockers (must fix before 1.0)

- CR-004 (docs describe removed project; quickstart broken)
- CR-005, CR-006 (published-contract tests: error mapping, correlation echo)
- CR-007 (cancellation invariant violation + tests)
- HP-005 (SQLite method rename), HP-007 (500→400), HP-015 (endpoint-name typo)
- HP-009 (document/enforce unsafe defaults — at minimum document + fix wrong warning text)
- HP-010 (Moq/SQLitePCLRaw), CR-003 (versioning)
- **External-merge blockers:**
  - EXT-01 (process store concurrency/CAS + idempotency) — needs a design decision even if implementation is scoped
  - EXT-02 (atomic state/event commit or explicit contract) — design decision required
  - EXT-03 (sync I/O behind async query APIs — verified defect)
  - EXT-04 (event payload privacy policy — at minimum metadata-only default)
  - EXT-05 (empty packable `Kaleido.Analyzers` — delete or package correctly)
  - EXT-07 (`Map*` authorization composition — decide convention-builder return)
  - Validation evidence matrix (external reviews): clean build/test/pack + scans must run before signoff

### Recommended Pre-1.0 Improvements

- HP-006, HP-008, HP-011–HP-014, HP-016–HP-018
- MP-005–MP-016, MP-019 (test gaps), MP-020–MP-026
- Analyzer proposals KAL0020/KAL0021 (internal, cheap)

### Recommended Post-1.0 Improvements

- MP-001 (feature-based organization), MP-017 remainder, MP-018 (if `ProcessStepDependencyGraph` ships)
- LP-001–LP-015, LP-011 (OpenAPI), KAL2001–2004 (consumer analyzer package)
- Multi-transport question (Q-001) — informed by whether the boundary leaks are plugged

### Open Questions (updated)

| ID | Question | Status |
|----|----------|--------|
| Q-001 | Non-HTTP transports in the future? (Informs how far to take boundary fixes) | Open |
| Q-002 | Real-world `IEventPublisher` consumers? (If none, HP-009 enforcement is easier) | Open |
| Q-003 | Is `MapRegistry` always-200 contract deliberate? (MP-026) | Open |
| Q-004 | Should `SutFixture` be adopted broadly or retired? (MP-017) | Open |
| Q-005 | Should `Assemblies` fallback be removed entirely? (HP-012) | Open |
| Q-006 | Analyzer disposition table: keep DI-hygiene suite (v2) vs prune to correctness-only rules (external)? (MP-017) | Open |
| Q-007 | Scope of EXT-01/EXT-02 for 1.0: full CAS+outbox vs contract-design-now/implement-later? | Open — biggest decision |
| Q-008 | Ship a consumer analyzer package (KAL2xxx) or repo-only tooling? (EXT-05) | Open |

---

## Tracking & Approval

### Review Status

| Phase | Status | Date | Notes |
|-------|--------|------|-------|
| Initial Analysis (v1) | ✅ Complete | Sep 25, 2026 | Superseded where noted |
| Second-Pass Analysis (v2) | ✅ Complete | Sep 25, 2026 | This merge — code-verified |
| Design Review | ⏳ Pending | — | Schedule team meeting |
| Documentation Review | ⏳ Pending | — | After design decisions |
| Implementation | ⏳ Not Started | — | After approvals |
| QA & Testing | ⏳ Not Started | — | After implementation |
| Final Review | ⏳ Not Started | — | Before 1.0 release |

### Team Sign-Off

**Document Owner:** [TBD]  
**Technical Lead:** [TBD]  
**Product Manager:** [TBD]  

| Role | Name | Decision | Date | Notes |
|------|------|----------|------|-------|
| Architect | | [ ] Approve | | |
| Tech Lead | | [ ] Approve | | |
| Product | | [ ] Approve | | |
| QA | | [ ] Approve | | |

---

## Related Documents

- [`REVIEW_TRACKER.yaml`](./REVIEW_TRACKER.yaml) — Tracking configuration
- [`REVIEW_INTEGRATION.md`](./REVIEW_INTEGRATION.md) — Integration guide
- [`REVIEW_QUICK_REF.md`](./REVIEW_QUICK_REF.md) — Quick reference
- [`docs/PRERELEASE_PROMPT.md`](./docs/PRERELEASE_PROMPT.md) — Review spec
- [`docs/ANALYZERS.md`](./docs/ANALYZERS.md) — Analyzer rules
- [`docs/ERROR_CODES.md`](./docs/ERROR_CODES.md) — Error code reference

---

**End of Consolidated Review Findings Document (v2.0)**
