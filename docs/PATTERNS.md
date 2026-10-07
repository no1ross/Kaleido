# Kaleido canonical patterns

Single source of truth for the conventions a consumer (or code generator) must
follow. Each pattern links the enforcing analyzer where one exists — see
[`ANALYZERS.md`](./ANALYZERS.md).

## Vocabulary

- **Processor** — the unit that owns and executes steps (runtime/DI identity).
- **Process** — one executing instance of a processor (wire/transport domain).
- **Step / Handler** — an `IProcessStep` record (described by `[ProcessStep]`) + `IProcessStepHandler<T>` pair.

## Step + handler

```csharp
[ProcessStep(
    Version = "1.0",
    DisplayName = "Capture service",
    Description = "Captures the requested service for the authorization.")]
[AvailableAfter<CaptureMemberStep>]
public sealed record CaptureServiceStep : IProcessStep { /* input fields */ }

public sealed class CaptureServiceHandler(IHistoryClient history)
    : IProcessStepHandler<CaptureServiceStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureServiceStep step, ProcessStepContext context, CancellationToken ct)
    {
        // ... domain work ...
        return ProcessStepHandlerResult.Success<CaptureProviderStep>();
    }
}
```

Rules:
- **`IProcessStep` is the step's identity**; `[ProcessStep]` only describes it. Every
  `IProcessStep` type needs `[ProcessStep]` (KAL2011), and `[ProcessStep]` is only
  valid on `IProcessStep` types (KAL2010).
- **The step name is the type name** (`Type.Name`, unmodified) — in the registry, on
  the wire, in persisted state, and in events. Renaming the type renames the step.
- `[ProcessStep]` declares non-empty `Version`, `DisplayName`, and `Description`
  (KAL2001). `DisplayName`/`Description` are what UIs and AI agents read — write
  them for that audience.
- **The next step is named by type**: `Success<TNext>()` (or
  `ProcessStepHandlerResult<T>.Success<TNext>(response)`); `Success()` lets the
  process rules decide. Cross-processor routing uses `HandOff(targetProcessorName)`.
- Relationships are typed: `[DependsOn<T>]`, `[AvailableAfter<T>]`,
  `[AvailableUntil<T>]`, constrained to `IProcessStep`.
- Every step needs exactly one handler (KAL2008); the handler must not swallow
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

`AddHttp()` always wires `ExceptionMiddleware` + `ObservabilityMiddleware` via
`KaleidoStartupFilter`; there is no opt-out.

### Middleware ordering

The startup filter registers `ExceptionMiddleware` **outside**
`ObservabilityMiddleware`, before the rest of the host pipeline. The outer
boundary turns exceptions from correlation setup and endpoints into Kaleido
HTTP error responses; the inner middleware initializes correlation and echoes
its headers. Reversing them would leave a failure during observability setup
outside Kaleido's exception handler. Both middleware types are internal; hosts
never register them manually.

## Transport-neutral response semantics

Design the meaning of response fields before choosing JSON, gRPC, or another
transport. Keep handler-authored `BusinessMessages` separate from framework
`FrameworkMessages`: consumers may present the former and treat the latter as
diagnostics. A transport option may include framework messages, but when it is
off the collection is empty, not absent by a JSON-only rule. Repeated fields
have the same empty-collection meaning in a gRPC consumer. Adapters decide
which messages to populate; serializer-specific omission does not define the
contract or belong in core runtime types.

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
