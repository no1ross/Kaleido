# Kaleido

This project bootstraps Kaleido and owns shared service identity, metadata and correlation. [`Kaleido.Eventing`](../../Events/Kaleido.Eventing/README.md) owns event writing and the outbox; [`Kaleido.Process`](../../Capabilities/Process/Kaleido.Process/README.md) and [`Kaleido.Queryable`](../../Capabilities/Queryable/Kaleido.Queryable/README.md) own their runtimes. The foundation does not depend on any of them or on a transport.

> How this project fits into a whole service, with tested examples: the [consumer guide](../../../docs/guide/README.md). This README is the reference for the project.

For the full repository model, see:
- [Foundation architecture](../../../docs/architecture/core.md) and [dependency boundaries](../../../docs/architecture/dependencies.md)
- [`../AGENTS.md`](../AGENTS.md)

---

## What lives here

### Bootstrap
- `KaleidoServiceCollectionExtensions` — `AddKaleido()` entry point; assemblies come from `KaleidoServiceOptions.Assemblies` and no provider is selected here
- `IKaleidoBuilder` / `KaleidoBuilder` — shared builder abstraction carrying `Services`, `Assemblies`, `Configuration` and `ServiceOptions`
- `IKaleidoCorrelationContextAccessor` — scoped correlation accessor registered by bootstrap (a pre-existing registration wins)
- no event writer, outbox store or publisher: hosts currently compose capability/Eventing registrations explicitly. Planned `Kaleido.Hosting` (#597) will centralize that composition and required-provider checks; relay hosts separately add `AddOutboxRelay<TPublisher>()` and the typed dispatchers for stored event families (ADR 0036).

### Shared abstractions
- `TypeDescriber` (internal concrete singleton) — converts CLR types and properties into `DataTypeDescriptor` values; HTTP maps those into wire `DataTypeMetadata`
- `ConstraintMapper` (internal concrete singleton) — converts `ValidationAttribute` usage into metadata constraint contracts
- `KaleidoCorrelationContext` — shared ambient identity model for requests/workflows
- Event publication, redaction, relay and outbox types belong to [`Kaleido.Eventing`](../../Events/Kaleido.Eventing/README.md); the foundation supplies only the shared `DataClassificationAttribute` inspection used by event and metadata owners (ADR 0013, ADR 0025).
- `Kaleido.Exceptions` — shared `KaleidoValidationException` (400), `KaleidoAuthorizationException` (401/403), `KaleidoConfigurationException` and `KaleidoFrameworkException` (500). The Process-owned `KaleidoConcurrencyException` reports process-state conflicts (409). HTTP error bodies belong to `Kaleido.Platform` during extraction (see [`docs/ERROR_CODES.md`](../../../docs/ERROR_CODES.md)).

### Queryable runtime
- [`Kaleido.Queryable`](../../Capabilities/Queryable/Kaleido.Queryable/README.md) owns source/view declarations, query planning and execution, and explicit `AddQueryable()` registration.

### Process runtime
- [`Kaleido.Process`](../../Capabilities/Process/Kaleido.Process/README.md) owns step/handler declarations, state, execution, Process telemetry and the context/idempotency/transaction provider seams. `AddProcess()` is explicit; the outbox is owned by [`Kaleido.Eventing`](../../Events/Kaleido.Eventing/README.md).

---

## What this project is for

Reference this project when you need to:
- bootstrap Kaleido with `AddKaleido()`; currently compose the needed owner registrations explicitly while `AddHosting()` is being implemented in #597
- register assemblies via `KaleidoServiceOptions.Assemblies` in the options passed to `AddKaleido()`
- work with shared metadata primitives (`TypeDescriber`, `ConstraintMapper`)
- find step/handler declarations in [`Kaleido.Process`](../../Capabilities/Process/Kaleido.Process/README.md) and query source/view declarations in [`Kaleido.Queryable`](../../Capabilities/Queryable/Kaleido.Queryable/README.md)
- work with correlation context; use [`Kaleido.Eventing`](../../Events/Kaleido.Eventing/README.md) for event publishing

## What this project is NOT for

This project does not contain:
- HTTP endpoint publication, DI registration, middleware, or transport services (see [`Kaleido.Http.Server`](../../Http/Http.Server/README.md))
- HTTP contract types (see [`Kaleido.Platform`](../../Platform/README.md))
- Remote HTTP client consumption (see [`Kaleido.Http.Client`](../../Http/Http.Client/README.md))
- SQLite state persistence (see [`Kaleido.Provider.SQLite`](../../Providers/Kaleido.Provider.SQLite/README.md))
- event writing, redaction, outbox stores or relay behavior (see [`Kaleido.Eventing`](../../Events/Kaleido.Eventing/README.md))

---

## Bootstrap model

A service starts by calling `AddKaleido()`:

```csharp
builder.Services.AddKaleido(builder.Configuration, new()
{
    ServiceName = "myservice", // [a-z0-9][a-z0-9_-]*: service and MCP tool identity (KAL2005)
    ServiceVersion = "1",     // required when mapping HTTP; each version gets its own gateway route
    Assemblies = [
        typeof(Program).Assembly,
        typeof(MyDbContext).Assembly
    ],
});
```

Other `KaleidoServiceOptions` (code values overlaid by the `Kaleido` configuration section):
- `DisplayName` / `Description` — the service's human-readable name and description, published in the registry
- `InstanceId` — identifies this running instance (new at each start by default) in its own trace tags, events and logs; never sent or read in correlation headers
- `TypeFilter` — limits which step and query source/view types are registered when an assembly is shared

The [commented configuration inventory](../../../docs/guide/examples/kaleido-settings.jsonc) shows every Core JSON setting. `Assemblies` (required) and `TypeFilter` (optional) are code-only. Process owns its entry flag, Server owns authorization mode/default ([authorization architecture](../../../docs/architecture/authorization.md)), and [Eventing](../../Events/Kaleido.Eventing/README.md) owns its `Kaleido:Events` section.

`AddKaleido()`:
- validates the `IServiceCollection`
- registers the scoped correlation accessor (using `TryAddScoped` so a pre-existing registration wins)
- registers no store implementation or store-choice check. `AddInMemoryProviders()` is an explicit development/test choice; durable providers register their own stores. Planned Hosting composition (#597) will check required provider registrations at startup.
- registers no capability runtime, event writer, publisher or transport; those owners currently register on the returned builder
- returns an `IKaleidoBuilder`

`AddKaleido()` is called once per service collection: a host runs one Kaleido service. A second call throws `KaleidoConfigurationException` (`duplicate_registration`) instead of silently replacing the first one's options; the same holds for `AddHttp()` (#301).

`KaleidoServiceOptions.Assemblies` records the assemblies that become the shared scanning input for the Queryable and Process runtimes. A non-empty list is required: `AddKaleido()` throws `KaleidoConfigurationException` (`missing_assembly`) before changing DI when it is omitted or empty. There is no calling-assembly or entry-assembly fallback. Even a client-only host must choose a scan set deliberately; it can use `typeof(KaleidoServiceOptions).Assembly` when it does not publish application capabilities.

---

## Queryable capability

Queryable registration, query execution and source/view semantics belong to [`Kaleido.Queryable`](../../Capabilities/Queryable/Kaleido.Queryable/README.md). Call `AddQueryable()` explicitly after `AddKaleido()`; the foundation itself does not register the Queryable runtime.

---

## Process execution model

Step graph, execution, recovery and process-state contracts belong to [`Kaleido.Process`](../../Capabilities/Process/Kaleido.Process/README.md).

---

## Where to look

- `KaleidoServiceCollectionExtensions.cs` — bootstrap entry point
- `KaleidoBuilder.cs` — builder contract (`IKaleidoBuilder`) and implementation
- `KaleidoServiceOptions.cs` — service options and their validation
- `TypeDescriber.cs` / `ConstraintMapper.cs` — shared metadata utilities
- `AssemblyTypeExtensions.cs` / `TypeFilterExtensions.cs` / `TypeExtensions.cs` — shared assembly scan, explicit type filter and generic-interface reflection
- [`Kaleido.Queryable/QueryableService.cs`](../../Capabilities/Queryable/Kaleido.Queryable/QueryableService.cs) — Queryable dispatch
- [`Kaleido.Process/Processor/Execution/ProcessorExecutor.cs`](../../Capabilities/Process/Kaleido.Process/Processor/Execution/ProcessorExecutor.cs) — Process execution loop
