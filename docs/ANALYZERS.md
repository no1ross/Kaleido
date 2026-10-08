# Kaleido analyzer rules

Kaleido has three analyzer projects (in `tools/analyzers`). Each targets **one audience and one kind of code**; this guide gives each its own self-contained section.

| Section | IDs | Who sees it | Runs on | Shipped |
|---|---|---|---|---|
| [Consumer rules](#consumer-rules--kal2xxx) | `KAL2xxx` | Anyone referencing the `Kaleido` NuGet package | Consumer services (and this repo's samples) | Yes — inside `Kaleido.nupkg` (`analyzers/dotnet/cs/`) |
| [Source rules](#source-rules--kal0xxx) | `KAL0xxx` | Contributors to this repository | Framework source (`src/`) | No |
| [Test rules](#test-rules--kal1xxx) | `KAL1xxx` | Contributors writing tests in this repository | Test projects (`tests/`) | No |

All three are build diagnostics, not runtime error codes (runtime codes are in [`ERROR_CODES.md`](./ERROR_CODES.md)). Severities are configured in `.editorconfig`: in `src/` warnings are treated as errors; in `tests/` some rules are relaxed where the convention genuinely differs.

**For code generators:** every rule below exists because of a correctness or security hazard, not style. Generate compliant code on the first pass; never suppress a `KAL` rule.

---

## Consumer rules — `KAL2xxx`

**Audience:** developers building services on Kaleido. **Project:** [`Kaleido.Analyzers`](../tools/analyzers/Kaleido.Analyzers/), shipped in the `Kaleido` package, so the rules apply automatically to any code that references it. Most are compile-time equivalents of startup validation: a misconfigured step, source or view fails the build instead of failing at startup.

| ID | Severity | Rule | What it prevents |
|---|---|---|---|
| KAL2001 | Error | `[ProcessStep]` must declare a non-empty `Version`, `DisplayName` and `Description` | Startup failure `pro_missing_attribute` |
| KAL2002 | Error | `[QuerySource]` must declare a non-empty `Version`, `DisplayName` and `Description` | Startup failure `qry_missing_attribute` |
| KAL2003 | Error | `[QueryView]` must declare a non-empty `Version`, `DisplayName` and `Description` | Startup failure `qry_missing_attribute` |
| KAL2004 | Warning | A step handler's `ExecuteAsync` must not swallow `OperationCanceledException` in a bare `catch (Exception)` | Canceled steps recorded as failures; inflated error metrics, false alerts |
| KAL2005 | Warning | `ServiceName` literals must be lowercase with no spaces, hyphens or underscores | Corrupted route prefix (the name is used verbatim) |
| KAL2008 | Warning | An `IProcessStep` type has no `IProcessStepHandler<TStep>` (or `<TStep, TResult>`) in the same compilation | A step that is published but can never run (startup `pro_missing_handler`) |
| KAL2009 | Warning | An `AddKaleido(config, o => …)` lambda never sets `o.Assemblies` | Startup failure `missing_assembly` |
| KAL2010 | Error | `[ProcessStep]` on a type that does not implement `IProcessStep` | Startup failure `pro_invalid_registration` |
| KAL2011 | Error | A concrete `IProcessStep` type without `[ProcessStep]` | Startup failure `pro_missing_attribute` |
| KAL2012 | Error | `[QuerySource]` or `[QueryView]` on a type without the matching source/view interface | A capability that is silently never published (discovery is interface-only) |
| KAL2013 | Error | A concrete query source or view without its `[QuerySource]` / `[QueryView]` attribute | Startup failure `qry_missing_attribute` |
| KAL2014 | Warning | `[Filterable]`, `[Searchable]` or `[Sortable]` on a property of a type that is not an `IQueryContext` | Query rules that silently have no effect |
| KAL2015 | Error | An `IInformationStep` declares a property other than `InformationRequestId` and `Items` | Startup failure `pro_invalid_registration`; the same data in two places |
| KAL2016 | Error | `Success<TNext>()` names an information step | A step with nothing to answer (runtime `pro_information_request_missing`); use `RequireInformation<TNext>(request)` |
| KAL2017 | Warning | A process step input property has no description (`[Description]` or `[Display(Description = …)]`) | Inputs UIs and AI agents can't explain or ask for |
| KAL2018 | Warning | An `IQueryContext` or `IQueryParameters` property has no description | Query fields UIs and AI agents (MCP tool schemas) can't explain |

### Examples and notes

- **KAL2001–KAL2003 — required metadata.** Steps, sources and views are named by their type name; `Version`, `DisplayName` and `Description` are what the registry publishes to UIs, documentation and AI agents, so they must be non-empty.
- **KAL2004 — preserve cancellation.** Use `catch (Exception ex) when (ex is not OperationCanceledException)` or handle cancellation in an earlier catch. The rule targets step-handler `ExecuteAsync` bodies, not every catch in an application. [Implementation](../tools/analyzers/Kaleido.Analyzers/Process/StepHandlerOceAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Process/StepHandlerOceAnalyzerTests.cs).
- **KAL2008 — one handler per step.** Only the same compilation is inspected; a handler in another assembly suppresses the warning.
- **KAL2009 — explicit discovery.** `AddKaleido(config, o => o.ServiceName = "app")` omits `Assemblies`; set `o.Assemblies = [typeof(Program).Assembly]`. The warning is a syntax-level hint for lambdas; `AddKaleido()` itself rejects missing or empty lists with `missing_assembly`, including when no lambda is supplied. [Implementation](../tools/analyzers/Kaleido.Analyzers/Bootstrap/AddKaleidoAssembliesAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Bootstrap/AddKaleidoAssembliesAnalyzerTests.cs).
- **KAL2010–KAL2013 — the interface is the identity, the attribute describes it.** This holds for steps (`IProcessStep` / `[ProcessStep]`), sources (`IQuerySource<T>`, `IQuerySourceAsync<T>`, `IDelegatedQuerySource<…>` / `[QuerySource]`) and views (`IQueryViewSource<TSource, …>` / `[QueryView]`). Each rule flags one side without the other. [Step implementation](../tools/analyzers/Kaleido.Analyzers/Process/ProcessStepIdentityAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Process/ProcessStepIdentityAnalyzerTests.cs); [Queryable implementation](../tools/analyzers/Kaleido.Analyzers/Queryable/QueryableIdentityAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Queryable/QueryableIdentityAnalyzerTests.cs).
- **KAL2014 — query rules live on query contexts.** Put `[Filterable]` / `[Searchable]` / `[Sortable]` on the `IQueryContext` record a source is queried by, not on view or result records.
- **KAL2015–KAL2016 — information steps.** An `IInformationStep`'s payload is the answers to its pending request, so it declares only `InformationRequestId` and `Items`; known inputs belong on an ordinary step. It is always required with its questions: `RequireInformation<TNext>(request)`, never `Success<TNext>()`. Requests built from runtime values can't be checked statically; the runtime guard (`pro_information_request_missing`) still applies. See [`INFORMATION_REQUESTS.md`](./INFORMATION_REQUESTS.md). [Implementation](../tools/analyzers/Kaleido.Analyzers/Process/InformationStepAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Process/InformationStepAnalyzerTests.cs).
- **KAL2017–KAL2018 — describe inputs.** Only a description is asked for — `[Description("…")]` or `[Display(Description = "…")]`; `[Display(Name, Prompt)]` stay optional hints. The registry publishes them as field metadata, which is what UIs and AI agents read to know what to ask for. Warnings, not errors: the rule nudges without blocking. Information steps are exempt (their request describes the questions). [Implementation](../tools/analyzers/Kaleido.Analyzers/InputDescriptionAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/InputDescriptionAnalyzerTests.cs).

Compliant shapes for all of the above are in [`PATTERNS.md`](./PATTERNS.md).

---

## Source rules — `KAL0xxx`

**Audience:** contributors changing the framework. **Project:** [`Kaleido.Analyzers.Source`](../tools/analyzers/Kaleido.Analyzers.Source/), referenced by every `src/` project through `src/Directory.Build.props`; never shipped.

| ID | Rule | Diagnostic message |
|---|---|---|
| KAL0001 | Static classes are reserved for extension methods — no static helper/util classes | `Static class '{0}' is reserved for extension methods — convert '{1}' to an extension method or use a non-static class` |
| KAL0002 | Throw `Kaleido*Exception` types, not general BCL exceptions (argument guards and the ASP.NET 400 contract are allow-listed) | `Throw a Kaleido*Exception instead of '{0}'` |
| KAL0003 | No `!` null-forgiving operator — use explicit null checks | `Replace '!' with an explicit null check` |
| KAL0004 | Exception types must not be records | `Exception type '{0}' is declared as a record — exception classes must remain classes` |
| KAL0005 | Service-like types must not be static (name ends `Service/Handler/Executor/Processor/Provider/Factory/Reader/Writer`) | `Static class '{0}' looks like an injectable service ('{1}' is not an extension method) — make it injectable or rename it` |
| KAL0006 | No `new` on DI-registered implementation types | `Type '{0}' is a DI-registered service implementation — resolve it from DI instead of newing it up` |
| KAL0007 | No service locator — `IServiceProvider.GetService/GetServices/GetRequiredService` outside composition roots | `'{0}' on IServiceProvider is a service locator call — inject the dependency through the constructor instead` |
| KAL0008 | Inject the service abstraction, not the concrete implementation | `Parameter '{0}' is typed as concrete '{1}' — inject '{2}' instead` |
| KAL0009 | Services use constructor injection only — no settable service properties or `Set*` injection methods | `'{0}' on service '{1}' injects '{2}' outside the constructor — use constructor injection` |
| KAL0010 | Injected deps must be retained safely — readonly fields, no ignored ctor params | `Field '{0}' on service '{1}' holds '{2}' — make it readonly` / `Constructor parameter '{0}' on service '{1}' is never used` |
| KAL0011 | DI constructors must not perform work on injected dependencies | `Invocation '{0}' inside '{1}'s constructor does work — assign dependencies and validate arguments only` |
| KAL0012 | No manual infrastructure instantiation (`HttpClient`, `ServiceCollection`, `ServiceProvider`, `LoggerFactory`, `DbContext`) | `'new {0}()' bypasses the container-managed factory — inject the corresponding abstraction instead` |
| KAL0013 | Do not dispose container-owned dependencies | `'{0}' on '{1}' disposes a container-owned dependency ('{2}') — the container manages its lifetime` |
| KAL0014 | Singleton registrations must not resolve scoped services | `Singleton factory resolves '{0}' which is registered as Scoped — the scoped instance would be captured for the app lifetime` |
| KAL0015 | Interface must live in the same file as its same-named implementation (`IProcessorRuntime` in `ProcessorRuntime.cs`); provider contracts exempt | `Interface '{0}' should be declared in '{1}' alongside '{2}' — an interface and its concrete class share a file` |
| KAL0018 | Public and internal API members must not expose mutable collection types (`List<T>`, `IList<T>`, `Dictionary<K,V>`, `IDictionary<K,V>`, `HashSet<T>`, `ISet<T>`, `ICollection<T>`) | `'{0}' is a mutable collection type — use IReadOnlyCollection<T>, IReadOnlyList<T>, IReadOnlyDictionary<K,V>, or IEnumerable<T> instead` |
| KAL0019 | Public and internal async methods returning `Task`/`Task<T>` must accept a `CancellationToken` parameter | `Async method '{0}' does not accept a CancellationToken — add 'CancellationToken cancellationToken = default' so callers can propagate cancellation` |
| KAL0020 | ASP.NET Core `WithName(...)` must take a constant or factory method from a type whose name ends in `EndpointNames` | `Pass a member of an *EndpointNames type to WithName instead of '{0}'` |
| KAL0021 | A catch-all (`catch` / `catch (Exception)`) that calls a member on an `*Observation`/`*Observability` type must exclude `OperationCanceledException`, via a `when` filter or an earlier `catch (OperationCanceledException)` | `This catch-all calls '{0}' but does not exclude OperationCanceledException — add 'when (ex is not OperationCanceledException)' or an earlier catch (OperationCanceledException)` |

### What the security-relevant rules prevent

| Rule | What it prevents |
|---|---|
| KAL0006 | `new`-ing a DI-registered service — loses container lifetime/scoping guarantees |
| KAL0007 | Service-locator calls outside composition roots — hides dependencies from validation |
| KAL0008 | Injecting concrete types — breaks substitutability, lets callers reach internals |
| KAL0009 | Property/`Set*` injection on services — services become incompletely initialized |
| KAL0010 | Non-readonly injected fields / ignored ctor params — mutable deps or silent misconfiguration |
| KAL0011 | Constructor work on injected deps — side effects at resolution time, ordering hazards |
| KAL0012 | Manual `new HttpClient`/`ServiceProvider`/`DbContext` — socket exhaustion, leaked scopes |
| KAL0013 | Disposing container-owned dependencies — disposed-injected-dependency crashes mid-request |
| KAL0014 | Singleton capturing a scoped service — captive dependency, stale per-request state |
| KAL0018 | Mutable collection types on the API surface — callers can corrupt shared state |
| KAL0019 | Async methods without `CancellationToken` — cancellations stop propagating |
| KAL0020 | Ad-hoc endpoint names — typos break link generation and OpenAPI operation ids silently |
| KAL0021 | Cancellations recorded as failures in observability catch-alls — inflated error metrics, false alerts |

### Scope notes

**DI rules (KAL0005–KAL0014).** The registered-service model is harvested from `*ServiceCollectionExtensions` classes in the same compilation (generic args, `typeof()` args, returned `new` in factory lambdas). `*ServiceCollectionExtensions` and `*EndpointRouteBuilderExtensions` are composition roots — exempt. `context.RequestServices` resolution (middleware/endpoint activation) and dynamic resolutions (runtime `Type` args, open-generic type parameters) are exempt from KAL0007 — they are the container's dispatch seam. So is resolution from a scope created in the same method (`using var scope = scopeFactory.CreateScope(); scope.ServiceProvider.Get…`): a child scope's services can never be constructor-injected (injection yields the parent scope's instance), so that scope is a composition seam. Resolution from an injected `IServiceProvider` or `IServiceScope` is still reported. Tests are exempt via `.editorconfig`.

**API design rules (KAL0018–KAL0019).** KAL0018 applies to all `src/` projects (suppressed for `Kaleido.Provider.SQLite` where EF Core entity navigation properties conventionally use `ICollection<T>`). Overrides and explicit interface implementations are exempt — the collection type is fixed at the interface/base. KAL0019 applies to all `src/` projects. Overrides, explicit interface implementations, and the ASP.NET Core middleware `InvokeAsync(HttpContext)` convention are exempt from KAL0019.

**Endpoint names (KAL0020).** Endpoint names are declared as `const string` fields or name-factory methods on a `*EndpointNames` class in `Kaleido.Http.Abstractions` (`ProcessEndpointNames`, `RegistryEndpointNames`) or `Kaleido.Http` (`QueryableEndpointNames`). All names follow one pattern, `Kaleido` + area + action with `_` before any variable part (`KaleidoProcessExecute`, `KaleidoProcessStepExecute_{step}`, `KaleidoQueryableQuery_{source}`, `KaleidoQueryableViewQuery_{source}_{view}`, `KaleidoRegistry`); the prefix keeps them from colliding with the host app's own endpoint names. An inline literal or a constant from any other type passed to `WithName()` is reported. Endpoint names are link targets and OpenAPI operation ids, and an unenforced literal let the `KaleidoProcessStepREgistry` typo ship (HP-015, [#62](https://github.com/no1ross/Kaleido/issues/62)).

### Examples

- **KAL0003 — make null assumptions visible.** `typeof(Widget).GetMethod("Run")!` hides a missing-reflection-member failure; `typeof(Widget).GetMethod("Run") ?? throw new KaleidoFrameworkException(FrameworkErrorCodes.ReflectionError, "Run was not found.")` handles it explicitly. Test code legitimately uses `!` in assertion/stub setups and is exempted in `.editorconfig`. [Implementation](../tools/analyzers/Kaleido.Analyzers.Source/Design/NullForgivingOperatorAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Source.UnitTests/Design/NullForgivingOperatorAnalyzerTests.cs).
- **KAL0015 — keep a local interface with its implementation.** If `IWidgetService` and its same-named `WidgetService` implementation live in one assembly, declare both in `WidgetService.cs` instead of a separate `IWidgetService.cs`. Provider contracts such as `IProcessorContextStore`, which have no same-named implementation in that assembly, are exempt. This is a repository layout convention, not a runtime correctness check. [Implementation](../tools/analyzers/Kaleido.Analyzers.Source/Structure/InterfaceCoLocationAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Source.UnitTests/Structure/InterfaceCoLocationAnalyzerTests.cs).

For the remaining rules, see their implementations under [`Kaleido.Analyzers.Source`](../tools/analyzers/Kaleido.Analyzers.Source/) when changing a convention.

---

## Test rules — `KAL1xxx`

**Audience:** contributors writing tests. **Project:** [`Kaleido.Analyzers.Testing`](../tools/analyzers/Kaleido.Analyzers.Testing/), referenced by every test project through `tests/Directory.Build.props`; never shipped. Individual rules have their own applicability checks and `.editorconfig` exemptions: KAL1006 is disabled for functional/integration tests, KAL1009 runs only in `*.UnitTests` assemblies, and KAL1010/KAL1011 apply wherever their matching test-project code occurs.

| ID | Rule | Diagnostic message |
|---|---|---|
| KAL1001 | Test fixtures are named after their subject under test: `{SutName}Tests` | `Test fixture '{0}' must be named after its subject under test ('{1}Tests')` |
| KAL1002 | The fixture's `{Sut}Tests` prefix must resolve to a real type | `Test fixture '{0}' does not map to a type named '{1}' — fixtures are named {SutName}Tests` |
| KAL1003 | The fixture's file location mirrors the SUT's location: `src/{Project}/{path}/Sut.cs` → `tests/{TestProject}/{path}/SutTests.cs` | `Test fixture '{0}' must live at '{1}' (mirroring SUT path '{2}')` |
| KAL1004 | One fixture per subject under test | `Test fixture '{0}' duplicates '{1}' — both map to SUT '{2}'` |
| KAL1005 | Test-built `ServiceProvider`s must validate scopes and validate on build | `BuildServiceProvider() must pass 'new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }'` |
| KAL1006 | Unit-test fixtures must inherit `SutFixture<TSut>` — every test declares its SUT | `Test fixture '{0}' must inherit SutFixture<{1}> — every unit test declares its subject under test` |
| KAL1007 | Fixture name must equal `{TSut.Name}Tests` | `Test fixture '{0}' declares SUT '{1}' — it must be named '{2}'` |
| KAL1008 | The SUT may only be constructed inside `CreateSut()` | `'{0}' may only be constructed inside CreateSut() — use CreateSut() or the Sut property` |
| KAL1009 | Every testable type in the matching `Kaleido.*` source assembly must have a `{Name}Tests` fixture | `Type '{0}' in '{1}' has no '{2}' fixture — every testable type must have a unit-test fixture` |
| KAL1010 | A concrete `*Tests` class that declares ordinary methods but no `[Fact]`/`[Theory]` is a helper-only stub, not evidence of tests; a class with no ordinary methods is exempt | `Fixture '{0}' has no [Fact] or [Theory] test methods — add at least one test or remove the empty stub` |
| KAL1011 | A record deriving from `System.Exception` has inappropriate synthesized value/copy semantics; the C# compiler also rejects this declaration today | `Exception type '{0}' is declared as a record — exception classes must remain classes` |
| KAL1012 | Fixtures may not `new` a framework collaborator — a testable Kaleido type implementing a service interface; mock it instead | `'{0}' is a collaborator, not the SUT — mock it instead of new-ing a real instance` |
| KAL1013 | A unit-test fixture is declared in its SUT's namespace with `.UnitTests` appended (`Kaleido.Queryable.Query.QueryContextEngine` → `Kaleido.Queryable.Query.UnitTests`); `*.UnitTests` assemblies only | `Test fixture '{0}' tests '{1}' — declare it in namespace '{2}'` |

KAL1009 is configured as a warning — it flags types missing a fixture without breaking the build; see the `.editorconfig` `[tests/**]` section.

### SutFixture notes (KAL1006–KAL1009)

`SutFixture<TSut>` lives in `Kaleido.UnitTests` (the base test project — other `*.UnitTests` projects reference it). Testable means public or internal class, non-static, non-abstract, with at least one ordinary method; records, exceptions, attributes, DTO-suffix types, and static `*Extensions` classes are exempt. KAL1009 runs only in `*.UnitTests` assemblies; KAL1006 is disabled for functional/integration projects in `.editorconfig`. Those suites are scenario-scoped and do not normally use `SutFixture`, while the other fixture-shape rules activate only when their specific fixture pattern occurs.

### Examples

- **KAL1010 — no helper-only fixtures.** A concrete `WidgetTests : SutFixture<Widget>` with `CreateSut()` and only helper methods, but no `[Fact]` or `[Theory]`, reports KAL1010; add a real test rather than an empty assertion. A `*Tests` class with **no ordinary methods at all** is intentionally skipped, as are abstract/static or differently named classes. KAL1009 independently looks for an actual `[Fact]`/`[Theory]` fixture for a testable source type in a matching `*.UnitTests` assembly. [Implementation](../tools/analyzers/Kaleido.Analyzers.Testing/Fixtures/FixtureEmptyAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Testing.UnitTests/Fixtures/FixtureEmptyAnalyzerTests.cs).
- **KAL1011 — exceptions are classes.** `record WidgetException : System.Exception` is already invalid C# today, so this diagnostic is a **secondary safeguard**, not a claim to detect an otherwise compiling bug. Use `class WidgetException : System.Exception` instead; a record that is not an exception (for example `record WidgetResult(bool Success)`) is exempt. The analyzer test suppresses the compiler's own errors to assert the rule independently. [Implementation](../tools/analyzers/Kaleido.Analyzers.Testing/Design/ExceptionRecordAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Testing.UnitTests/Design/ExceptionRecordAnalyzerTests.cs).
