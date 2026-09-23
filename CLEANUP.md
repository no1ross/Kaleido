# Kaleido Architecture Cleanup Tracker

Progress tracker for the architecture cleanup sprint. Items are ordered by tier (risk level) and grouped so each tier can land as one or more focused commits. Check items off as they are fixed and committed.

**Scope:** All High and Medium findings from the 12-category audit. Low items are listed for awareness but not required.

**Legend:** ⚠️ = wire/public-API breaking change (coordinate before merging)

---

## Tier 1 — Zero-risk automated cleanup

These are purely mechanical: delete dead files, fix unused usings, trivial syntax modernization. No behavioral change. Should all pass `dotnet build` + `dotnet test` with zero changes to logic.

### Dead file deletions
- [x] Delete `Kaleido.AspNetCore/KaleidoBuilder.cs` — dead duplicate, never instantiated
- [x] Delete `Kaleido.Http/Process/Services/ProcessMetadataService.cs` — `IProcessMetadataService` has zero impls and zero callers
- [x] Delete `Kaleido.Http/Process/Contracts/ProcessCatalogRequest.cs` — record never referenced anywhere
- [x] Delete `Kaleido/Queryable/Query/DelegatedQueryContextEngine.cs` — physically empty (0 bytes)
- [x] Delete `Kaleido/Queryable/Query/IDelegatedQueryContextEngine.cs` — physically empty (0 bytes)
- [x] Delete `Kaleido/Process/Context/StepProcessingRecord.cs` — `StepProcessingRecord` + `RequestRecord` never referenced
- [x] Delete `Kaleido/Extensions/EnumExtensions.cs` — `ToName` = `value.ToString()`, never called

### Namespace fix
- [x] Add `namespace Kaleido.Queryable;` to `Kaleido/Queryable/QueryableService.cs` (currently in global namespace) ⚠️ minor breaking for any direct `IQueryableService` reference without a using

### Code style / dead code removal
- [x] Remove empty constructor from `Kaleido/Process/Planning/StepCandidatePlanner.cs`
- [x] Remove `ValidateRequest()` method from `ProcessRuntime.cs` — body was only a duplicate null check; the method itself removed entirely
- [x] Remove dead `RegisterProcessStep` pass-through in `ProcessServiceCollectionExtensions.cs` (inlined to `RegisterHandler`)
- [~] ~~Simplify tautological type filter~~ — **KEPT**: `IsPublic||IsNestedPublic||IsNotPublic||IsNestedAssembly` is intentional; it excludes `private`/`protected` nested types. Not tautological.
- [x] Fix `InMemoryProcessContextStore.LoadAsync` — drop `async`/`await Task.FromResult`, return `Task.FromResult` directly
- [x] Rename `contexts` field → `_contexts` in `InMemoryProcessContextStore.cs`
- [x] Remove unused `sp =>` lambda params in `TryAddSingleton` factories → `_ =>`
- [~] ~~Remove `StepCandidate.GetStep<T>()`~~ — **KEPT**: part of the complete typed accessor API
- [~] ~~Remove `StepCandidate.AddWarning(...)`~~ — **KEPT**: completes the `AddInformation/AddWarning/AddError` symmetry

### Collection expression modernization
- [x] `KaleidoBuilder.cs` (core): `new[] { }.Where().ToArray()!` → `new Assembly?[]{}.OfType<Assembly>().ToArray()`
- [x] `CompiledQueryApplier.cs:452`: `new object[] { query, lambda }` → `[query, lambda]`
- [x] `QueryableService.cs`: `new object[] { ... }` → `[...]` at 3 Invoke call sites
- [x] `QueryContextEngine.cs`: `new object[] { ... }` → `[...]`

### Null-forgiving operator (`!`) → `.OfType<>()` fixes
- [x] `KaleidoBuilder.cs` (core) `ToArray()!` → `.OfType<Assembly>().ToArray()`
- [x] `KaleidoBuilder.cs` `GetName().Name!` → `?? string.Empty`

### Unused `using` directives
- [x] `Kaleido/Process/ProcessServiceCollectionExtensions.cs` — removed `Microsoft.Extensions.Configuration`
- [x] `Kaleido.Http.Client/KaleidoClientExtensions.cs` — removed `Microsoft.Extensions.Configuration`
- [x] `Kaleido/Queryable/Query/QueryContextEngine.cs` — removed `System.Linq` (covered by ImplicitUsings)
- [x] `Kaleido.Http.Client/Queryable/KaleidoQueryableClientFactory.cs` — removed `Kaleido.Http.Queryable` (already a global using)
- [x] `Kaleido.Http.Client/Process/KaleidoProcessClientFactory.cs` — removed `Kaleido.Http.Process` (already a global using)
- [x] `Kaleido.Observability.OpenTelemetry/KaleidoObservabilityOpenTelemetryExtensions.cs` — removed `Microsoft.AspNetCore.Builder`
- [~] ~~`Kaleido/Queryable/Records/QueryViewRegistry.cs` — remove `Kaleido`~~ — **KEPT**: `using Kaleido` needed for `KaleidoConfigurationException`
- [~] ~~`Kaleido/Process/IProcessRuntime.cs` — remove `Kaleido.Process.Context`~~ — **KEPT**: needed for `ProcessExecutionState`
- [~] ~~`Kaleido.Http/Process/Contracts/ProcessExecutionResponseFactory.cs` — remove `Kaleido`~~ — **KEPT**: needed for `KaleidoFrameworkException`

### EF nav property `= null!` → `required`
- [~] ~~`ProcessStepContextEntity.cs`, `ProcessRequiredStepEntity.cs`, `ProcessAvailableStepEntity.cs`~~ — **REMOVED FROM SCOPE**: `= null!` on EF Core navigation properties is the correct pattern when Fluent API enforces NOT NULL at DB level. Not a bug.

### File renames (file name must match primary type)
- [x] Rename `Kaleido/Process/ParticipantRuntime.cs` → `ProcessRuntime.cs`
- [x] Rename `Kaleido/Process/IParticipantRuntime.cs` → `IProcessRuntime.cs`
- [x] Rename `Kaleido/Process/Registry/ParticipantRegistry.cs` → `ProcessRegistry.cs`
- [x] Rename `Kaleido/Process/ParticipantServiceCollectionExtensions.cs` → `ProcessServiceCollectionExtensions.cs`

### String formatting
- [~] ~~`KaleidoServiceOptions.cs:80–82`~~ — **FALSE FLAG**: message has no interpolated values; string literal concatenation is correct here.

### Stale `InternalsVisibleTo` cleanup
- [x] `Kaleido.Http.Abstractions/AssemblyInfo.cs` — removed stale old-project-structure entries; added missing `Kaleido.Http.Abstractions.UnitTests`
- [x] `Kaleido.Http/AssemblyInfo.cs` — removed stale old-project-structure test entries

---

## Tier 2 — Low-risk correctness fixes

These fix real bugs and behavioral inconsistencies. Each item should be committed with a companion test or test update.

### Exception hierarchy fixes
- [x] `QueryContextSourceNotFoundException` — deleted; replaced by `KaleidoFrameworkException(MissingRegistration)` (500, not 400)
- [x] All legacy `*Exception` types replaced: consolidated into `KaleidoValidationException`, `KaleidoConfigurationException`, `KaleidoFrameworkException`, `KaleidoHttpClientException` — each carries a `Code` property
- [x] `QueryableValueNormalizer` — moved to `Kaleido.Http`; wraps `ValueConverter` errors with `innerException` + `OperationCanceledException` filter
- [x] `ExceptionMiddleware` — catches all four Kaleido exception types, logs `exception.Code`, returns `exception.Message` in error response body
- [x] Replace 6× `NotSupportedException` in `CompiledQueryApplier.cs` with `KaleidoValidationException` + appropriate `ValidationErrorCodes`
- [x] `StepCandidateBuilder.cs` — `NotSupportedException` catch updated to match new exception types
- [x] Remove dead `ValidationException.cs` — deleted, zero throw/catch sites confirmed

### Nullable `!` suppression → `?? throw KaleidoFrameworkException` (AGENTS.md mandate)
- [x] All reflection/registry suppressions — already resolved during exception refactor (`DelegatedQueryViewEngine`, `CompiledQueryApplier`, `QueryContextEngine`, `QueryableService`, `KaleidoClientFactoryBase`, `StepCandidatePlanner`, `StepCandidateConsistencyChecker`, both registration validators)
- [x] `Kaleido/Json/KaleidoEnumConverterFactory.cs` — file already deleted
- [x] `Kaleido/Json/ValueConverter.cs` — `ToString()!` ×7 → `?? string.Empty`; `GetString()!` ×4 → `GetJsonString` helper throwing `FormatException`
- [x] `StepCandidate.GetStep<T>()` — `Step!` → pattern match + `KaleidoFrameworkException(TypeMismatch)`
- [x] `ProcessStepHandlerResult<T>.Response` and `ExecuteStepRequest<T>.ProcessStep` — suppressions dropped (target types already nullable)
- [~] ~~`QueryableServiceCollectionExtensions.cs:59` double `GetCustomAttribute`~~ — **KEPT**: separate LINQ chains, restructuring not worth the churn

### OperationCanceledException handling
- [x] `QueryContextEngine.cs` ×2 + `DelegatedQueryViewEngine.cs` — `catch (OCE) { observation.Canceled(); throw; }` already in place
- [x] `ProcessRuntime.cs` — explicit `catch (OCE) { observation.Canceled(); throw; }` added; new `IProcessExecutionObservation.Canceled()` + `ProcessTelemetry.ExecutionCanceledEventName` for parity with Queryable
- [x] `ProcessStepInvoker.cs` — `when (exception is not OperationCanceledException)` filter; step-level canceled recorded by `ProcessExecutor` via `stepObservation.Canceled()`
- [x] `RegistryEndpointRouteBuilderExtensions.cs` — `catch (OCE) when (cancellationToken.IsCancellationRequested) { throw; }` added before generic catch in both fan-out methods

### ExceptionMiddleware hardening
- [x] Terminal `catch (Exception)` added — `LogError`, `Activity.SetStatus(Error)`, 500 `KaleidoErrorResponse`, `HasStarted` guard
- [x] `Activity.SetStatus(Error)` set on all exception branches
- [x] `KaleidoValidationException`, `KaleidoConfigurationException`, `KaleidoFrameworkException` caught with `.Code` logged and `.Message` returned in body

### Duplicate code extraction
- [x] Extract `StampCorrelationHeaders` + `SanitizeHeaderValue` to `internal static class CorrelationHeaderStamper` in `Kaleido.Http.Client` (currently copy-pasted between `KaleidoProcessClient` and `KaleidoQueryableClient`)
- [x] Extract `GetDownstreamProcessesAsync` / `GetDownstreamQueryablesAsync` to a single generic method `GetDownstreamAsync<TItem>` in `RegistryEndpointRouteBuilderExtensions.cs`
- [x] Extract the three identical `KaleidoValidationException → BadRequest` catch blocks in `QueryableEndpointRouteBuilderExtensions.cs` to a shared `GuardQueryAsync` helper
- [x] Extract `ProcessExecutionService.ExecuteAsync` shared core (~40 dup lines) to a private `ExecuteStepCoreAsync` method
- [x] Extract assembly type-scan predicate + TypeFilter guard to shared `AssemblyTypeExtensions` extension methods (`ScanTypes` / `PassesTypeFilter`) — used in both `ProcessServiceCollectionExtensions` and `QueryableServiceCollectionExtensions`
- [x] Extract `IQueryViewSource*` interface detection pattern (repeated ×5) to `QueryViewTypeExtensions` Type extension methods (`GetViewSourceInterfaces`, `GetDelegateViewSourceInterfaces`, `ImplementsGenericInterfaceFor`, …)
- [~] ~~Extract duplicate-name GroupBy validation (×3)~~ — **KEPT**: only cleanly covers 2 of 3 sites (Process's dup-check has a richer message listing offending types); marginal dedup value
- [x] Centralize observability tag-name magic strings to a `KaleidoObservabilityTags` constants class

### Observability correctness
- [x] Add correlation headers to registry-fetch HTTP call in `KaleidoProcessClient.cs` — `HttpRequestMessage` + `headerStamper.Stamp(...)` before send (was already in place; confirmed)
- [x] Same fix in `KaleidoQueryableClient.cs` — already in place; confirmed
- [x] Add `logger.LogDebug(...)` in the two 404-swallow branches in `RegistryEndpointRouteBuilderExtensions.cs` — already present in `GetDownstreamAsync`
- [x] Add `kaleido.http.endpoint_errors` counter in `ExceptionMiddleware` — new `KaleidoHttpTelemetry` (in `Kaleido.Http.Abstractions`), tagged by `error.code` + `http.status_code`, meter wired via `AddKaleidoHttpInstrumentation`
- [x] Add `ILogger` injection to `KaleidoProcessClient` and `KaleidoQueryableClient` — required `ILogger<T>` via primary ctor, factories pass through; shared `SendAsync` helper logs Debug on send, Warning on non-success
- [x] ~~Add `AddEntityFrameworkCoreInstrumentation()`~~ — **CHANGED**: not all consumers use EF; instead `AddOpenTelemetry()` gained `configureTracing`/`configureMetrics` hooks for consumer-supplied instrumentation
- [x] Add latency `Histogram<double>` instruments to `ProcessTelemetry` and `QueryableTelemetry` — `kaleido.process.execution.duration`, `kaleido.process.step.duration`, `kaleido.queryable.execution.duration` (unit `s`); recorded in observation `Dispose` via `Stopwatch.GetTimestamp`
- [x] Promote key lifecycle log messages — `ExecutionCompleted` event + `LogInformation` on `IProcessExecutionObservation` (once per request); `ProcessRegistry`/`QueryableRegistry` log "built" at `LogInformation` (once per service); context-save logged at `LogDebug` inside `IProcessContextStore` implementations (per AGENTS.md log-level policy — Information stays request-boundary minimal)

### Performance — safe caching
- [x] Cache all `GetMethods()` results in `CompiledQueryApplier.cs` as `static readonly` fields — already in place (+ `ConcurrentDictionary` caches for sort/contains)
- [x] Fix double JSON round-trip in `StepCandidateBuilder.cs` — `is JsonElement je → je.Deserialize(stepType)` fast-path already in place
- [x] Cache `ProcessStepRegistry.InitialRegistrations` — `_initialRegistrations` computed once in constructor
- [x] Fix `DataTypeMapper.Lookup` — `ConcurrentDictionary<Type, DataTypeDescriptor>` + extracted `BuildDescriptor` for enum/array/enumerable paths
- [x] Fix O(N²) state mutation in `ProcessStateUpdater` — `Reconcile` uses a name→index dictionary; `ReplaceStep` uses a plain index loop
- [~] ~~Compute `StepAvailabilityResolver.Resolve` once per step in `ExecutionProcessor`~~ — **KEPT**: passing `availableSteps` into `Evaluate` leaks plumbing through the seam; the duplicate `Resolve` is a cheap filtered scan and evaluator owning availability is more readable
- [x] Build `Dictionary<string,FieldMetadata>` once per request — per-request `FieldLookup` (metadata + name→field dict) threaded through `QueryRequestValidator`, `QueryRequestCompiler`, `QueryableValueNormalizer`

### Public API correctness
- [x] ~~Make `AddProcessClient`/`AddQueryableClient` `public`~~ — **KEPT internal**: `AddHttpClients()` (config-driven `Kaleido:Clients`) is the consumer-facing seam; stale docs corrected (README, ARCHITECTURE, AGENTS, PriorAuth HANDOFF) — plural `AddProcessClients`/`AddQueryableClients` never existed
- [~] ~~Make `MapQueryView`/`MapDelegatedQueryView` `private`~~ — **KEPT**: mapping surface intentionally granular; note the "leaks internal types" rationale was stale (registry types are all `public`)
- [~] ~~Remove unused `serviceName` param from `MapQueryView`/`MapDelegatedQueryView`~~ — **KEPT** with above
- [x] Make `ExecuteStepRequest.ToProcessRequest` `internal` — sealed the core `ProcessRequest`/`ProcessorRequest` leak through the contract assembly; sole caller covered by `InternalsVisibleTo`
- [x] Fix `ProcessStepHandlerResult<T>.HandOff` return type → `ProcessStepHandlerResult<TProcessStepResult>` — typed handlers can now `return HandOff(...)`; non-generic overload unchanged

### Dependency graph fixes
- [x] Change `Kaleido.Observability.OpenTelemetry.csproj` project reference from `Kaleido.AspNetCore` → `Kaleido` (only uses core types)
- [x] Removed `FrameworkReference Microsoft.AspNetCore.App` from `Kaleido.Observability.OpenTelemetry.csproj` (no AspNetCore types used)
- [~] ~~Add explicit `Kaleido` ProjectReference to `Kaleido.Http.csproj`~~ — **KEPT**: transitive via `Kaleido.Http.Abstractions` accepted (user decision)
- [x] Added explicit `Microsoft.Extensions.Configuration.Abstractions` + `Microsoft.Extensions.Configuration.Binder` (for `GetSection().Bind()`) to `Kaleido.Http.Client.csproj` — no longer relies on transitive
- [x] Pruned 4 dead `InternalsVisibleTo` entries in `Kaleido.Http.Client/AssemblyInfo.cs` (`Kaleido.Process.Http.Client.UnitTests`, `Kaleido.Queryable.Http.Client.UnitTests`, `Kaleido.Registry`, `Kaleido.Http` — nonexistent assemblies / no consuming project ref)

### Security guardrails
- [x] Add max filter depth check to `QueryRequestValidator.ValidateFilter` — `MaxFilterDepth = 10` guard already in place
- [x] Sanitize `RegistryClientError.Reason` — returns generic `"{clientType} registry fetch failed. See server logs for details."`; `ex.Message` detail kept in `LogWarning`
- [x] Add startup warning when `InMemoryProcessContextStore` is the registered store (it has no eviction and grows without bound in production)
- [x] Cap correlation header value lengths in `KaleidoAspNetCoreCorrelation.cs`

### Build pipeline
- [x] Add `Microsoft.SourceLink.GitHub` package reference and `EmbedUntrackedSources` to `build/packages.props`
- [x] Add `PackageLicenseExpression` (MIT), `Description`, `PackageProjectUrl`, `RepositoryType=git` to `build/packages.props` — also updated stale `RepositoryUrl` to `no1ross/Kaleido` (repo transferred)
- [x] Add `<None Include="$(MSBuildProjectDirectory)\README.md" Pack="true" PackagePath="\" />` item group to `build/packages.props` (fixes NU5039 broken readme reference)
- [~] ~~Add `NuGet.config` at repo root with explicit `nuget.org` source~~ — **KEPT** (user decision; all packages resolve from nuget.org anyway)
- [x] Fix `Radiology.csproj` — added missing `<Import Project="../../../../build/samples.props" />` and removed duplicated properties
- [x] Extend CI (`build.yml`): added `--collect:"XPlat Code Coverage"` + `--settings coverlet.runsettings` to `dotnet test`; lcov uploaded as workflow artifact (no Codecov — free tier is patch-coverage only)
- [x] Extend CI: added `dotnet pack -c Release --no-build` + `actions/upload-artifact` for nupkg/snupkg
- [x] Set `global-json-file` in CI `setup-dotnet` action to respect `global.json` SDK pin

### Test fixes (mechanical)
- [ ] Fix `Assert.Equal("Kaleido.Provider.SQLite.SqliteProcessContextStore", store.GetType().FullName)` → `Assert.IsType<SqliteProcessContextStore>(store)` in `DbContextDependencyTests.cs:65`
- [ ] Fix `StepCandidateConsistencyCheckerTests.CreateChecker` — remove dead parameter or wire dependencies through candidate registrations so the "dependency satisfied by history/candidate" tests actually exercise the scenario
- [ ] Fix `StepCandidatePlannerTests.CreatePlanner` — remove dead `dependencies` parameter; give candidate a real missing dependency
- [ ] Update `StepCandidateConsistencyCheckerTests` circular-dependency test — add `[Trait]`/comment noting this asserts current (known-broken) behavior, so a future fix isn't mistaken for a regression
- [ ] Add real assertions (or delete) the 4 no-assert observability smoke tests in `ProcessObservabilityTests` and `QueryableObservabilityTests`
- [ ] Fix `ProcessExecutionEndpointTests.PostExecute_WhenStepFails_ReturnsErrorInResponse` — add a handler that actually fails; assert on error payload
- [ ] Fix test class name mismatch: `ParticipantRuntimeTests.cs` contains `ProcessRuntimeTests`; `ParticipantServiceCollectionExtensionsTests.cs` contains `ProcessorServiceCollectionExtensionsTests`
- [ ] Remove dead `HandlerWithSequence` helper from `KaleidoProcessClientTests.cs:45`
- [ ] Fix corrupted comment characters (`G��`) in `KaleidoProcessClientTests.cs:206,261–264,431`
- [ ] Fix dead code in `KaleidoQueryableClientTests.cs:131–161` — remove abandoned `client2` and the unasserted `fetchedUrl`

---

## Tier 3 — Medium-risk refactoring

These require more careful testing. Commit each as its own focused PR.

- [ ] Add root `.editorconfig` with C# formatting and analyzer severity rules matching AGENTS.md conventions (primary ctors, collection expressions, no `!` operators)
- [ ] Add `Directory.Build.props` at repo root to replace manual per-project imports; remove per-project `<Import>` lines
- [ ] Pin all package versions (or adopt Central Package Management `Directory.Packages.props`) — replace `8.0.*` floating versions
- [ ] Add Roslyn analyzer configuration (`AnalysisLevel=latest-recommended`) to `build/packages.props`
- [ ] Replace `KaleidoClientFactoryBase<TClient,TMap>` reflection hack — introduce `IRouteOptionsMap` interface with `Options` property; constrain `TMap`; eliminate `GetProperty("Options")` reflection
- [ ] Collapse `KaleidoProcessClientRouteOptionsMap` and `KaleidoQueryableClientRouteOptionsMap` into a single `KaleidoClientRouteOptionsMap` base
- [ ] Add `EnsureRegistryAsync` generic helper — consolidate semaphore-guarded lazy-load used in both clients
- [ ] Consolidate the 6+ route/endpoint-name constant classes into a single source-of-truth per capability
- [ ] Convert `ProcessRuntime` (8-param constructor) to primary constructor syntax (AGENTS.md mandate)
- [x] Convert `KaleidoProcessClient` and `KaleidoQueryableClient` constructors to primary constructors — done; also inlined `StampCorrelationHeaders` wrapper and `registryUrl` field (single-use)
- [ ] Convert `SqliteProcessContextDbContext` to primary constructor
- [ ] Replace `KaleidoEnumConverter<T>` + factory with BCL `JsonStringEnumConverter` (verify error message compatibility first)
- [ ] Add `SqliteProcessContextStore` activity source + `ILogger` + failure counter instrumentation
- [ ] Add `KaleidoCorrelationContextAccessor` — back `_current` with `AsyncLocal<KaleidoCorrelationContext>` so context flows across `CreateScope()` boundaries in step handlers (design discussion required first)
- [ ] Add `MapHealthChecks` guidance / `SqliteProcessContextStoreHealthCheck` + `AddHealthChecks()` registration

---

## Tier 4 — Design decisions required

These touch wire format, public contracts, or require team alignment before proceeding. Do not implement without explicit decision.

- [ ] ⚠️ **BREAKING** Fix `StepExecutionOutcome.Cancelled` (British) vs `StepExecutionStatus.Canceled` (American) — JSON wire format inconsistency; coordinate with consumers and note in release notes
- [ ] ⚠️ **BREAKING** Standardize "Process" vs "Processor" terminology throughout the public API (`ProcessorRequest` → `ProcessStepInputs`, etc.)
- [ ] ⚠️ **BREAKING** `QueryApiRequest`/`QueryApiRequest<T>` — introduce common base/interface to collapse the triplicated endpoint handlers
- [ ] SQLite per-step delete/re-insert O(N²) — decide durability semantics: save only on completion (breaks per-step crash recovery) vs upsert-only changed row vs keep current behavior
- [ ] Single-implementation internal interface collapse — audit test mocks first; determine which interfaces are legitimately substitutable (`IEventPublisher`, `IProcessContextStore`) vs never-substituted
- [ ] Authorization hooks on all HTTP endpoints — design `KaleidoEndpointOptions.ConfigureEndpoint` hook pattern; bind `processId` to authenticated principal
- [ ] `IProcessStepRegistry` / `IQueryContextRegistry` etc. internalization — split public metadata view from internal engine registration types
- [ ] Convenience `ProcessRequest` factory API — `ProcessRequest.FromStep<TStep>(name, step, processId?)` on `IProcessRuntime`
- [ ] `KaleidoProcessClient` registry cache refresh — add `RefreshRegistryAsync()` to `IKaleidoProcessClient`/`IKaleidoQueryableClient`

---

## Low severity items (optional / as-you-go)

Not required, but worth cleaning up if you touch the surrounding code:

- Seal unsealed public `record` types and attributes where inheritance isn't intended
- `DataTypeMapper.GetDescriptor(Type)` → make public (currently internal while `GetDescriptor(PropertyInfo)` is public)
- [x] `NullEventPublisher` doc comment fix + startup warning when no real publisher registered
- `IProcessStateService.GetCurrentState` missing `Async` suffix
- `KaleidoEventEnvelope<TEvent,TContext>` — add `IKaleidoEventContext` marker constraint on `TContext`
- [x] `KaleidoCorrelationContext.IsEmpty` ignores `StepName` — fix to include it
- `AvailableAfterAttribute` / `AvailableUntilAttribute` consolidation
- `"application/json"` literal → `MediaTypeNames.Application.Json`
- `RegistryCache` never DI-registered — register via `TryAddSingleton` or simplify
- Process error codes not wired into runtime throws — either wire `ProcessErrorCodes.*` into `ProcessExecutor`/`StepExecutionEvaluator` or remove unused constants
- Stale doc references: `ARCHITECTURE.md`/`AGENTS.md` mention `AddQueryableAspNetCore()`/`AddProcessorAspNetCore()` which don't exist
- `UseSqliteContextStore` renamed to match documented `UseSqliteProcessContextStore`
- `EnsureRegistryAsync` `SemaphoreSlim` wait is invisible to traces — add activity tag

---

## Commit conventions

Each commit should reference the tier and finding:

```
fix(tier1): delete dead files - KaleidoBuilder duplicate, empty engines, unused records

fix(tier2): fix OCE handling in query/process engine catch blocks

fix(tier2): cache CompiledQueryApplier reflection MethodInfo as static readonly fields

fix(tier2): extract StampCorrelationHeaders to shared CorrelationHeaderStamper helper

fix(tier3): replace KaleidoClientFactoryBase reflection with IRouteOptionsMap interface
```

---

## Progress

- [x] Tier 1 complete — branch `cleanup/tier1-zero-risk-cleanup`, 9 commits, 501 tests green
- [ ] Tier 2 complete
- [ ] Tier 3 complete
- [ ] Tier 4 decisions made
