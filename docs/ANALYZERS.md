# Kaleido analyzer rules

Kaleido has three analyzer projects (in `tools/analyzers`). Each targets **one audience and one kind of code**; this guide gives each its own self-contained section.

| Section | IDs | Who sees it | Runs on | Shipped |
|---|---|---|---|---|
| [Consumer rules](#consumer-rules--kal2xxx) | `KAL2xxx` | Anyone referencing the `Kaleido` NuGet package | Consumer services (and this repo's samples) | Yes — the `Kaleido.Analyzers` package, a dependency of `Kaleido` |
| [Source rules](#source-rules--kal0xxx) | `KAL0xxx` | Contributors to this repository | Framework source (`src/`) | No |
| [Test rules](#test-rules--kal1xxx) | `KAL1xxx` | Contributors writing tests in this repository | Test projects (`tests/`) | No |

All three are build diagnostics, not runtime error codes (runtime codes are in [`ERROR_CODES.md`](./ERROR_CODES.md)). Severities are configured in `.editorconfig`: in `src/` warnings are treated as errors; in `tests/` some rules are relaxed where the convention genuinely differs.

<a id="rule-ids"></a>**Rule IDs.** Each family is numbered contiguously, in the order its rules were added. Until the first release a removed rule's number is taken by the rules after it, which are renumbered (#416); from **1.0** an ID is permanent: a removed consumer rule is recorded under `### Removed Rules` in `AnalyzerReleases.Shipped.md`, and its number is never reused (see [`tools/analyzers/README.md`](../tools/analyzers/README.md#rule-inventory-and-release-tracking)).

**For code generators:** every rule below exists because of a correctness or security hazard, or an agreed repository convention (marked as such: e.g. KAL0014's file layout, the test-fixture rules). Generate compliant code on the first pass; never suppress a `KAL` rule.

---

## Consumer rules — `KAL2xxx`

**Audience:** developers building services on Kaleido. **Project:** [`Kaleido.Analyzers`](../tools/analyzers/Kaleido.Analyzers/), shipped as the analyzer-only `Kaleido.Analyzers` package. The `Kaleido` package depends on it, so the rules apply automatically to any code that installs `Kaleido`. In this repository `src/Kaleido` references the analyzer project only to declare that dependency; it does not run the consumer rules on the framework. A project reference to `src/Kaleido` does not carry the analyzer, so the samples load it explicitly in `samples/Directory.Build.props`, as a NuGet consumer would get it; test projects do not load it, because unit tests deliberately declare invalid capabilities to exercise startup validation. Most are compile-time equivalents of startup validation: a misconfigured step, source or view fails the build instead of failing at startup.

**Verified from the packages.** CI builds a clean consumer from the packed `.nupkg` files ([`tools/packaging/Test-PackageConsumer.ps1`](../tools/packaging/Test-PackageConsumer.ps1); run it locally after `dotnet pack … -o artifacts`). It checks the package contents (assembly, XML docs, README and symbols per library package; `Kaleido.Analyzers` analyzer-only; `Kaleido` depending on it), builds a valid step and handler without any repository-only diagnostic (KAL0xxx, KAL1xxx, Meziantou, Roslynator), and fails a deliberate violation with KAL2001.

**Severity policy.** A consumer rule defaults to `Error` only when the same mistake also fails at startup (the last column names that startup failure), so the build never rejects code the runtime would run; everything else is a `Warning`. Every rule links to its entry here (`helpLinkUri`), so an IDE's help opens this page at the rule. A step without a handler is not an analyzer rule: handlers often live in another project than their steps, which an analyzer cannot see, and startup rejects a step without exactly one handler (`pro_missing_handler`).

| ID | Severity | Rule | What it prevents |
|---|---|---|---|
| <a id="kal2001"></a>KAL2001 | Error | `[ProcessStep]` must declare a non-empty `Version`, `DisplayName` and `Description` | Startup failure `pro_missing_attribute` |
| <a id="kal2002"></a>KAL2002 | Error | `[QuerySource]` must declare a non-empty `Version`, `DisplayName` and `Description` | Startup failure `qry_missing_attribute` |
| <a id="kal2003"></a>KAL2003 | Error | `[QueryView]` must declare a non-empty `Version`, `DisplayName` and `Description` | Startup failure `qry_missing_attribute` |
| <a id="kal2004"></a>KAL2004 | Warning | A step handler's `ExecuteAsync` must not swallow `OperationCanceledException` in a bare `catch (Exception)` | Canceled steps recorded as failures; inflated error metrics, false alerts |
| <a id="kal2005"></a>KAL2005 | Warning | A constant `ServiceName` must match `[a-z0-9][a-z0-9_-]*` (lowercase ASCII letters, digits, `-` and `_`, starting with a letter or digit) | Startup failure `invalid_service_name` (the name is the route prefix and the MCP tool-name prefix) |
| <a id="kal2006"></a>KAL2006 | Warning | An `AddKaleido(config, o => …)` lambda never sets `o.Assemblies` | Startup failure `missing_assembly` |
| <a id="kal2007"></a>KAL2007 | Error | `[ProcessStep]` on a type that does not implement `IProcessStep` | Startup failure `pro_invalid_registration` |
| <a id="kal2008"></a>KAL2008 | Error | A concrete `IProcessStep` type without `[ProcessStep]` | Startup failure `pro_missing_attribute` |
| <a id="kal2009"></a>KAL2009 | Error | `[QuerySource]` or `[QueryView]` on a type without the matching source/view interface | Startup failure `qry_invalid_registration` (otherwise a capability that is silently never published) |
| <a id="kal2010"></a>KAL2010 | Error | A concrete query source or view without its `[QuerySource]` / `[QueryView]` attribute | Startup failure `qry_missing_attribute` |
| <a id="kal2011"></a>KAL2011 | Warning | `[Filterable]`, `[Searchable]` or `[Sortable]` on a property of a type that is not an `IQueryContext` | Query rules that silently have no effect |
| <a id="kal2012"></a>KAL2012 | Error | An `IInformationStep` declares a property other than `InformationRequestId` and `Items` | Startup failure `pro_invalid_registration`; the same data in two places |
| <a id="kal2013"></a>KAL2013 | Error | `Success<TNext>()` or `Next<TNext>()` names an information step | A step with nothing to answer (runtime `pro_information_request_missing`); use `RequireInformation<TNext>(request)` |
| <a id="kal2014"></a>KAL2014 | Warning | A process step input property has no description (`[Description]` or `[Display(Description = …)]`) | Inputs UIs and AI agents can't explain or ask for |
| <a id="kal2015"></a>KAL2015 | Warning | An `IQueryContext` or `IQueryParameters` property has no description | Query fields UIs and AI agents (MCP tool schemas) can't explain |
| <a id="kal2016"></a>KAL2016 | Warning | A step, query source or query view type name makes the MCP tool name's capability part longer than 40 characters | MCP tool names over 64 characters are shortened with a hash, which agents read badly |
| <a id="kal2017"></a>KAL2017 | Warning | `o.ServiceName` is longer than 15 characters | The service name prefixes every MCP tool name (`{service}_{capability}`) and eats the 64-character budget |
| <a id="kal2018"></a>KAL2018 | Warning | A `[Required]` process step input has a non-nullable value type and is not a C# `required` member | An omitted input silently becomes its default (`Guid.Empty`, `default(DateOnly)`) and passes validation |
| <a id="kal2019"></a>KAL2019 | Warning | The same for an `IQueryParameters` property | An omitted query parameter silently becomes its default and passes validation |
| <a id="kal2020"></a>KAL2020 | Error | A `[Filterable]` operator does not apply to the property's type (a comparison on a string, enum or bool; a string operator on a non-string; `IsTrue`/`IsFalse` on a non-bool; `IsNull`/`IsNotNull` on a non-nullable value type) | Startup failure `qry_invalid_registration`; otherwise an operator the registry advertises but every request using it fails |
| <a id="kal2021"></a>KAL2021 | Warning | A property of a step result type (`TResult` of `IProcessStepHandler<TStep, TResult>`) or of a view's output record (`TView` of `IQueryViewSource`/`IQueryViewSourceAsync`) has no description | Returned values an AI agent or a UI can't interpret (the MCP adapter returns the descriptions with the data) |

### Examples and notes

- **KAL2001–KAL2003 — required metadata.** Steps, sources and views are named by their type name; `Version`, `DisplayName` and `Description` are what the registry publishes to UIs, documentation and AI agents, so they must be non-empty. The value is read as the compiler sees it, so a constant expression (`"…" + "…"`, a `const`, `nameof(…)`) counts like a literal.
- **KAL2004 — preserve cancellation.** Use `catch (Exception ex) when (ex is not OperationCanceledException)` or handle cancellation in an earlier catch. The rule targets step-handler `ExecuteAsync` bodies, not every catch in an application. <a id="cancellation-filters"></a>**How a filter is read (KAL2004 and KAL0018).** Both rules read the `when` filter by its meaning, not by the type names it mentions, and accept a catch-all when the filter is never true for a cancellation, or is true *only* for cancellations (`when (ex is OperationCanceledException)` is a cancellation handler, like `catch (OperationCanceledException)`). Understood, on the catch variable: type tests (`is T`, `is not T`, `!(ex is T)`, declaration and property patterns, `and`/`or`/`not` patterns), `&&`, `||` and `true`/`false`; a `using` alias or a base type such as `SystemException` counts like the type itself. `ex is not TaskCanceledException` (or any type derived from `OperationCanceledException`) does not exclude a plain cancellation; `a || b` excludes only when both sides do, `a && b` when either side does. An earlier clause counts only when it catches every cancellation (`OperationCanceledException` or a base type) *without* a filter. **Not understood, so reported:** method calls (`Log(...)`), string or `nameof` comparisons, `ex.GetType() == typeof(...)` (an exact type misses derived cancellations), `switch` expressions, and tests on any variable other than the catch variable. Rewrite such a filter as `ex is not OperationCanceledException && …`; do not suppress the rule. [Implementation](../tools/analyzers/Kaleido.Analyzers/Process/StepHandlerOceAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Process/StepHandlerOceAnalyzerTests.cs).
- **KAL2005 — one service-name rule.** `AddKaleido` applies the same rule at startup; both are tested against one table ([`ServiceNameCases.cs`](../tests/Shared/ServiceNameCases.cs)). `intake-events` and `my_service` are valid; `MyService`, `my service`, `a/b`, `{id}`, `a.b` and `-intake` are not. The rule checks assignments to `KaleidoServiceOptionsBuilder.ServiceName` (including object initializers) whose value is a compile-time constant: a literal, a `const`, a constant interpolation or `nameof(…)`. A value from configuration (`Kaleido:ServiceName`) is only checked at startup. [Implementation](../tools/analyzers/Kaleido.Analyzers/Bootstrap/ServiceNameAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Bootstrap/ServiceNameAnalyzerTests.cs).
- **KAL2006 — explicit discovery.** `AddKaleido(config, o => o.ServiceName = "app")` omits `Assemblies`; set `o.Assemblies = [typeof(Program).Assembly]`. The warning is a syntax-level hint for lambdas; `AddKaleido()` itself rejects missing or empty lists with `missing_assembly`, including when no lambda is supplied. [Implementation](../tools/analyzers/Kaleido.Analyzers/Bootstrap/AddKaleidoAssembliesAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Bootstrap/AddKaleidoAssembliesAnalyzerTests.cs).
- **KAL2007–KAL2010 — the interface is the identity, the attribute describes it.** This holds for steps (`IProcessStep` / `[ProcessStep]`), sources (`IQuerySource<T>`, `IQuerySourceAsync<T>`, `IDelegatedQuerySource<…>` / `[QuerySource]`) and views (`IQueryViewSource<TSource, …>` / `[QueryView]`). Each rule flags one side without the other. **Registration enforces the same rules at startup, on purpose:** analyzers can be switched off (pragma, `.editorconfig`, `<NoWarn>`, or no analyzer reference), so the analyzer reports the mistake early and the startup check guarantees it; neither replaces the other. [Step implementation](../tools/analyzers/Kaleido.Analyzers/Process/ProcessStepIdentityAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Process/ProcessStepIdentityAnalyzerTests.cs); [Queryable implementation](../tools/analyzers/Kaleido.Analyzers/Queryable/QueryableIdentityAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Queryable/QueryableIdentityAnalyzerTests.cs).
- **KAL2011 — query rules live on query contexts.** Put `[Filterable]` / `[Searchable]` / `[Sortable]` on the `IQueryContext` record a source is queried by, not on view or result records.
- **KAL2012–KAL2013 — information steps.** An `IInformationStep`'s payload is the answers to its pending request, so it declares only `InformationRequestId` and `Items`; known inputs belong on an ordinary step. It is always required with its questions: `RequireInformation<TNext>(request)`, never `Success<TNext>()`. Requests built from runtime values can't be checked statically; the runtime guard (`pro_information_request_missing`) still applies. See [`INFORMATION_REQUESTS.md`](./INFORMATION_REQUESTS.md). [Implementation](../tools/analyzers/Kaleido.Analyzers/Process/InformationStepAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Process/InformationStepAnalyzerTests.cs).
- **KAL2014–KAL2015 — describe inputs.** Only a description is asked for — `[Description("…")]` or `[Display(Description = "…")]`; `[Display(Name, Prompt)]` stay optional hints. The registry publishes them as field metadata, which is what UIs and AI agents read to know what to ask for. Warnings, not errors: the rule nudges without blocking. Information steps are exempt (their request describes the questions). [Implementation](../tools/analyzers/Kaleido.Analyzers/InputDescriptionAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/InputDescriptionAnalyzerTests.cs).

- **KAL2016–KAL2017 — MCP tool-name budgets, in every service.** The rules apply whether or not the project references `Kaleido.Http.Mcp`: MCP tools are named on the router (`{service}_{capability}`), from the registries of the services behind it, so a leaf service that never references the MCP adapter still declares the names its tools get. A service that is never published through MCP may turn both off in its `.editorconfig` (`dotnet_diagnostic.KAL2016.severity = none`). [Implementation](../tools/analyzers/Kaleido.Analyzers/ToolNameLengthAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/ToolNameLengthAnalyzerTests.cs).

- **KAL2018–KAL2019 — a required value must be able to be missing.** The `[Required]` attribute fails only for `null`, so on a `Guid`, `DateOnly`, enum or number it never fails: an omitted value has already become its default. Declare the input with the C# `required` keyword (`[Required] public required Guid MemberId { get; init; }`): the compiler makes in-process callers set it, and System.Text.Json refuses a request body that leaves it out (Kaleido answers 400 naming it). A nullable type (`Guid?`) also satisfies the rule, at the cost of `.Value` in handlers. [Implementation](../tools/analyzers/Kaleido.Analyzers/RequiredValueTypeAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/RequiredValueTypeAnalyzerTests.cs).

- **KAL2020 - filter operators fit the field.** The rule mirrors the startup check: comparisons (`GreaterThan`, `GreaterThanOrEqual`, `LessThan`, `LessThanOrEqual`, `Between`, `NotBetween`) need a number or a type with comparison operators (dates, times, `Guid`), string operators need `string`, `IsTrue`/`IsFalse` need `bool`/`bool?`, `IsNull`/`IsNotNull` need a nullable type, and `Equals`/`NotEquals` on a struct need an equality operator. `In`/`NotIn` fit every type. [Implementation](../tools/analyzers/Kaleido.Analyzers/Queryable/FilterOperatorTypeAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/Queryable/FilterOperatorTypeAnalyzerTests.cs).

- **KAL2021 - describe outputs.** What a step returns and what a view returns is read too: the registry publishes each property's description, and the MCP adapter returns the descriptions of the fields in a result with the data (`fieldDescriptions`), so an agent knows what `resolvedCodeValue: 72142` means. A source's output is its query context, already covered by KAL2015. Reported once per output type declared in the project; types from other assemblies are not the project's to describe. This repository's samples treat it as an error (the `[samples/**/*.cs]` section of `.editorconfig`). [Implementation](../tools/analyzers/Kaleido.Analyzers/OutputDescriptionAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.UnitTests/OutputDescriptionAnalyzerTests.cs).

Compliant shapes for all of the above are in [`PATTERNS.md`](./PATTERNS.md).

---

## Source rules — `KAL0xxx`

**Audience:** contributors changing the framework. **Project:** [`Kaleido.Analyzers.Source`](../tools/analyzers/Kaleido.Analyzers.Source/), referenced by every `src/` project through `src/Directory.Build.props`; never shipped.

| ID | Rule | Diagnostic message |
|---|---|---|
| KAL0001 | Static classes are reserved for extension methods — no static helper/util classes | `Static class '{0}' is reserved for extension methods — convert '{1}' to an extension method or use a non-static class` |
| KAL0002 | Throw `Kaleido*Exception` types, not general BCL exceptions (argument and format guards, the ASP.NET 400 contract and the MCP input-required signal are allow-listed) | `Throw a Kaleido*Exception instead of '{0}'` |
| KAL0003 | No `!` null-forgiving operator — use explicit null checks | `Replace '!' with an explicit null check` |
| KAL0004 | Service-like types must not be static (name ends `Service/Handler/Executor/Processor/Provider/Factory/Reader/Writer`) | `Static class '{0}' looks like an injectable service ('{1}' is not an extension method) — make it injectable or rename it` |
| KAL0005 | No `new` on DI-registered implementation types | `Type '{0}' is a DI-registered service implementation — resolve it from DI instead of newing it up` |
| KAL0006 | No service locator — `IServiceProvider.GetService/GetServices/GetRequiredService` outside composition roots | `'{0}' on IServiceProvider is a service locator call — inject the dependency through the constructor instead` |
| KAL0007 | Inject the service abstraction, not the concrete implementation | `Parameter '{0}' is typed as concrete '{1}' — inject '{2}' instead` |
| KAL0008 | Services use constructor injection only — no settable service properties or `Set*` injection methods | `'{0}' on service '{1}' injects '{2}' outside the constructor — use constructor injection` |
| KAL0009 | Injected deps must be retained in readonly fields (unused ctor params are caught by IDE0060) | `Field '{0}' on service '{1}' holds '{2}' — make it readonly` |
| KAL0010 | DI constructors must not perform work on injected dependencies | `Invocation '{0}' inside '{1}'s constructor does work — assign dependencies and validate arguments only` |
| KAL0011 | No manual infrastructure instantiation (`HttpClient`, `ServiceCollection`, `ServiceProvider`, `LoggerFactory`, `DbContext`) | `'new {0}()' bypasses the container-managed factory — inject the corresponding abstraction instead` |
| KAL0012 | Do not dispose container-owned dependencies | `'{0}' on '{1}' disposes a container-owned dependency ('{2}') — the container manages its lifetime` |
| KAL0013 | Singleton registrations must not resolve scoped services | `Singleton factory resolves '{0}' which is registered as Scoped — the scoped instance would be captured for the app lifetime` |
| KAL0014 | Interface must live in the same file as its same-named implementation (`IProcessorRuntime` in `ProcessorRuntime.cs`); provider contracts exempt | `Interface '{0}' should be declared in '{1}' alongside '{2}' — an interface and its concrete class share a file` |
| KAL0015 | Public and internal API members must not expose mutable collection types (`List<T>`, `IList<T>`, `Dictionary<K,V>`, `IDictionary<K,V>`, `HashSet<T>`, `ISet<T>`, `ICollection<T>`) | `'{0}' is a mutable collection type — use IReadOnlyCollection<T>, IReadOnlyList<T>, IReadOnlyDictionary<K,V> or IEnumerable<T>, or FrozenDictionary<K,V>/FrozenSet<T> for a collection built once and only read` |
| KAL0016 | Public and internal async methods (`Task`, `Task<T>`, `ValueTask`, `ValueTask<T>`) must accept a `CancellationToken`, **without a default value** | `Async method '{0}' does not accept a CancellationToken ...` / `Async method '{0}' has a CancellationToken with a default value ...` |
| KAL0017 | ASP.NET Core `WithName(...)` must take a constant or factory method from a type whose name ends in `EndpointNames` | `Pass a member of an *EndpointNames type to WithName instead of '{0}'` |
| KAL0018 | A catch-all (`catch` / `catch (Exception)`) that calls a member on an `*Observation`/`*Observability` type must exclude `OperationCanceledException`, via a `when` filter or an earlier `catch (OperationCanceledException)` | `This catch-all calls '{0}' but does not exclude OperationCanceledException — add 'when (ex is not OperationCanceledException)' or an earlier catch (OperationCanceledException)` |
| KAL0019 | A type with behavior is declared in a file named after it - its [convention name](#types-with-behavior) (`ProcessStepHandlerResult<T>` in `ProcessStepHandlerResultOfT.cs`); partial parts in `{Name}.{Part}.cs` | `Type '{0}' has behavior and must be declared in '{1}' (or a '{2}.*.cs' part), not '{3}' — a type with behavior has its own file and its own test class` |
| KAL0020 | `[ExcludeFromCodeCoverage]` is not allowed on a type with behavior | `Type '{0}' has behavior: remove [ExcludeFromCodeCoverage] and test it in its own test class` |

### What the security-relevant rules prevent

| Rule | What it prevents |
|---|---|
| KAL0005 | `new`-ing a DI-registered service — loses container lifetime/scoping guarantees |
| KAL0006 | Service-locator calls outside composition roots — hides dependencies from validation |
| KAL0007 | Injecting concrete types — breaks substitutability, lets callers reach internals |
| KAL0008 | Property/`Set*` injection on services — services become incompletely initialized |
| KAL0009 | Non-readonly injected fields — a dependency replaced after construction |
| KAL0010 | Constructor work on injected deps — side effects at resolution time, ordering hazards |
| KAL0011 | Manual `new HttpClient`/`ServiceProvider`/`DbContext` — socket exhaustion, leaked scopes |
| KAL0012 | Disposing container-owned dependencies — disposed-injected-dependency crashes mid-request |
| KAL0013 | Singleton capturing a scoped service — captive dependency, stale per-request state |
| KAL0015 | Mutable collection types on the API surface — callers can corrupt shared state |
| KAL0016 | Async methods without a `CancellationToken`, or with a defaulted one a caller can silently omit - cancellations stop propagating (#313) |
| KAL0017 | Ad-hoc endpoint names — typos break link generation and OpenAPI operation ids silently |
| KAL0018 | Cancellations recorded as failures in observability catch-alls — inflated error metrics, false alerts |
| KAL0019 | Logic hidden in another type's file - its test class is missing without anyone seeing it, and indirect tests stand in for it |
| KAL0020 | Logic excluded from the coverage gate |

### Scope notes

**DI rules (KAL0004–KAL0013).** The registered-service model is harvested from `*ServiceCollectionExtensions` classes in the same compilation (generic args, `typeof()` args, returned `new` in factory lambdas). `*ServiceCollectionExtensions` and `*EndpointRouteBuilderExtensions` are composition roots — exempt. `context.RequestServices` resolution (middleware/endpoint activation) and dynamic resolutions (runtime `Type` args, open-generic type parameters) are exempt from KAL0006 — they are the container's dispatch seam. So is resolution from a scope created in the same method (`using var scope = scopeFactory.CreateScope(); scope.ServiceProvider.Get…`): a child scope's services can never be constructor-injected (injection yields the parent scope's instance), so that scope is a composition seam. Resolution from an injected `IServiceProvider` or `IServiceScope` is still reported. Tests are exempt via `.editorconfig`.

**API design rules (KAL0015–KAL0016).** KAL0015 applies to all `src/` projects (suppressed for `Kaleido.Provider.SQLite` where EF Core entity navigation properties conventionally use `ICollection<T>`). **Ownership:** the diagnostic is reported at the member that declares the contract, once. An override, or an explicit or implicit interface implementation, is never reported: its type was fixed by the base or interface, which is reported itself when it is source in this compilation; a contract from another assembly (metadata) is that assembly's to report. Generated code is not analyzed, and a deliberate mutable surface is suppressed at its declaration (#306). A collection built once and only read (registries, lookups) is a `FrozenDictionary<K,V>`/`FrozenSet<T>`; one filled after construction is written through methods and exposed read-only - no `#pragma` needed. KAL0016 applies to all `src/` projects: every caller passes its token on, so no public or internal async method defaults it (#313). Exempt: overrides and interface implementations (explicit or implicit; the signature is the base's or interface's, so an implementation of an external interface such as `IHealthCheck` keeps its default, which MA0061 requires), and the ASP.NET Core `InvokeAsync(HttpContext)` and `BindAsync(HttpContext)` conventions, whose token is `HttpContext.RequestAborted`.

**Cancellation in observability catch-alls (KAL0018).** The filter and earlier-clause rules are shared with KAL2004; see [how a filter is read](#cancellation-filters). The observability call is any member of a type whose name contains `Observation` or `Observability`.

**One type with behavior per file (KAL0019, KAL0020).** Both use the shared [definition of behavior](#types-with-behavior), the same one KAL1009 uses, so the file rule and the fixture rule never disagree. Data-only records, interfaces (KAL0014 keeps one with its implementation), exceptions and attributes may share a file; MA0048 stays off for them. Files written by generators (`obj/`, `*.g.cs`) are skipped.

**Endpoint names (KAL0017).** Endpoint names are declared as `const string` fields or name-factory methods on a `*EndpointNames` class in `Kaleido.Http.Abstractions` (`ProcessEndpointNames`, `RegistryEndpointNames`) or `Kaleido.Http` (`QueryableEndpointNames`). All names follow one pattern, `Kaleido` + area + action with `_` before any variable part (`KaleidoProcessExecute`, `KaleidoProcessStepExecute_{step}`, `KaleidoQueryableQuery_{source}`, `KaleidoQueryableViewQuery_{source}_{view}`, `KaleidoRegistry`); the prefix keeps them from colliding with the host app's own endpoint names. An inline literal or a constant from any other type passed to `WithName()` is reported. Endpoint names are link targets and OpenAPI operation ids, and an unenforced literal let the `KaleidoProcessStepREgistry` typo ship (HP-015, [#62](https://github.com/no1ross/Kaleido/issues/62)).

### Examples

- **KAL0003 — make null assumptions visible.** `typeof(Widget).GetMethod("Run")!` hides a missing-reflection-member failure; `typeof(Widget).GetMethod("Run") ?? throw new KaleidoFrameworkException(FrameworkErrorCodes.ReflectionError, "Run was not found.")` handles it explicitly. Test code legitimately uses `!` in assertion/stub setups and is exempted in `.editorconfig`. [Implementation](../tools/analyzers/Kaleido.Analyzers.Source/Design/NullForgivingOperatorAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Source.UnitTests/Design/NullForgivingOperatorAnalyzerTests.cs).
- **KAL0014 — keep a local interface with its implementation.** If `IWidgetService` and its same-named `WidgetService` implementation live in one assembly, declare both in `WidgetService.cs` instead of a separate `IWidgetService.cs`. Provider contracts such as `IKaleidoContextStore`, which have no same-named implementation in that assembly, are exempt. This is a repository layout convention, not a runtime correctness check. [Implementation](../tools/analyzers/Kaleido.Analyzers.Source/Layout/InterfaceCoLocationAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Source.UnitTests/Layout/InterfaceCoLocationAnalyzerTests.cs).

For the remaining rules, see their implementations under [`Kaleido.Analyzers.Source`](../tools/analyzers/Kaleido.Analyzers.Source/) when changing a convention.

---

## Test rules — `KAL1xxx`

**Audience:** contributors writing tests. **Project:** [`Kaleido.Analyzers.Testing`](../tools/analyzers/Kaleido.Analyzers.Testing/), referenced by every test project through `tests/Directory.Build.props`; never shipped. Individual rules have their own applicability checks and `.editorconfig` exemptions: KAL1006 is disabled for functional/integration tests, KAL1009 runs only in `*.UnitTests` assemblies, and KAL1010 applies wherever its matching test-project code occurs.

| ID | Rule | Diagnostic message |
|---|---|---|
| KAL1001 | Test fixtures are named after their subject under test: `{SutName}Tests` | `Test fixture '{0}' must be named after its subject under test ('{1}Tests')` |
| KAL1002 | The fixture's `{Sut}Tests` prefix must resolve to a real type (`XOfTTests` resolves to `X` with one type parameter, `XOfT{n}Tests` with n; a plain `XTests` to the lowest arity) | `Test fixture '{0}' does not map to a type named '{1}' — fixtures are named {SutName}Tests` |
| KAL1003 | The fixture's file location mirrors the SUT's location: `src/{Project}/{path}/Sut.cs` → `tests/{TestProject}/{path}/SutTests.cs`. A SUT from a referenced assembly (every `src/` project, for a unit-test project) is found among the `src/**/*.cs` paths `tests/Directory.Build.props` passes as additional files, by its convention file name | `Test fixture '{0}' must live at '{1}' (mirroring SUT path '{2}')` |
| KAL1004 | One fixture per subject under test | `Test fixture '{0}' duplicates '{1}' — both map to SUT '{2}'` |
| KAL1005 | Test-built `ServiceProvider`s must validate scopes and validate on build | `BuildServiceProvider() must pass 'new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }'` |
| KAL1006 | Unit-test fixtures must inherit `SutFixture<TSut>` — every test declares its SUT | `Test fixture '{0}' must inherit SutFixture<{1}> — every unit test declares its subject under test` |
| KAL1007 | Fixture name must equal `{ConventionName}Tests` (`ProcessStepHandlerResultOfTTests` for `SutFixture<ProcessStepHandlerResult<T>>`) | `Test fixture '{0}' declares SUT '{1}' — it must be named '{2}'` |
| KAL1008 | The SUT may only be constructed inside `CreateSut()` | `'{0}' may only be constructed inside CreateSut() — use CreateSut() or the Sut property` |
| KAL1009 | Every [type with behavior](#types-with-behavior) in the matching `Kaleido.*` source assembly must have its own `{ConventionName}Tests` fixture | `Type '{0}' in '{1}' has no '{2}' fixture — every type with behavior must have its own unit-test fixture` |
| KAL1010 | A concrete `*Tests` class that declares ordinary methods but no `[Fact]`/`[Theory]` is a helper-only stub, not evidence of tests; a class with no ordinary methods is exempt | `Fixture '{0}' has no [Fact] or [Theory] test methods — add at least one test or remove the empty stub` |
| KAL1011 | Fixtures may not `new` a framework collaborator — a Kaleido class with methods implementing a service interface (records, DTOs and exceptions are data, not collaborators); mock it instead | `'{0}' is a collaborator, not the SUT — mock it instead of new-ing a real instance` |
| KAL1012 | A unit-test fixture is declared in its SUT's namespace with `.UnitTests` appended (`Kaleido.Queryable.Query.QueryContextEngine` → `Kaleido.Queryable.Query.UnitTests`); `*.UnitTests` assemblies only | `Test fixture '{0}' tests '{1}' — declare it in namespace '{2}'` |

**Effective severities and why each rule stays (#307).** These are repository conventions, not runtime-correctness checks; each is kept because it makes the suite predictable to navigate and to generate:
- every KAL1xxx rule is an `Error`, except **KAL1009**, a `Warning` by default (a new type without a fixture is flagged without breaking the build); KAL1006 is off in the functional and integration test projects (their fixtures are hosts, not a single SUT);
- **KAL1009** (a fixture per type with behavior) - kept: it is what makes "where is X tested" answerable by name; since #404 it judges behavior, not type kind, so a record's logic is tested in its own fixture rather than through another type's;
- **KAL1011** (mock collaborators) - kept with its exemptions, which are the justified counterexamples and are tested: domain data and records/DTOs implementing no Kaleido interface, test-assembly doubles, BCL and third-party scaffolding (`ServiceCollection`, `HttpClient`), and the SUT itself;
- the fixture-shape rules (KAL1003, KAL1006-KAL1008, KAL1010, KAL1012) - kept: they keep one SUT per fixture, found at the mirrored path and namespace;
- **KAL0014** (an interface in its implementation's file) - kept as a layout convention; provider contracts are exempt.

### Types with behavior

One definition, shared by KAL0019, KAL0020 and KAL1009 ([`TypeBehavior`](../tools/analyzers/Shared/TypeBehavior.cs), #404), decides which types need their own file and their own unit-test class:
- **Candidates:** top-level (not nested), non-abstract, non-static public or internal classes, records and structs. Exceptions, attributes and EF Core model configuration (`IEntityTypeConfiguration<T>`, `DbContext`, covered end to end by the store's tests) are exempt.
- **Behavior:** a member written by hand - an ordinary method (instance or static), a user-defined operator or conversion, or a property accessor with a body (explicit interface implementations included). Auto-properties, positional record members, compiler-written record members and generated code (`[LoggerMessage]`) do not count; neither do constructors (not visible from metadata). The analyzers cannot tell trivial logic from real logic: a one-line factory counts.
- **Convention name:** the type name; for a generic type with a same-name, lower-arity twin in its namespace, the name with its arity - `XOfT` for one type parameter, `XOfT{n}` for more (`ProcessStepHandlerResult<T>`: `ProcessStepHandlerResultOfT.cs`, `ProcessStepHandlerResultOfTTests`). The type keeps its name in code.

A type with behavior may still be *used* as input data by another type's tests (KAL1011 allows domain data), but its own logic is tested only in its own fixture.

### SutFixture notes (KAL1006–KAL1009)

`SutFixture<TSut>` lives in `Kaleido.UnitTests` (the base test project — other `*.UnitTests` projects reference it). KAL1009 runs only in `*.UnitTests` assemblies; KAL1006 is disabled for functional/integration projects in `.editorconfig`. Those suites are scenario-scoped and do not normally use `SutFixture`, while the other fixture-shape rules activate only when their specific fixture pattern occurs.

### Examples

- **KAL1010 — no helper-only fixtures.** A concrete `WidgetTests : SutFixture<Widget>` with `CreateSut()` and only helper methods, but no `[Fact]` or `[Theory]`, reports KAL1010; add a real test rather than an empty assertion. A `*Tests` class with **no ordinary methods at all** is intentionally skipped, as are abstract/static or differently named classes. KAL1009 independently looks for an actual `[Fact]`/`[Theory]` fixture for a source type with behavior in a matching `*.UnitTests` assembly. [Implementation](../tools/analyzers/Kaleido.Analyzers.Testing/Fixtures/FixtureEmptyAnalyzer.cs) · [tests](../tests/Kaleido.Analyzers.Testing.UnitTests/Fixtures/FixtureEmptyAnalyzerTests.cs).
