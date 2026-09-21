# Kaleido Architecture Cleanup Tracker

Progress tracker for the architecture cleanup sprint. Items are ordered by tier (risk level) and grouped so each tier can land as one or more focused commits. Check items off as they are fixed and committed.

**Scope:** All High and Medium findings from the 12-category audit. Low items are listed for awareness but not required.

**Legend:** ⚠️ = wire/public-API breaking change (coordinate before merging)

---

## Tier 1 — Zero-risk automated cleanup

These are purely mechanical: delete dead files, fix unused usings, trivial syntax modernization. No behavioral change. Should all pass `dotnet build` + `dotnet test` with zero changes to logic.

### Dead file deletions
- [ ] Delete `Kaleido.AspNetCore/KaleidoBuilder.cs` — dead duplicate, never instantiated
- [ ] Delete `Kaleido.Http/Process/Services/ProcessMetadataService.cs` — `IProcessMetadataService` has zero impls and zero callers
- [ ] Delete `Kaleido.Http/Process/Contracts/ProcessCatalogRequest.cs` — record never referenced anywhere
- [ ] Delete `Kaleido/Queryable/Query/DelegatedQueryContextEngine.cs` — physically empty (0 bytes)
- [ ] Delete `Kaleido/Queryable/Query/IDelegatedQueryContextEngine.cs` — physically empty (0 bytes)
- [ ] Delete `Kaleido/Process/Context/StepProcessingRecord.cs` — `StepProcessingRecord` + `RequestRecord` never referenced
- [ ] Delete `Kaleido/Extensions/EnumExtensions.cs` — `ToName` = `value.ToString()`, never called

### Namespace fix
- [ ] Add `namespace Kaleido.Queryable;` to `Kaleido/Queryable/QueryableService.cs` (currently in global namespace) ⚠️ minor breaking for any direct `IQueryableService` reference without a using

### Code style / dead code removal
- [ ] Remove empty constructor from `Kaleido/Process/Planning/StepCandidatePlanner.cs`
- [ ] Remove redundant `ValidateRequest(request)` call in `ParticipantRuntime.cs:56` (duplicates the `ThrowIfNull` on line 54)
- [ ] Remove dead `RegisterProcessStep` pass-through in `ParticipantServiceCollectionExtensions.cs:214` (inlines to `RegisterHandler`)
- [ ] Simplify tautological type filter in `ParticipantServiceCollectionExtensions.cs:35–38` → `x.IsClass && !x.IsAbstract`
- [ ] Fix `InMemoryProcessContextStore.LoadAsync` — drop `async`/`await Task.FromResult`, return `Task.FromResult` directly
- [ ] Rename `contexts` field → `_contexts` in `InMemoryProcessContextStore.cs` (breaks `_camelCase` convention)
- [ ] Remove dead lambda parameter `sp` in `IProcessStepRegistry` factory in `ParticipantServiceCollectionExtensions.cs:74`
- [ ] Remove `StepCandidate.GetStep<T>()` — never called; all consumers access `.Step` directly
- [ ] Remove `StepCandidate.AddWarning(...)` — never called anywhere

### Collection expression modernization
- [ ] `KaleidoBuilder.cs` (core): `new[] { Assembly.GetCallingAssembly(), ... }` → `[...]`
- [ ] `KaleidoBuilder.cs` (AspNetCore — before deletion): already covered by dead file deletion above
- [ ] `CompiledQueryApplier.cs:452`: `new object[] { query, lambda }` → `[query, lambda]`
- [ ] `QueryableService.cs:130,164,253`: `new object[] { ... }` → `[...]`
- [ ] `QueryContextEngine.cs:233`: `new object[] { ... }` → `[...]`

### Null-forgiving operator (`!`) → `.OfType<>()` fixes
- [ ] `KaleidoBuilder.cs` (core) `ToArray()!` → `.OfType<Assembly>().ToArray()`
- [ ] `KaleidoBuilder.cs` `GetName().Name!` → `?? string.Empty` or `?? throw`

### Unused `using` directives (11 files)
- [ ] `Kaleido/Process/ParticipantServiceCollectionExtensions.cs` — remove `Microsoft.Extensions.Configuration`
- [ ] `Kaleido.Http.Client/KaleidoClientExtensions.cs` — remove `Microsoft.Extensions.Configuration`
- [ ] `Kaleido/Queryable/Query/QueryContextEngine.cs` — remove `System.Linq` (covered by ImplicitUsings)
- [ ] `Kaleido/Queryable/Records/QueryViewRegistry.cs` — remove `Kaleido`
- [ ] `Kaleido/Process/IParticipantRuntime.cs` — remove `Kaleido.Process.Context`
- [ ] `Kaleido.Http/Process/Contracts/ProcessExecutionResponseFactory.cs` — remove `Kaleido`
- [ ] `Kaleido.Http.Client/Queryable/KaleidoQueryableClientFactory.cs` — remove `Kaleido.Http.Queryable` (already a global using)
- [ ] `Kaleido.Http.Client/Process/KaleidoProcessClientFactory.cs` — remove `Kaleido.Http.Process` (already a global using)
- [ ] `Kaleido.Observability.OpenTelemetry/KaleidoObservabilityOpenTelemetryExtensions.cs` — remove `Microsoft.AspNetCore.Builder` (verify at compile)

### EF nav property `= null!` → `required`
- [ ] `ProcessStepContextEntity.cs:48` — `Context { get; set; } = null!` → `required`
- [ ] `ProcessRequiredStepEntity.cs:27` — same pattern
- [ ] `ProcessAvailableStepEntity.cs:27` — same pattern

### File renames (file name must match primary type)
- [ ] Rename `Kaleido/Process/ParticipantRuntime.cs` → `ProcessRuntime.cs`
- [ ] Rename `Kaleido/Process/IParticipantRuntime.cs` → `IProcessRuntime.cs`
- [ ] Rename `Kaleido/Process/Registry/ParticipantRegistry.cs` → `ProcessRegistry.cs`
- [ ] Rename `Kaleido/Process/ParticipantServiceCollectionExtensions.cs` → `ProcessServiceCollectionExtensions.cs`

### String formatting
- [ ] `KaleidoServiceOptions.cs:80–82` — multi-line `+` concat in exception message → single interpolated string

### Stale `InternalsVisibleTo` cleanup
- [ ] `Kaleido.Http.Abstractions/AssemblyInfo.cs` — remove obsolete assembly name entries (`Kaleido.Process.AspNetCore`, `Kaleido.Queryable.AspNetCore`, `Kaleido.Process.Http.Client`, etc.); add missing `Kaleido.Http.Abstractions.UnitTests` entry
- [ ] `Kaleido.Http/AssemblyInfo.cs` — same audit pass

---

## Tier 2 — Low-risk correctness fixes

These fix real bugs and behavioral inconsistencies. Each item should be committed with a companion test or test update.

### Exception hierarchy fixes
- [ ] `QueryContextSourceNotFoundException` — move out of `QueryableValidationException` hierarchy; make it `KaleidoFrameworkException` (server DI misconfiguration → 500, not 400) ⚠️ changes HTTP status from 400 to 500 for this error
- [ ] Add `protected QueryableValidationException(string code, string message, Exception innerException)` constructor to base
- [ ] `NamedQueryRequiredException` — change error code from `QueryErrorCodes.NamedQueryNotAllowed` → `QueryErrorCodes.NamedQueryRequired`
- [ ] `ValueConversionException` — change error code from `QueryErrorCodes.InvalidParameterType` → `QueryErrorCodes.InvalidParameterValue` (or merge/remove — zero throw sites found)
- [ ] Replace 6× `NotSupportedException` in `CompiledQueryApplier.cs` with appropriate domain exceptions (`UnsupportedOperatorException`, `UnsupportedMatchModeException`, etc.)
- [ ] Update `StepCandidateBuilder.cs:96` `catch (... is NotSupportedException)` in tandem with above
- [ ] Remove dead `ValidationException.cs` (or wire into actual validation paths) — zero throw/catch sites
- [ ] `QueryableValueNormalizer.cs:45–51,146–152` — pass caught `exception` as `innerException` when rethrowing domain exceptions; add `when (exception is not OperationCanceledException)` filter

### Nullable `!` suppression → `?? throw KaleidoFrameworkException` (AGENTS.md mandate)
- [ ] `Kaleido/Queryable/Query/DelegatedQueryViewEngine.cs:58` — `GetMethod(...)!`
- [ ] `Kaleido/Queryable/Runtime/CompiledQueryApplier.cs:476,784` — `GetMethod(...)!`, `GetProperty(...)!`
- [ ] `Kaleido/Queryable/Query/QueryContextEngine.cs:231` — `Invoke(...)!` cast
- [ ] `Kaleido/Queryable/QueryableService.cs` — `Invoke` result suppressions
- [ ] `Kaleido.Http.Client/KaleidoClientFactoryBase.cs:59` — `(Dictionary<...>)GetValue(map)!`
- [ ] `Kaleido/Json/KaleidoEnumConverterFactory.cs:24` — `Activator.CreateInstance(converterType)!`
- [ ] `Kaleido/Json/ValueConverter.cs:33–76` — `value.ToString()!` ×7 → `?? throw` or `Convert.ToString`
- [ ] `Kaleido/Json/ValueConverter.cs:176,182,188,195` — `element.GetString()!` ×4 → `?? throw`
- [ ] `Kaleido/Process/Planning/StepCandidatePlanner.cs:51,78,86` — `candidate.Registration!` ×3
- [ ] `Kaleido/Process/Planning/StepCandidateConsistencyChecker.cs:57,65,92,144` — `candidate.Registration!` ×4
- [ ] `Kaleido/Queryable/QueryableServiceCollectionExtensions.cs:59` — double `GetCustomAttribute` → single projection with null filter
- [ ] `Kaleido/Queryable/Records/QueryViewRegistrationValidator.cs:51` — `x.Attribute!.Name`
- [ ] `Kaleido/Queryable/Records/QueryContextRegistrationValidator.cs:45` — `x.Attribute!.Name`

### OperationCanceledException handling
- [ ] `Kaleido/Queryable/Query/QueryContextEngine.cs:90–94,150–154` — add `catch (OperationCanceledException)` before generic catch; call observation.Canceled() if available, then rethrow
- [ ] `Kaleido/Queryable/Query/DelegatedQueryViewEngine.cs:94–98` — same fix
- [ ] `Kaleido/Process/ParticipantRuntime.cs:113–117` — same fix
- [ ] `Kaleido/Process/Execution/ProcessStepInvoker.cs:72–76` — same fix
- [ ] `Kaleido.Http/Registry/RegistryEndpointRouteBuilderExtensions.cs:203–217,248–262` — add `catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }` before generic catch in both fan-out methods

### ExceptionMiddleware hardening
- [ ] Add terminal `catch (Exception exception)` → `LogError`, `Activity.Current?.SetStatus(ActivityStatusCode.Error)`, return 500 `KaleidoErrorResponse` — check `Response.HasStarted` first
- [ ] Set `Activity.Current?.SetStatus(ActivityStatusCode.Error)` on existing `ArgumentException` and `KaleidoFrameworkException` branches too

### Duplicate code extraction
- [ ] Extract `StampCorrelationHeaders` + `SanitizeHeaderValue` to `internal static class CorrelationHeaderStamper` in `Kaleido.Http.Client` (currently copy-pasted between `KaleidoProcessClient` and `KaleidoQueryableClient`)
- [ ] Extract `GetDownstreamProcessesAsync` / `GetDownstreamQueryablesAsync` to a single generic method `GetDownstreamAsync<TMap,TItem>` in `RegistryEndpointRouteBuilderExtensions.cs`
- [ ] Extract the three identical `QueryableValidationException → BadRequest` catch blocks in `QueryableEndpointRouteBuilderExtensions.cs` to a shared helper
- [ ] Extract `ProcessExecutionService.ExecuteAsync` shared core (~40 dup lines) to a private `ExecuteStepCoreAsync` method
- [ ] Extract assembly type-scan predicate + TypeFilter guard to a shared `AssemblyTypeScanner` helper (used in both `ParticipantServiceCollectionExtensions` and `QueryableServiceCollectionExtensions`)
- [ ] Extract `IQueryViewSource*` interface detection pattern (repeated ×5) to a `QueryViewInterfaceScanner` helper
- [ ] Extract duplicate-name GroupBy validation (×3) to a `ValidationHelpers.ThrowOnDuplicateNames<T>` method
- [ ] Centralize observability tag-name magic strings to a `KaleidoObservabilityTags` constants class

### Observability correctness
- [ ] Add correlation headers to registry-fetch HTTP call in `KaleidoProcessClient.cs:266` — build `HttpRequestMessage`, call `StampCorrelationHeaders` before `SendAsync`
- [ ] Same fix in `KaleidoQueryableClient.cs:203`
- [ ] Add `logger.LogDebug(...)` in the two 404-swallow branches in `RegistryEndpointRouteBuilderExtensions.cs`
- [ ] Add `ILogger` injection to `KaleidoProcessClient` and `KaleidoQueryableClient`; log at Debug on send, Warning on non-success before throw
- [ ] Add `AddEntityFrameworkCoreInstrumentation()` to `AddOpenTelemetry()` in `KaleidoObservabilityOpenTelemetryExtensions.cs`
- [ ] Add latency `Histogram<double>` instruments to `ProcessTelemetry` and `QueryableTelemetry`; record on execution/step/query completion
- [ ] Promote key lifecycle log messages to `LogInformation` (process execution complete, context save, registry rebuilt)

### Performance — safe caching
- [ ] Cache all `GetMethods()` results in `CompiledQueryApplier.cs` as `static readonly` fields (biggest per-request CPU win)
- [ ] Fix double JSON round-trip in `StepCandidateBuilder.cs:74–83` — if value `is JsonElement je`, call `je.Deserialize(stepType)` directly
- [ ] Cache `ProcessStepRegistry.InitialRegistrations` — compute once in constructor instead of `Where`+`ToArray` per property access
- [ ] Fix `DataTypeMapper.Lookup` to cache enum/array descriptor results in a `ConcurrentDictionary<Type, DataTypeDescriptor>`
- [ ] Fix O(N²) state mutation in `ProcessStateUpdater` — replace LINQ `ToList`+scan with index-based or dictionary approach
- [ ] Compute `StepAvailabilityResolver.Resolve` once per step in `ExecutionProcessor`, pass result into `evaluator.Evaluate` instead of recomputing
- [ ] Build `Dictionary<string,FieldMetadata>` once per request in validator/normalizer/compiler instead of linear scans

### Public API correctness
- [ ] Make `AddProcessClient(...)` and `AddQueryableClient(...)` `public` (currently `internal` but documented as public)
- [ ] Make `MapQueryView` and `MapDelegatedQueryView` `private` (currently public but leak internal types)
- [ ] Remove unused `serviceName` parameter from `MapQueryView` and `MapDelegatedQueryView`
- [ ] Make `ExecuteStepRequest.ToProcessRequest` `internal` (leaks core runtime types through shared contract assembly)
- [ ] Fix `ProcessStepHandlerResult<T>.HandOff` return type → `ProcessStepHandlerResult<TProcessStepResult>` (currently returns non-generic, typed handlers cannot use it)

### Dependency graph fixes
- [ ] Change `Kaleido.Observability.OpenTelemetry.csproj` project reference from `Kaleido.AspNetCore` → `Kaleido` (only uses core types)
- [ ] Add explicit `<ProjectReference Include="..\Kaleido\Kaleido.csproj" />` to `Kaleido.Http.csproj` (currently relies on transitive)
- [ ] Add explicit `PackageReference Include="Microsoft.Extensions.Configuration.Abstractions"` to `Kaleido.Http.Client.csproj`

### Security guardrails
- [ ] Add max filter depth check (e.g., depth ≤ 10) to `QueryRequestValidator.ValidateFilter` — prevents stack-overflow DoS from deeply nested `QueryFilterGroup`
- [ ] Sanitize `RegistryClientError.Reason` — replace raw `ex.Message` (which can contain internal hostnames/URLs) with a generic message; keep detail in logs
- [ ] Add startup warning when `InMemoryProcessContextStore` is the registered store (it has no eviction and grows without bound in production)
- [ ] Cap correlation header value lengths in `KaleidoAspNetCoreCorrelation.cs`

### Build pipeline
- [ ] Add `Microsoft.SourceLink.GitHub` package reference and `EmbedUntrackedSources` to `build/packages.props`
- [ ] Add `PackageLicenseExpression`, `Description`, `PackageProjectUrl`, `RepositoryType=git` to `build/packages.props`
- [ ] Add `<None Include="$(MSBuildProjectDirectory)\README.md" Pack="true" PackagePath="\" />` item group to `build/packages.props` (fixes NU5039 broken readme reference)
- [ ] Add `NuGet.config` at repo root with explicit `nuget.org` source
- [ ] Fix `Radiology.csproj` — add missing `<Import Project="../../../../build/samples.props" />` and remove duplicated properties
- [ ] Extend CI (`build.yml`): add `--collect:"XPlat Code Coverage"` to `dotnet test`, add Codecov upload step
- [ ] Extend CI: add `dotnet pack -c Release --no-build` + `actions/upload-artifact` step
- [ ] Set `global-json-file` in CI `setup-dotnet` action to respect `global.json` SDK pin

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
- [ ] Convert `KaleidoProcessClient` and `KaleidoQueryableClient` constructors to primary constructors
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
- `NullEventPublisher` doc comment fix (says "before AddKaleido" when it's actually after)
- `IProcessStateService.GetCurrentState` missing `Async` suffix
- `KaleidoEventEnvelope<TEvent,TContext>` — add `IKaleidoEventContext` marker constraint on `TContext`
- `KaleidoCorrelationContext.IsEmpty` ignores `StepName` — fix to include it
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

- [ ] Tier 1 complete
- [ ] Tier 2 complete
- [ ] Tier 3 complete
- [ ] Tier 4 decisions made
