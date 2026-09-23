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

### DI rule notes (KAL0005–KAL0014)

The registered-service model is harvested from `*ServiceCollectionExtensions` classes in the same compilation (generic args, `typeof()` args, returned `new` in factory lambdas). `*ServiceCollectionExtensions` and `*EndpointRouteBuilderExtensions` are composition roots — exempt. `context.RequestServices` resolution (middleware/endpoint activation) and dynamic resolutions (runtime `Type` args, open-generic type parameters) are exempt from KAL0007 — they are the container's dispatch seam. Tests are exempt via `.editorconfig`.

## Test rules — `KAL1xxx`

Scoped to unit-test projects via `.editorconfig`.

| ID | Rule | Diagnostic message |
|---|---|---|
| KAL1001 | Test fixtures are named after their subject under test: `{SutName}Tests` | `Test fixture '{0}' must be named after its subject under test ('{1}Tests')` |
| KAL1002 | The fixture's `{Sut}Tests` prefix must resolve to a real type | `Test fixture '{0}' does not map to a type named '{1}' — fixtures are named {SutName}Tests` |
| KAL1003 | The fixture's file location mirrors the SUT's location: `src/{Project}/{path}/Sut.cs` → `tests/{TestProject}/{path}/SutTests.cs` | `Test fixture '{0}' must live at '{1}' (mirroring SUT path '{2}')` |
| KAL1004 | One fixture per subject under test | `Test fixture '{0}' duplicates '{1}' — both map to SUT '{2}'` |
| KAL1005 | Test-built `ServiceProvider`s must validate scopes and validate on build | `BuildServiceProvider() must pass 'new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true }'` |
