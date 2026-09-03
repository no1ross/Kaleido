# Kaleido

This project contains the root bootstrap layer for Kaleido.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)
- [`../Abstractions/README.md`](../Abstractions/README.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)

## What lives here

This project contains:
- the root `AddKaleido()` registration entry point
- the shared `IKaleidoBuilder` abstraction
- the concrete `KaleidoBuilder` implementation
- shared assembly registration tracking
- default event publisher bootstrap behavior
- correlation context accessor initialization support
- shared JSON and value-conversion helpers

## Main entry point

### `AddKaleido()`
`AddKaleido()` is the root bootstrap path for the framework.

It:
- validates the input `IServiceCollection`
- registers the scoped correlation accessor
- exposes that accessor through both accessor and initializer interfaces
- installs `NullEventPublisher` as the default event publisher
- returns an `IKaleidoBuilder`

```csharp
builder.Services.AddKaleido();
```

## Assembly registration model

After calling `AddKaleido()`, a consumer can register assemblies with `AddAssembly(...)`.

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly);
```

The builder carries:
- `Services`
- `Assemblies`

Important distinction:
- this project records assemblies
- it does not itself scan them for higher-level capability registrations

## Default services

By default, this project registers:
- `KaleidoCorrelationContextAccessor` as a scoped service
- `IKaleidoCorrelationContextAccessor`
- `IKaleidoCorrelationContextInitializer`
- `NullEventPublisher` as the default `IEventPublisher`

This gives higher-level frameworks a consistent shared baseline without forcing a concrete eventing implementation up front.

## Builder behavior

`IKaleidoBuilder` is intentionally minimal.
It exists to carry the service collection and the registered assembly set.

`KaleidoBuilder` deduplicates assemblies by identity.

## Shared helpers

This project also contains shared helpers used by higher-level layers:
- enum-related helpers and converters
- `ValueConverter`
- JSON conversion infrastructure

Review these carefully before changing them because they can affect shared transport and conversion behavior.

## What this project does not do

This project does **not** contain:
- business-capability execution
- capability-specific registries
- capability-specific endpoint publication
- feature-specific discovery models

Its role is bootstrap and shared framework plumbing.

## Where to look

- [`KaleidoServiceCollectionExtensions`](./KaleidoServiceCollectionExtensions.cs)
- [`IKaleidoBuilder`](./IKaleidoBuilder.cs)
- [`KaleidoBuilder`](./KaleidoBuilder.cs)
- [`KaleidoCorrelationContextAccessor`](./Observability/KaleidoCorrelationContextAccessor.cs)
- [`ValueConverter`](./Json/ValueConverter.cs)
