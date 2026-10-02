# Kaleido canonical patterns

Single source of truth for the conventions a consumer (or code generator) must
follow. Each pattern links the enforcing analyzer where one exists — see
[`ANALYZERS.md`](./ANALYZERS.md).

## Vocabulary

- **Processor** — the unit that owns and executes steps (runtime/DI identity).
- **Process** — one executing instance of a processor (wire/transport domain).
- **Step / Handler** — `[ProcessStep]` POCO + `IProcessStepHandler<T>` pair.

## Step + handler

```csharp
[ProcessStep(Name = "capture-service", Version = "1.0")]
public sealed class CaptureServiceStep { /* input fields */ }

public sealed class CaptureServiceHandler(IHistoryClient history)
    : IProcessStepHandler<CaptureServiceStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureServiceStep step, ProcessStepContext context, CancellationToken ct)
    {
        // ... domain work ...
        return ProcessStepHandlerResult.Complete();
    }
}
```

Rules: step class ends in `Step` (KAL2007); `[ProcessStep]` declares non-empty
`Name`/`Version` (KAL2001); handler must not swallow
`OperationCanceledException` in `catch (Exception)` — filter it or let the
executor own cancellation (KAL2004).

## Process authoring

```csharp
services.AddKaleido(builder.Configuration, o =>
{
    o.ServiceName = "intake";                       // lowercase, no separators (KAL2005)
    o.Assemblies = [typeof(Program).Assembly];      // explicit — never omit (KAL2009)
})
    .AddHttp();
```

`AddKaleido()` auto-registers Processor + Queryable runtimes and binds
`Kaleido:*` config into an immutable `KaleidoServiceOptions` snapshot. Set
options via the `KaleidoServiceOptionsBuilder` configure lambda — the
registered options are init-only.

`AddHttp()` wires `ExceptionMiddleware` + `ObservabilityMiddleware` via
`KaleidoStartupFilter`. Opt out with `o.AutoRegisterMiddleware = false` when
the host owns middleware ordering.

## Queryable sources — sync vs async

- `IQueryContextSource<T>` — the queryable is already in hand (in-memory,
  pre-fetched, or deferred LINQ). Prefer this.
- `IQueryContextSourceAsync<T>` — producing the queryable needs async I/O.
- Same split for views: `IQueryViewSource` vs `IQueryViewSourceAsync`.
- Never `.Result`/`.GetAwaiter().GetResult()` inside a sync source.

## Exceptions

Throw `Kaleido*Exception` types only (KAL0002): `KaleidoValidationException`
(400 surface), `KaleidoConfigurationException` (startup), `KaleidoFrameworkException`
(internal integrity). Argument guards (`ArgumentNullException`) are fine.
Exception types are classes, never records (KAL0004/KAL1011).

## Dependency injection

Constructor injection, service abstractions, readonly fields — enforced by
KAL0005–KAL0014 (see the security-relevant table in `ANALYZERS.md`). AddHttpClients /
AddKaleido configure lambdas are composition roots; `IServiceProvider` resolution
belongs there or in `HttpContext.RequestServices` — nowhere else.

## Static helpers vs extension methods

Convention (KAL0001 + MP-027): **a transformation that derives from one type is
an extension on that type** — `query.ToApiBody()`, `type.GetQueryViewInterface()`.
**Orchestration stays a helper/builder** — registry compile passes, metadata
composition, multi-source assembly. `static` classes exist only to host
extension methods.

## Correlation

Never set `X-Kaleido-*` headers by hand; never mint a second `RequestId`.
Ambient identity flows through `IKaleidoCorrelationContextAccessor` (scoped)
and is forwarded automatically by the typed clients. See the invariants in
`src/ARCHITECTURE.md` → Correlation context invariants.

## Unit tests

One fixture per SUT, named `{Sut}Tests`, mirroring the SUT's path
(KAL1001–KAL1004). Inherit `SutFixture<TSut>` and construct only inside
`CreateSut()` (KAL1006–KAL1008). Mock collaborators — `new` on a Kaleido
service interface implementation is KAL1012. `BuildServiceProvider` must pass
`ValidateScopes + ValidateOnBuild` (KAL1005).

## Endpoint metadata

Endpoint names are `const` fields in `*EndpointNames` classes in
`Kaleido.Http.Abstractions` — never inline literals in `WithName()`.
