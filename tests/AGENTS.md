## Test project structure

Unit-test suites generally mirror source and analyzer projects; functional and integration suites exercise composed behavior:

| Test project | Tests code in |
|---|---|
| `Kaleido.UnitTests` | `src/Kaleido` (core runtime: bootstrap, Process, Queryable) |
| `Kaleido.Http.UnitTests` | `src/Kaleido.Http` (middleware, startup filter, correlation reader, endpoint extensions, queryable value normalizer) |
| `Kaleido.Http.FunctionalTests` | `src/Kaleido.Http` and client contracts through TestServer |
| `Kaleido.Http.Client.UnitTests` | `src/Kaleido.Http.Client` |
| `Kaleido.Http.Abstractions.UnitTests` | `src/Kaleido.Http.Abstractions` (`HttpHeaderSanitizer` and contract types) |
| `Kaleido.Provider.SQLite.UnitTests` | `src/Kaleido.Provider.SQLite` (`IProcessorContextStore` contract and SQLite fidelity) |
| `Kaleido.Analyzers.UnitTests` | `tools/analyzers/Kaleido.Analyzers` (consumer `KAL2xxx` rules) |
| `Kaleido.Analyzers.Source.UnitTests` | `tools/analyzers/Kaleido.Analyzers.Source` (contributor `KAL0xxx` rules) |
| `Kaleido.Analyzers.Testing.UnitTests` | `tools/analyzers/Kaleido.Analyzers.Testing` (test `KAL1xxx` rules) |
| `Kaleido.IntegrationTests` | cross-project DI and EF Core registration behavior |

## Testing conventions
- When adding unit tests, scope them to a single class
- Mock injected dependencies with `Moq`
- In unit tests, verify that the class behaves as designed at its seam/contract boundary
- Do not turn unit tests into business workflow, transport, or integration tests
- Use functional tests to validate transport, real implementations, and broader business behavior

## Unit test fixtures (SutFixture)

Every unit-test fixture must declare its subject under test by inheriting
`SutFixture` (`tests/Shared/SutFixture.cs`), enforced by analyzers KAL1006–KAL1010
(see `docs/ANALYZERS.md`):

- **Public SUT** — inherit `SutFixture<TSut>` and override `CreateSut()`; the
  `Sut` property is available for per-test construction.
- **Internal SUT** — inherit the non-generic `SutFixture` and add a
  `private static TSut CreateSut(...)` overload (a public `Sut` property would
  violate accessibility, CS9338). Parameterize `CreateSut` when tests need
  custom dependencies.
- **Static SUT** (e.g. `*Extensions`) — inherit the non-generic `SutFixture`;
  no `CreateSut` needed.

Rules enforced at build time: fixtures are named `{Sut}Tests` (KAL1007), the
SUT may only be constructed inside a `CreateSut` method or via `Sut` (KAL1008),
collaborators must be mocked — never `new` a real Kaleido service
implementation (KAL1012; domain data the SUT consumes is exempt) — and the
fixture file must mirror the SUT's source path (KAL1003 — e.g.
`src/Kaleido/Json/ValueConverter.cs` → `tests/Kaleido.UnitTests/Json/ValueConverterTests.cs`).
The fixture's namespace is the SUT's namespace with `.UnitTests` appended, not
the folder path (KAL1013 — e.g. `Kaleido.Queryable.Query.QueryContextEngine` →
`namespace Kaleido.Queryable.Query.UnitTests;`), so the SUT's types resolve
without a `using`. Shared helpers (`SutFixture`, analyzer harnesses) stay in
`Kaleido.UnitTests` / `Kaleido.Testing`; functional and integration test
projects keep project-based namespaces.
A unit test exercises exactly one SUT; assembled-pipeline behavior belongs in
functional or integration tests.

## Functional test fixtures

### Shared fixture state
`ProcessAspNetCoreFixture` and `QueryableAspNetCoreFixture` expose:
- `Client` — a pre-wired `HttpClient` pointed at the TestServer (use for raw HTTP assertions)
- `ClientFactory` — the Kaleido client factory wired against the TestServer (use for client-level tests)
- `TestServer` — the underlying `TestServer` instance (use when a test needs its own `CreateHandler()`)

Both fixtures are in `tests/Kaleido.Http.FunctionalTests`.

### Writing tests that need their own DI container

Some tests (e.g. correlation-header tests) need a fresh `ServiceCollection` with a custom
`IHttpClientFactory` or a controllable `IKaleidoCorrelationContextAccessor`. Two important pitfalls apply:

#### Pitfall 1 — IHttpClientFactory pipeline wiring is unreliable in test providers

`AddHttpMessageHandler<T>()` and `ConfigurePrimaryHttpMessageHandler()` on an `IHttpClientBuilder`
work correctly in production but have been found to silently produce pipelines that skip registered
delegating handlers when the named client is registered twice (once inside `AddQueryableClient` /
`AddProcessorClient` and once explicitly in test setup).

**Do not** try to inject a capture/spy handler through the `IHttpClientBuilder` callback in tests.

**Do** bypass `IHttpClientFactory` entirely by registering a `FixedHttpClientFactory` singleton that
returns a manually-composed `HttpClient`:

```csharp
var captureHandler = new RequestCaptureHandler() { InnerHandler = fixture.TestServer.CreateHandler() };
var httpClient = new HttpClient(captureHandler) { BaseAddress = new Uri("http://localhost/") };

services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory("my-client", httpClient));
```

The `FixedHttpClientFactory` pattern used in `ProcessClientHeaderTests` and
`QueryableClientHeaderTests` is the canonical example.

#### Pitfall 2 — Always build with ValidateScopes + ValidateOnBuild

Any test that builds its own `ServiceProvider` must use:

```csharp
services.BuildServiceProvider(
    new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
```

This catches captive dependency errors (e.g. a singleton consuming a scoped service) at build time
rather than silently at runtime. Both fixtures and all header test containers follow this pattern.

#### Tip — Substituting IKaleidoCorrelationContextAccessor

`AddKaleido()` uses `TryAddScoped` for `IKaleidoCorrelationContextAccessor` and
`IKaleidoCorrelationContextInitializer`, so a pre-existing registration wins and the framework
default is skipped.

Register a test-controlled accessor **before** `AddKaleido()`. In test containers that are built
with `ValidateScopes = true`, use `AddSingleton` (not `AddScoped`) for a fixed/stub accessor so it
is compatible with both root-scope and per-scope resolution:

```csharp
services.AddSingleton<IKaleidoCorrelationContextAccessor>(_ => myAccessor);
services.AddKaleido(config, o => ...).AddHttpClients();
```
