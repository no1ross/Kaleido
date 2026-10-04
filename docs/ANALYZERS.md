# Kaleido analyzer rules

Kaleido ships three analyzer projects (in `tools/analyzers`), each with a distinct audience:

| Project | IDs | Audience |
|---|---|---|
| [`Kaleido.Analyzers`](../tools/analyzers/Kaleido.Analyzers/) | `KAL2xxx` | **Consumers** of the `Kaleido` NuGet package — bundled into the package and applied to consumer compilations to catch framework misuse at compile time |
| [`Kaleido.Analyzers.Source`](../tools/analyzers/Kaleido.Analyzers.Source/) | `KAL0xxx` | **Contributors** to this repository — enforces internal design conventions; never shipped |
| [`Kaleido.Analyzers.Testing`](../tools/analyzers/Kaleido.Analyzers.Testing/) | `KAL1xxx` | **Contributors** writing tests — enforces fixture conventions; never shipped |

These are build diagnostics, not runtime error codes — runtime codes are documented in [`ERROR_CODES.md`](./ERROR_CODES.md).

Severities are configured in `.editorconfig`. In `src/` warnings are treated as errors; in `tests/` several rules are relaxed where the convention genuinely differs (e.g. `!` usage, `KAL0002` stubs).

## Consumer rules — `KAL2xxx`

Shipped inside the `Kaleido` package (`analyzers/dotnet/cs/`). They fire on **consumer code** — anyone referencing the `Kaleido` package gets these checks automatically.

| ID | Severity | Rule |
|---|---|---|
| KAL2001 | Error | `[ProcessStep]` must declare a non-empty `Name` and `Version` — compile-time equivalent of startup `pro_missing_attribute` failures |
| KAL2002 | Error | `[QueryContext]` must declare a non-empty `Name` and `Version` — compile-time equivalent of `qry_missing_attribute` |
| KAL2003 | Error | `[QueryView]` must declare a non-empty `Name` and `Version` — compile-time equivalent of `qry_missing_attribute` |
| KAL2004 | Warning | `IProcessStepHandler<T>.ExecuteAsync` must not swallow `OperationCanceledException` in a bare `catch (Exception)` — add `when (ex is not OperationCanceledException)` or a preceding OCE catch. A swallowed cancellation inflates failure metrics and hides client disconnects |
| KAL2005 | Warning | `ServiceName` string literals must be lowercase with no spaces, hyphens, or underscores — it is used verbatim as the HTTP route prefix |
| KAL2007 | Warning | `[ProcessStep]` class names must end in `Step` — the framework derives the step name by stripping the suffix |
| KAL2008 | Warning | `[ProcessStep]` type has no `IProcessStepHandler<TStep>` (or `IProcessStepHandler<TStep, TResult>`) in the same compilation — compile-time equivalent of `pro_missing_handler`. Cross-assembly handlers suppress the warning |
| KAL2009 | Warning | `AddKaleido(config, o => ...)` lambda never sets `o.Assemblies` — the `GetCallingAssembly()` fallback is JIT-nondeterministic; set assemblies explicitly |

## Source rules — `KAL0xxx`

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

### DI rule notes (KAL0005–KAL0014)

The registered-service model is harvested from `*ServiceCollectionExtensions` classes in the same compilation (generic args, `typeof()` args, returned `new` in factory lambdas). `*ServiceCollectionExtensions` and `*EndpointRouteBuilderExtensions` are composition roots — exempt. `context.RequestServices` resolution (middleware/endpoint activation) and dynamic resolutions (runtime `Type` args, open-generic type parameters) are exempt from KAL0007 — they are the container's dispatch seam. Tests are exempt via `.editorconfig`.

### HTTP endpoint notes

Contributor convention: declare endpoint names as `const string` fields in a `*EndpointNames` class in `Kaleido.Http.Abstractions`, not inline literals passed to `WithName()`. This is **not** a shipped analyzer diagnostic; the proposed KAL0020 rule remains separately tracked in [#152](https://github.com/no1ross/Kaleido/issues/152).

### API design rules notes (KAL0018–KAL0019)

KAL0018 applies to all `src/` projects (suppressed for `Kaleido.Provider.SQLite` where EF Core entity navigation properties conventionally use `ICollection<T>`). Overrides and explicit interface implementations are exempt — the collection type is fixed at the interface/base. KAL0019 applies to all `src/` projects. Overrides, explicit interface implementations, and the ASP.NET Core middleware `InvokeAsync(HttpContext)` convention are exempt from KAL0019.

## Security-relevant analyzers

For code generators: these rules exist because the bypass they prevent is a
security or correctness hazard, not a style preference. Generate compliant
code on the first pass — do not suppress.

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
| KAL2001–2003 | Step/context/view attributes missing `Name`/`Version` — startup failures ship as runtime 500s |
| KAL2004 | Swallowed `OperationCanceledException` in step handlers — canceled steps recorded as failures, false alerts |
| KAL2005 | Invalid `ServiceName` literals — route-prefix corruption (path separators, casing) |
| KAL2007 | `[ProcessStep]` classes not ending in `Step` — derived step names drift from intent |
| KAL2008 | `[ProcessStep]` with no handler — step is registered but can never execute |
| KAL2009 | `AddKaleido` without `o.Assemblies` — JIT-nondeterministic calling-assembly fallback |

## Test rules — `KAL1xxx`

`Kaleido.Analyzers.Testing` is referenced by all test projects through `tests/Directory.Build.props`. Individual fixture rules have their own applicability checks and `.editorconfig` exemptions: KAL1006 is disabled for functional/integration tests, KAL1009 runs only in `*.UnitTests` assemblies, and KAL1010/KAL1011 apply wherever their matching test-project code occurs.

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

KAL1009 is configured as a warning — it flags types missing a fixture without breaking the build; see `.editorconfig` `[tests/**]` section.

### SutFixture notes (KAL1006–KAL1009)

`SutFixture<TSut>` lives in `Kaleido.UnitTests` (the base test project — other `*.UnitTests` projects reference it). Testable means public or internal class, non-static, non-abstract, with at least one ordinary method; records, exceptions, attributes, DTO-suffix types, and static `*Extensions` classes are exempt. KAL1009 runs only in `*.UnitTests` assemblies; KAL1006 is disabled for functional/integration projects in `.editorconfig`. Those suites are scenario-scoped and do not normally use `SutFixture`, while the other fixture-shape rules activate only when their specific fixture pattern occurs.

## Rationale, examples, and implementation

### Consumer checks

- **KAL2004 — preserve cancellation.** A step handler's `ExecuteAsync` with a bare `catch (Exception)` can turn `OperationCanceledException` into a false execution failure. Use `catch (Exception ex) when (ex is not OperationCanceledException)` or handle cancellation in an earlier catch. The rule targets Process step-handler `ExecuteAsync` bodies, not every catch in an application. [Implementation](../tools/analyzers/Kaleido.Analyzers/Process/StepHandlerOceAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Process/StepHandlerOceAnalyzerTests.cs).
- **KAL2009 — explicit discovery.** `AddKaleido(config, o => o.ServiceName = "app")` omits `Assemblies`; set `o.Assemblies = [typeof(Program).Assembly]` in the options lambda so registration does not depend on the calling-assembly fallback. This checks options lambdas, not arbitrary configuration loaded from other sources. [Implementation](../tools/analyzers/Kaleido.Analyzers/Bootstrap/AddKaleidoAssembliesAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Bootstrap/AddKaleidoAssembliesAnalyzerTests.cs).

The KAL2001–KAL2003 attribute rules catch missing `Name`/`Version` before startup, and KAL2008 detects a missing same-compilation step handler; the table above distinguishes warnings from errors. These are consumer checks bundled with the main Kaleido package, unlike the contributor checks below.

### Contributor conventions

- **KAL0003 — make null assumptions visible.** `typeof(Widget).GetMethod("Run")!` hides a missing-reflection-member failure; `typeof(Widget).GetMethod("Run") ?? throw new KaleidoFrameworkException(FrameworkErrorCodes.ReflectionError, "Run was not found.")` handles it explicitly. Test code legitimately uses `!` in assertion/stub setups and is exempted in `.editorconfig`. [Implementation](../tools/analyzers/Kaleido.Analyzers.Source/Design/NullForgivingOperatorAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Source.UnitTests/Design/NullForgivingOperatorAnalyzerTests.cs).
- **KAL0015 — keep a local interface with its implementation.** If `IWidgetService` and its same-named `WidgetService` implementation live in one assembly, declare both in `WidgetService.cs` instead of a separate `IWidgetService.cs`. Provider contracts such as `IProcessorContextStore`, which have no same-named implementation in that assembly, are exempt. This is a repository layout convention, not a runtime correctness check. [Implementation](../tools/analyzers/Kaleido.Analyzers.Source/Structure/InterfaceCoLocationAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Source.UnitTests/Structure/InterfaceCoLocationAnalyzerTests.cs).

The DI and API-design tables above state the remaining source rules' immediate failure modes and scoped exemptions; see their implementations under [`Kaleido.Analyzers.Source`](../tools/analyzers/Kaleido.Analyzers.Source/) when changing a convention.

### Test-fixture safeguards

**KAL1010** prevents a helper-only `*Tests` class from looking like coverage. For example, a concrete `WidgetTests : SutFixture<Widget>` with `CreateSut()` and only helper methods, but no `[Fact]` or `[Theory]`, reports KAL1010; add a real test rather than an empty assertion. A `*Tests` class with **no ordinary methods at all** is intentionally skipped by KAL1010, as are abstract/static or differently named classes. KAL1009 independently looks for an actual `[Fact]`/`[Theory]` fixture for a testable source type in a matching `*.UnitTests` assembly. [Implementation](../tools/analyzers/Kaleido.Analyzers.Testing/Fixtures/FixtureEmptyAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Testing.UnitTests/Fixtures/FixtureEmptyAnalyzerTests.cs).

**KAL1011** rejects a record inheriting from `System.Exception`; synthesized record equality and copy behavior do not belong on an exception. `record WidgetException : System.Exception` is already invalid C# today, so this diagnostic is a **secondary safeguard**, not a claim to detect an otherwise compiling bug. Use `class WidgetException : System.Exception` instead; a record that is not an exception (for example `record WidgetResult(bool Success)`) is exempt. The analyzer test suppresses the compiler's own errors to assert the rule independently. [Implementation](../tools/analyzers/Kaleido.Analyzers.Testing/Design/ExceptionRecordAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Testing.UnitTests/Design/ExceptionRecordAnalyzerTests.cs).
