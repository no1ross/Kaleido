# Kaleido analyzer rules

`Kaleido.Analyzers` (in `tools/analyzers`) enforces the codebase's design conventions at compile time. These are build diagnostics, not runtime error codes — runtime codes are documented in [`ERROR_CODES.md`](./ERROR_CODES.md).

Severities are configured in `.editorconfig`. In `src/` warnings are treated as errors; in `tests/` several rules are relaxed where the convention genuinely differs (e.g. `!` usage, `KAL0002` stubs).

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
| KAL0015 | Interface must live in the same file as its same-named implementation (`IProcessRuntime` in `ProcessRuntime.cs`); provider contracts exempt | `Interface '{0}' should be declared in '{1}' alongside '{2}' — an interface and its concrete class share a file` |
| KAL0016 | Every `MapGet`/`MapPost` call must chain `.WithTags(...)` | `MapGet/MapPost call is missing a .WithTags() chain` |
| KAL0017 | Every `.WithTags(...)` chain must include `"Kaleido"` as one of the tag arguments | `WithTags() call is missing the "Kaleido" tag` |
| KAL0018 | Public and internal API members must not expose mutable collection types (`List<T>`, `IList<T>`, `Dictionary<K,V>`, `IDictionary<K,V>`, `HashSet<T>`, `ISet<T>`, `ICollection<T>`) | `'{0}' is a mutable collection type — use IReadOnlyCollection<T>, IReadOnlyList<T>, IReadOnlyDictionary<K,V>, or IEnumerable<T> instead` |
| KAL0019 | Public and internal async methods returning `Task`/`Task<T>` must accept a `CancellationToken` parameter | `Async method '{0}' does not accept a CancellationToken — add 'CancellationToken cancellationToken = default' so callers can propagate cancellation` |

### DI rule notes (KAL0005–KAL0014)

The registered-service model is harvested from `*ServiceCollectionExtensions` classes in the same compilation (generic args, `typeof()` args, returned `new` in factory lambdas). `*ServiceCollectionExtensions` and `*EndpointRouteBuilderExtensions` are composition roots — exempt. `context.RequestServices` resolution (middleware/endpoint activation) and dynamic resolutions (runtime `Type` args, open-generic type parameters) are exempt from KAL0007 — they are the container's dispatch seam. Tests are exempt via `.editorconfig`.

### HTTP endpoint rules notes (KAL0016–KAL0017)

These rules run against `src/Kaleido.Http` only (configured via `.editorconfig`). Every `MapGet`/`MapPost` call must chain `.WithTags(...)` (KAL0016) and that chain must include `"Kaleido"` as one of the tag arguments (KAL0017). The `"Kaleido"` tag is used for OpenAPI grouping and endpoint discovery. Endpoint names must be declared as `const string` fields in a `*EndpointNames` class in `Kaleido.Http.Abstractions` — never as inline literals passed to `WithName()`.

### API design rules notes (KAL0018–KAL0019)

KAL0018 applies to all `src/` projects (suppressed for `Kaleido.Provider.SQLite` where EF Core entity navigation properties conventionally use `ICollection<T>`). Overrides and explicit interface implementations are exempt — the collection type is fixed at the interface/base. KAL0019 applies to all `src/` projects. Overrides, explicit interface implementations, and the ASP.NET Core middleware `InvokeAsync(HttpContext)` convention are exempt from KAL0019.

## Test rules — `KAL1xxx`

Scoped to unit-test projects via `.editorconfig`.

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

### SutFixture notes (KAL1006–KAL1009)

`SutFixture<TSut>` lives in `Kaleido.UnitTests` (the base test project — other `*.UnitTests` projects reference it). Testable means public or internal class, non-static, non-abstract, with at least one ordinary method; records, exceptions, attributes, DTO-suffix types, and static `*Extensions` classes are exempt. These rules apply to `*.UnitTests` projects only — functional/integration test projects are scenario-scoped.
