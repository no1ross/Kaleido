## Testing conventions
- When adding unit tests, scope them to a single class
- Mock injected dependencies with `Moq`
- In unit tests, verify that the class behaves as designed at its seam/contract boundary
- Do not turn unit tests into business workflow, transport, or integration tests
- Use functional tests to validate transport, real implementations, and broader business behavior

## Functional test fixtures

### Shared fixture state
`ProcessAspNetCoreFixture` and `QueryableAspNetCoreFixture` expose:
- `Client` — a pre-wired `HttpClient` pointed at the TestServer (use for raw HTTP assertions)
- `ClientFactory` — the Kaleido client factory wired against the TestServer (use for client-level tests)
- `TestServer` — the underlying `TestServer` instance (use when a test needs its own `CreateHandler()`)

### Writing tests that need their own DI container

Some tests (e.g. correlation-header tests) need a fresh `ServiceCollection` with a custom
`IHttpClientFactory` or a controllable `IKaleidoCorrelationContextAccessor`. Two important pitfalls apply:

#### Pitfall 1 — IHttpClientFactory pipeline wiring is unreliable in test providers

`AddHttpMessageHandler<T>()` and `ConfigurePrimaryHttpMessageHandler()` on an `IHttpClientBuilder`
work correctly in production but have been found to silently produce pipelines that skip registered
delegating handlers when the named client is registered twice (once inside `AddQueryableClient` /
`AddProcessClient` and once explicitly in test setup).

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

#### Tip 2 — Substituting IKaleidoCorrelationContextAccessor

`AddKaleido()` uses `TryAddScoped` for `IKaleidoCorrelationContextAccessor` and
`IKaleidoCorrelationContextInitializer`, so a pre-existing registration is respected and the
framework default is skipped.

Register a test-controlled accessor **before** `AddKaleido()` and it will be used by the client
factories:

```csharp
services.AddScoped<IKaleidoCorrelationContextAccessor>(_ => myAccessor);
services.AddKaleido().AddQueryableClient(...);
```