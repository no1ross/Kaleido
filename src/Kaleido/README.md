# Kaleido

This project bootstraps Kaleido and temporarily hosts Eventing while it is extracted. [`Kaleido.Process`](../Capabilities/Process/Kaleido.Process/README.md) and [`Kaleido.Queryable`](../Capabilities/Queryable/Kaleido.Queryable/README.md) own their runtimes. The foundation remains free of transport and ASP.NET Core dependencies.

> How this project fits into a whole service, with tested examples: the [consumer guide](../../docs/guide/README.md). This README is the reference for the project.

For the full repository model, see:
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)

---

## What lives here

### Bootstrap
- `KaleidoServiceCollectionExtensions` — `AddKaleido()` entry point (assemblies supplied via `KaleidoServiceOptions.Assemblies`), `AddOutboxRelay<TPublisher>()`, `AddEventRedactor<T>()`
- `IKaleidoBuilder` / `KaleidoBuilder` — shared builder abstraction carrying `Services`, `Assemblies`, `Configuration` and `ServiceOptions`
- `IKaleidoCorrelationContextAccessor` — scoped correlation accessor registered by bootstrap (a pre-existing registration wins)
- no `IEventPublisher`: every service writes its events to its outbox store, and the host that publishes adds `AddOutboxRelay<TPublisher>()` (ADR 0036). A relay-only host also calls `AddProcessEventDispatch()` and `AddQueryableEventDispatch()` for the event families stored in its database; hosts with those capabilities register their dispatch automatically.

### Shared abstractions
- `ITypeDescriber` / `TypeDescriber` (internal) — converts CLR types and properties into `DataTypeDescriptor` values (internal; the HTTP transport maps them to the wire's `DataTypeMetadata`)
- `IConstraintMapper` / `ConstraintMapper` (internal) — converts `ValidationAttribute` usage into metadata constraint contracts
- `KaleidoCorrelationContext` — shared ambient identity model for requests/workflows
- `IEventPublisher` / `IKaleidoEvent` - infrastructure-agnostic event publication seam; events reach the publisher at least once through the outbox and the event relay (`KaleidoEventRelay`, `KaleidoEventOptions`, ADR 0012)
- `IKaleidoEventRedactor` / `AddEventRedactor<T>()` - reduce events before the outbox write; properties marked with a `DataClassificationAttribute` are redacted per the event policy `AddEventRedaction(r => ...)` sets (separate from the host's log redaction, `AddRedaction`), or erased without one (ADR 0013, ADR 0025)
- `Kaleido.Exceptions` — shared `KaleidoValidationException` (400), `KaleidoAuthorizationException` (401/403), `KaleidoConfigurationException` and `KaleidoFrameworkException` (500). The Process-owned `KaleidoConcurrencyException` reports process-state conflicts (409). HTTP error bodies belong to `Kaleido.Platform` during extraction (see [`docs/ERROR_CODES.md`](../../docs/ERROR_CODES.md)).

### Queryable runtime
- [`Kaleido.Queryable`](../Capabilities/Queryable/Kaleido.Queryable/README.md) owns source/view declarations, query planning and execution, and explicit `AddQueryable()` registration.

### Process runtime
- [`Kaleido.Process`](../Capabilities/Process/Kaleido.Process/README.md) owns step/handler declarations, state, execution, Process telemetry and the context/idempotency/transaction provider seams. `AddProcess()` is explicit; the outbox remains in Eventing pending its extraction.

---

## What this project is for

Reference this project when you need to:
- bootstrap Kaleido with `AddKaleido()`; add `AddProcess()` and/or `AddQueryable()` explicitly for hosted capabilities
- register assemblies via `KaleidoServiceOptions.Assemblies` in the options passed to `AddKaleido()`
- work with shared metadata primitives (`TypeDescriber`, `ConstraintMapper`)
- find step/handler declarations in [`Kaleido.Process`](../Capabilities/Process/Kaleido.Process/README.md) and query source/view declarations in [`Kaleido.Queryable`](../Capabilities/Queryable/Kaleido.Queryable/README.md)
- work with correlation context or event publishing

## What this project is NOT for

This project does not contain:
- HTTP endpoint publication, DI registration, middleware, or transport services (see [`Kaleido.Http.Server`](../Http/Http.Server/README.md))
- HTTP contract types (see [`Kaleido.Platform`](../Platform/README.md))
- Remote HTTP client consumption (see [`Kaleido.Http.Client`](../Http/Http.Client/README.md))
- SQLite state persistence (see [`Kaleido.Provider.SQLite`](../Providers/Provider.SQLite/README.md))

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

Other `KaleidoServiceOptions` (bound from the `Kaleido` configuration section, then the callback):
- `DisplayName` / `Description` — the service's human-readable name and description, published in the registry
- `IsEntryProcessor` — marks this service's processor as the central registry's entry point (only one may be published)
- `InstanceId` — identifies this running instance (new at each start by default) in its own trace tags, events and logs; never sent or read in correlation headers
- `TypeFilter` — limits which step and query source/view types are registered when an assembly is shared
- `AuthorizationMode` / `DefaultAuthorization` — capability authorization ([`docs/AUTHORIZATION.md`](../../docs/AUTHORIZATION.md))
- `Events` — the event relay (`Kaleido:Events`, `KaleidoEventOptions`)

`AddKaleido()`:
- validates the `IServiceCollection`
- registers the scoped correlation accessor (using `TryAddScoped` so a pre-existing registration wins)
- registers the in-memory outbox default (`IKaleidoOutboxStore`) and event writing; `AddProcess()` installs the context, idempotency and no-transaction defaults. Choose replacements through the owning builder extensions (`AddContextStore`, `AddIdempotencyStore`, `AddTransactions` in Process, `AddOutboxStore` here). It registers no publisher or relay: the publishing host adds `AddOutboxRelay<TPublisher>()` (see `KaleidoEventOptions`, `Kaleido:Events`).
- refuses to start a host that leaves a store it uses at the in-memory default (`stores_not_chosen`, ADR 0028): the context and idempotency stores when the service has process steps, the outbox store when it publishes through Kaleido's outbox. `AddInMemoryStores()` chooses memory explicitly (development, tests)
- registers neither runtime; call `AddProcess()` and/or `AddQueryable()` on the builder to register hosted capabilities
- returns an `IKaleidoBuilder`

`AddKaleido()` is called once per service collection: a host runs one Kaleido service. A second call throws `KaleidoConfigurationException` (`duplicate_registration`) instead of silently replacing the first one's options; the same holds for `AddHttp()` (#301).

`KaleidoServiceOptions.Assemblies` records the assemblies that become the shared scanning input for the Queryable and Process runtimes. A non-empty list is required: `AddKaleido()` throws `KaleidoConfigurationException` (`missing_assembly`) before changing DI when it is omitted or empty. There is no calling-assembly or entry-assembly fallback. Even a client-only host must choose a scan set deliberately; it can use `typeof(KaleidoServiceOptions).Assembly` when it does not publish application capabilities.

---

## Queryable capability

Queryable registration, query execution and source/view semantics belong to [`Kaleido.Queryable`](../Capabilities/Queryable/Kaleido.Queryable/README.md). Call `AddQueryable()` explicitly after `AddKaleido()`; the foundation itself does not register the Queryable runtime.

---

## Process execution model

Step graph, execution, recovery and process-state contracts belong to [`Kaleido.Process`](../Capabilities/Process/Kaleido.Process/README.md).

---

## Where to look

- `KaleidoServiceCollectionExtensions.cs` — bootstrap entry point
- `KaleidoBuilder.cs` — builder contract (`IKaleidoBuilder`) and implementation
- `KaleidoServiceOptions.cs` — service options and their validation
- `TypeDescriber.cs` / `ConstraintMapper.cs` — shared metadata utilities
- [`Kaleido.Queryable/QueryableService.cs`](../Capabilities/Queryable/Kaleido.Queryable/QueryableService.cs) — Queryable dispatch
- [`Kaleido.Process/Processor/Execution/ProcessorExecutor.cs`](../Capabilities/Process/Kaleido.Process/Processor/Execution/ProcessorExecutor.cs) — Process execution loop
