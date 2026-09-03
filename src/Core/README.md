# Core

Core is Kaleido's foundational shared infrastructure.

It does not define business-capability runtimes on its own. Instead, it provides the bootstrap, shared contracts, metadata primitives, eventing abstractions, correlation context, and minimal ASP.NET Core support that higher-level framework areas build on.

The code in `src/Core` is split into three main projects:

- [`Abstractions`](./Abstractions) — shared metadata mapping, validation metadata mapping, eventing abstractions, correlation contracts, and shared validation errors
- [`Kaleido`](./Kaleido) — the root `AddKaleido()` bootstrap path, builder state, assembly registration tracking, and shared JSON/value-conversion helpers
- [`AspNetCore`](./AspNetCore) — shared exception handling and HTTP correlation-header support

For contributor-oriented guidance, see:
- [`ARCHITECTURE.md`](./ARCHITECTURE.md)
- [`AGENTS.md`](./AGENTS.md)

## What Core provides

Core provides:
- the root DI/bootstrap entry point
- a shared builder abstraction with assembly tracking
- shared data type and validation constraint metadata projection
- shared correlation identity contracts
- shared event publication abstractions
- shared, minimal ASP.NET Core infrastructure

Core does **not** provide:
- business query execution
- business action execution
- capability-specific registries
- capability-specific endpoint mapping

## Bootstrap model

A service starts by calling `AddKaleido()`.

That call:
- validates the `IServiceCollection`
- registers the scoped correlation accessor
- exposes that accessor through read and initialization interfaces
- registers the default no-op event publisher
- returns an `IKaleidoBuilder`

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly);
```

The builder carries:
- `Services`
- `Assemblies`

Assemblies added to the builder become shared registration input for higher-level frameworks.

## Shared building blocks

### Metadata primitives
Use Core's metadata helpers when you need to reflect CLR contracts into discoverable metadata:
- [`DataTypeMapper`](./Abstractions/DataTypeMapper.cs)
- [`ConstraintMapper`](./Abstractions/ConstraintMapper.cs)

These utilities support:
- scalar type/format description
- enum metadata
- nullability
- validation rule projection
- runtime value conversion helpers

### Correlation context
Use [`KaleidoCorrelationContext`](./Abstractions/Observability/KaleidoCorrelationContext.cs) and the scoped accessor to carry request/workflow identity through a request lifetime.

### Eventing
Use [`IEventPublisher`](./Abstractions/Eventing/IEventPublishier.cs) when you need an infrastructure-agnostic event publication seam. By default, Core installs [`NullEventPublisher`](./Abstractions/Eventing/NullEventPublisher.cs), so eventing remains optional until a real publisher is supplied.

### ASP.NET Core support
Use [`UseKaleidoExceptionHandling()`](./AspNetCore/AspNetCoreServiceCollectionExtensions.cs) for the shared exception middleware and [`KaleidoAspNetCoreCorrelation`](./AspNetCore/Observability/KaleidoAspNetCoreCorrelation.cs) for shared correlation-header parsing.

## Request lifecycle at the Core layer

Core's request lifecycle is intentionally small:
1. bootstrap the service with `AddKaleido()`
2. record assemblies with `AddAssembly(...)`
3. initialize `KaleidoCorrelationContext` for a request
4. let higher-level frameworks consume Core services for metadata, eventing, correlation, and shared transport behavior

Core stops there. It does not itself execute business capabilities or publish feature-specific endpoints.

## Project map

- Start with [`Kaleido/README.md`](./Kaleido/README.md) for bootstrap behavior
- Read [`Abstractions/README.md`](./Abstractions/README.md) for shared contracts and metadata primitives
- Read [`AspNetCore/README.md`](./AspNetCore/README.md) for HTTP-layer behavior
- Read [`ARCHITECTURE.md`](./ARCHITECTURE.md) for the full subsystem model
- Read [`AGENTS.md`](./AGENTS.md) before making Core changes
