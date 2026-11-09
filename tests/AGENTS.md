## Test project structure

Unit-test suites generally mirror source and analyzer projects; functional and integration suites exercise composed behavior:

| Test project | Tests code in |
|---|---|
| `Kaleido.UnitTests` | `src/Kaleido` core runtime |
| `Kaleido.Platform.UnitTests` | `src/Platform` contracts, registry status and HTTP headers |
| `Kaleido.Server.UnitTests` | `src/Server` authorization, caller/correlation and registry filtering |
| `Kaleido.Client.UnitTests` | `src/Client` participation, hydration and transport selection |
| `Kaleido.Http.UnitTests` | `src/Http/Http` versioned URL and route shapes |
| `Kaleido.Http.Server.UnitTests` | `src/Http/Http.Server` endpoint adapters |
| `Kaleido.Http.Client.UnitTests` | `src/Http/Http.Client` HTTP clients and handlers |
| `Kaleido.Http.FunctionalTests` | HTTP server/client contracts with TestServer and the central registry |
| `Kaleido.Registry.UnitTests` | `src/Registry/Registry` registration, trust, leases, views, cache and store |
| `Kaleido.Registry.Http.UnitTests` | `src/Registry/Registry.Http` endpoint mapping |
| `Kaleido.Mcp.UnitTests` | `src/Mcp/Mcp` catalogs, schemas, tool invoker and naming |
| `Kaleido.Mcp.FunctionalTests` | MCP over a real host and the SDK client |
| `Kaleido.Provider.SQLite.UnitTests` | `src/Providers/Provider.SQLite` store and transaction fidelity |
| `Kaleido.Observability.OpenTelemetry.UnitTests` | `src/Observability/Observability.OpenTelemetry` registration and telemetry names |
| `Kaleido.Analyzers.UnitTests` | consumer `KAL2xxx` rules |
| `Kaleido.Analyzers.Source.UnitTests` | contributor `KAL0xxx` rules |
| `Kaleido.Analyzers.Testing.UnitTests` | test `KAL1xxx` rules |
| `Kaleido.IntegrationTests` | cross-project DI, EF Core, caller credentials and compiled consumer-guide examples |

## Warnings
Test projects build like `src/`: a warning is an error, and IDE code-style rules (unused usings, naming,
braces...) run in the build (#406, root [`AGENTS.md`](../AGENTS.md#per-commit-quality-gate)).

## Testing conventions
- When adding unit tests, scope them to a single class
- Mock injected dependencies with `Moq`
- In unit tests, verify that the class behaves as designed at its seam/contract boundary
- Do not turn unit tests into business workflow, transport, or integration tests
- Use functional tests to validate transport, real implementations, and broader business behavior
- Never wait on the wall clock for a time-dependent decision (a lease, an expiry, a retry, a TTL, a
  circuit's break): give the SUT a `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`),
  advance it, and test the boundary one tick before, at and after it (#460). Wait for background work
  with a `TaskCompletionSource` gate, not a `Task.Delay`.

**Why collaborators are always mocked (the mocking policy, #461).** A unit test tests one unit. Its
collaborators are mocked (KAL1011), so changing class B never fails class A's tests, and finding the
tests for a class is a matter of its name - not of searching every test that happens to run it
through another class. How the classes work together - the flow - is what the functional and
integration tests are for. This is deliberate; do not replace mocks with real collaborators to make
a unit test "more realistic" - write or extend a functional test instead.

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

**Which types get a fixture (#404).** Every type with behavior - a hand-written method, operator or
computed property, whatever its kind: records and `*Options`/`*Request` types included - has its
own fixture (KAL1009), and lives in its own file (KAL0019). The definition is in
[`docs/ANALYZERS.md`](../docs/ANALYZERS.md#types-with-behavior).
- **One SUT, its own logic.** An injected collaborator is mocked, never tested through (KAL1011). A
  record or other data type with behavior may be *used* as input in another fixture, but its logic is
  tested in its own fixture: `ProcessorContextTests` covers `HasCompletedStep`; the runtime's tests
  only pass contexts in.
- **Generic twins** keep their type names; files and fixtures carry the arity:
  `ProcessStepHandlerResult` → `ProcessStepHandlerResultTests`, `ProcessStepHandlerResult<T>` →
  `ProcessStepHandlerResultOfTTests` (`XOfT{n}Tests` with n type parameters).
- `[ExcludeFromCodeCoverage]` is for data types only (KAL0020): logic is measured and tested.

Rules enforced at build time: fixtures are named `{Sut}Tests` (KAL1007; `{Sut}OfTTests` for a generic twin), the
SUT may only be constructed inside a `CreateSut` method or via `Sut` (KAL1008),
collaborators must be mocked — never `new` a real Kaleido service
implementation (KAL1011; domain data the SUT consumes is exempt) — and the
fixture file must mirror the SUT's source path (KAL1003 — e.g.
`src/Kaleido/Queryable/Metadata/QueryMetadataExtensions.cs` → `tests/Kaleido.UnitTests/Queryable/Metadata/QueryMetadataExtensionsTests.cs`;
the analyzer finds the SUT's file among the `src/` paths `tests/Directory.Build.props` passes as
additional files).
The fixture's namespace is the SUT's namespace with `.UnitTests` appended, not
the folder path (KAL1012 — e.g. `Kaleido.Queryable.Query.QueryContextEngine` →
`namespace Kaleido.Queryable.Query.UnitTests;`), so the SUT's types resolve
without a `using`. Shared helpers (`SutFixture`, analyzer harnesses) stay in
`Kaleido.UnitTests` / `Kaleido.Testing`; functional and integration test
projects keep project-based namespaces.
A unit test exercises exactly one SUT; assembled-pipeline behavior belongs in
functional or integration tests.

## Functional test fixtures

### What a functional test is for
A functional test proves that real Kaleido components agree with each other: it configures a host,
performs a consumer action, and asserts the consumer-visible result (records, state read back with
`GET /processes/{id}`, error codes, registry metadata, MCP tool results) - not which collaborator
was called. Before adding one, name the production regression it would catch; if none, it belongs in
a unit test or nowhere. Prove it catches that regression once: apply the mutation to the production
code (a reversed condition, a dropped registration, ignored metadata), see the test fail, restore.
A flow that still lacks such a test, or a risk found while testing, is an Issue (e.g. #503), never a
list in a document.

Not functional-tested on purpose (unit tests own them): startup registration failures (a host cannot
start with them), per-operator and per-type exhaustiveness and error-code strings (one functional case
per class of behavior), and internals such as relay timing, cache TTLs, the header sanitizer, telemetry
names, option validation, tool-name truncation and catalog-cache eviction.

Models for one scenario family live in their own namespace and are registered with a `TypeFilter` on
that namespace (e.g. `Processor/Behavior`, `IntegrationTests/EfCore`), so they cannot change what the
shared fixtures publish.

### Shared fixture state
`ProcessorAspNetCoreFixture` and `QueryableAspNetCoreFixture` expose:
- `Client` — a pre-wired `HttpClient` pointed at the TestServer (use for raw HTTP assertions)
- `ClientFactory` — the Kaleido client factory wired against the TestServer (use for client-level tests)
- `TestServer` — the underlying `TestServer` instance (use when a test needs its own `CreateHandler()`)

Both fixtures are in `tests/Http/Http.FunctionalTests`.

### Real authentication: the shared test identity (#574)
Tests of who may call, and of what a call carries for its caller, authenticate as a production service does: ASP.NET's JWT bearer handler over real signed tokens, not a header-driven test scheme.
- `tests/Shared/Identity` (compiled into the functional projects that use it): `TestTokenIssuer` signs user tokens (`sub`, `preferred_username`, `roles`, the app as `azp`), app-only tokens (client credentials: `azp`, `client_id`, no user) and forged ones; `AddTestJwtBearer()` makes a host trust it (`MapInboundClaims = false`, roles in `roles`).
- Such hosts run as Production and are called over `https://` (TestServer ignores the scheme), so production-only checks apply to them. `JwtAuthorizationFixture` is the example.

### Writing tests that need their own DI container

Some tests (e.g. correlation-header tests) need a fresh `ServiceCollection` with a custom
`IHttpClientFactory` or a controllable `IKaleidoCorrelationContextAccessor`. Two important pitfalls apply:

#### Pitfall 1 — IHttpClientFactory pipeline wiring is unreliable in test providers

`AddHttpMessageHandler<T>()` and `ConfigurePrimaryHttpMessageHandler()` on an `IHttpClientBuilder`
work correctly in production but have been found to silently produce pipelines that skip registered
delegating handlers when the named client is registered twice (once by `AddHttpClients()` and once
explicitly in test setup).

**Do not** try to inject a capture/spy handler through the `IHttpClientBuilder` callback in tests.

**Do** bypass `IHttpClientFactory` entirely by registering a `FixedHttpClientFactory` singleton that
returns a manually-composed `HttpClient`:

```csharp
var captureHandler = new RequestCaptureHandler() { InnerHandler = fixture.TestServer.CreateHandler() };
var httpClient = new HttpClient(captureHandler) { BaseAddress = new Uri("http://localhost/") };

services.AddSingleton<IHttpClientFactory>(new FixedHttpClientFactory("kaleido-gateway", httpClient));
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
services.AddKaleido(config, new() { /* ... */ }).AddHttpClients();
```
