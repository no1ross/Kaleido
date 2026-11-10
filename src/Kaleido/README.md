# Kaleido

This project currently bootstraps Kaleido and runs Process and Eventing while those capabilities are being extracted. Queryable already lives in [`Kaleido.Queryable`](../Capabilities/Queryable/Kaleido.Queryable/README.md). The foundation remains free of transport and ASP.NET Core dependencies.

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
- `Kaleido.Exceptions` — `KaleidoValidationException` (400), `KaleidoAuthorizationException` (401/403), `KaleidoConcurrencyException` (409), `KaleidoConfigurationException` and `KaleidoFrameworkException` (500); the HTTP transport maps them to the error body `KaleidoErrorResponse`, which lives in `Kaleido.Platform` (see [`docs/ERROR_CODES.md`](../../docs/ERROR_CODES.md), ADR 0017)

### Queryable runtime
- [`Kaleido.Queryable`](../Capabilities/Queryable/Kaleido.Queryable/README.md) owns source/view declarations, query planning and execution, and explicit `AddQueryable()` registration.

### Process runtime
- `ProcessorServiceCollectionExtensions` — `AddProcessor(...)` (internal, auto-invoked by `AddKaleido()`)
- `IProcessorRuntime` (internal) — runs submitted steps in-process (what the HTTP transport calls); `ProcessorExecutor` (internal) is the main step execution loop
- `StepCandidateBuilder` / `StepCandidateValidator` / `StepCandidateConsistencyChecker` / `StepCandidatePlanner` / `StepCandidateNextStepChecker` — planning pipeline
- `IProcessorStepRegistry` / `IProcessorRegistry` (internal, shared with `Kaleido.Http.Server`) — runtime registries
- `ProcessorObservability` (internal) — logs, traces and metrics of the runtime
- `IProcessStepHandler<TStep>` / `IProcessStepHandler<TStep, TResult>` — handler contracts
- `IInformationStep` / `InformationRequest` / `RequireInformation<TNext>` — information requests: runtime-decided questions answered by an information step (see [`docs/INFORMATION_REQUESTS.md`](../../docs/INFORMATION_REQUESTS.md))
- `IKaleidoContextStore` - the process-state store (state only, keyed by `ProcessKey`: processor, version, process id); `IKaleidoOutboxStore` - every event the service writes, tagged with the writer, until the relay (`AddOutboxRelay<TPublisher>()`, on one host per outbox database) publishes it; `IKaleidoTransactions` - optional, makes a process-state transition (state + its event) atomic on one database; `IKaleidoIdempotencyStore` - requests sent with an idempotency key (ADR 0020, ADR 0023, ADR 0036). Every record is scoped by the service and its version, so services may share one database. The in-memory defaults (internal) are for development; each logs a Warning that its data is lost on restart

---

## What this project is for

Reference this project when you need to:
- bootstrap Kaleido with `AddKaleido()` (which still auto-registers Process during migration; add `AddQueryable()` explicitly for query sources/views)
- register assemblies via `KaleidoServiceOptions.Assemblies` in the options passed to `AddKaleido()`
- work with shared metadata primitives (`TypeDescriber`, `ConstraintMapper`)
- implement a step handler; query sources and views belong to [`Kaleido.Queryable`](../Capabilities/Queryable/Kaleido.Queryable/README.md)
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
- registers the in-memory stores (`IKaleidoContextStore`, `IKaleidoIdempotencyStore`, `IKaleidoOutboxStore`) and no transactions; the builder replaces them (`AddContextStore`, `AddIdempotencyStore`, `AddOutboxStore`, `AddTransactions`; one of each per service). It registers no publisher and no relay: `AddOutboxRelay<TPublisher>()` adds the relay (a hosted background service that hands every outbox entry of its database to the publisher - see `KaleidoEventOptions`, `Kaleido:Events`) on the host that publishes
- refuses to start a host that leaves a store it uses at the in-memory default (`stores_not_chosen`, ADR 0028): the context and idempotency stores when the service has process steps, the outbox store when it publishes through Kaleido's outbox. `AddInMemoryStores()` chooses memory explicitly (development, tests)
- still invokes `AddProcessor()` during extraction; call `AddQueryable()` on the builder to register query sources and views
- returns an `IKaleidoBuilder`

`AddKaleido()` is called once per service collection: a host runs one Kaleido service. A second call throws `KaleidoConfigurationException` (`duplicate_registration`) instead of silently replacing the first one's options; the same holds for `AddHttp()` (#301).

`KaleidoServiceOptions.Assemblies` records the assemblies that become the shared scanning input for the Queryable and Process runtimes. A non-empty list is required: `AddKaleido()` throws `KaleidoConfigurationException` (`missing_assembly`) before changing DI when it is omitted or empty. There is no calling-assembly or entry-assembly fallback. Even a client-only host must choose a scan set deliberately; it can use `typeof(KaleidoServiceOptions).Assembly` when it does not publish application capabilities.

---

## Queryable capability

Queryable registration, query execution and source/view semantics belong to [`Kaleido.Queryable`](../Capabilities/Queryable/Kaleido.Queryable/README.md). Call `AddQueryable()` explicitly after `AddKaleido()`; the foundation itself does not register the Queryable runtime.

---

## Process execution model

Process is step-centric:
1. declare a step type that implements `IProcessStep` (its identity) and carries `[ProcessStep]` (its required metadata: `DisplayName`, `Description`) and, where it applies, what it does in the world: `Destructive`, `External`, `RequiresConfirmation` (ADR 0015)
2. implement exactly one `IProcessStepHandler<TStep>` (or `<TStep, TResult>`)
3. register the assemblies containing steps via `KaleidoServiceOptions.Assemblies`
4. let the runtime build a registry and manage state
5. submit one or more steps through a transport (the HTTP transport, `Kaleido.Http.Server`); the in-process runtime it calls is internal

A step's public name is its type name (`Type.Name`, unmodified) — in the registry, on the wire, in persisted state, and in events. Handlers name the next step by type (`ProcessStepHandlerResult.Success<TNext>()`, or `ProcessStepHandlerResult.Success(response).Next<TNext>()` with a typed response); names are only produced at the boundary.

The runtime:
- builds and validates submitted step candidates
- evaluates dependency and repeatability rules (`[DependsOn<T>]`, `[AvailableAfter<T>]`, `[AvailableUntil<T>]`, `[Repeatable]`)
- orders executable steps
- invokes handlers
- persists updated processor state
- returns step results plus next-step guidance

---

## Where to look

- `KaleidoServiceCollectionExtensions.cs` — bootstrap entry point
- `KaleidoBuilder.cs` — builder contract (`IKaleidoBuilder`) and implementation
- `KaleidoServiceOptions.cs` — service options and their validation
- `TypeDescriber.cs` / `ConstraintMapper.cs` — shared metadata utilities
- `Queryable/QueryableService.cs` — Queryable dispatch
- `Processor/Execution/ProcessorExecutor.cs` — Process execution loop
- `Processor/Planning/` — planning pipeline components
