# Core contributor guide

This directory contains Kaleido's foundational shared infrastructure:
- [`Abstractions/`](./Abstractions) for shared contracts, metadata primitives, validation metadata, eventing, and correlation context
- [`Kaleido/`](./Kaleido) for root bootstrap, builder state, assembly registration, and shared JSON/value-conversion helpers
- [`AspNetCore/`](./AspNetCore) for shared exception handling and correlation-header support

Read [`ARCHITECTURE.md`](./ARCHITECTURE.md) first for the full subsystem model. Use this file as the short operating guide when making changes.

## Scope and boundaries
Core is the shared substrate for Kaleido.

Core owns:
- the root `AddKaleido()` bootstrap path
- the shared `IKaleidoBuilder` abstraction and assembly list
- shared metadata/type mapping
- shared validation metadata mapping
- shared eventing abstractions
- shared correlation context
- thin ASP.NET Core infrastructure shared across higher-level frameworks

Core does **not** own:
- capability-specific runtime registries
- business query execution
- business action execution
- capability-specific endpoint mapping
- feature-specific discovery models beyond the shared metadata primitives

When something is only meaningful to one capability area, it usually should not live in Core.

## Bootstrap rules
`AddKaleido()` is the root registration entry point for the framework.

It should remain responsible for:
- shared DI baseline setup
- scoped correlation accessor registration
- default event publisher registration
- returning the builder used by higher-level frameworks

`AddAssembly(...)` should remain a lightweight recording step.

Core stores assemblies so other framework layers can inspect them later.
Core should not become the place where business-capability scanning or registration logic lives.

## Builder rules
`IKaleidoBuilder` is intentionally small.

It currently exposes:
- `Services`
- `Assemblies`

Be cautious about expanding this interface.
Additional members affect every higher-level framework built on top of Core.

The concrete `KaleidoBuilder` deduplicates assemblies by identity.
Preserve that behavior unless there is a strong verified reason to change it.

## Metadata rules
`DataTypeMapper` and `ConstraintMapper` are high-impact shared primitives.

Changes here ripple into any framework that:
- reflects CLR types into discovery metadata
- projects validation rules to clients or registries
- converts incoming transport values into CLR values

Be especially careful when changing:
- scalar type names
- format strings
- nullability behavior
- enum metadata behavior
- constraint naming
- supported validation attribute mappings
- conversion error behavior

Prefer additive changes over breaking changes when possible.

## Correlation rules
`KaleidoCorrelationContext` is a shared ambient identity contract.

Treat changes to its fields as broad-impact work.
Those changes can affect:
- transport/header handling
- observability
- event payloads
- higher-level runtime context propagation

Header names under `AspNetCore/Observability/KaleidoAspNetCoreCorrelation.cs` are also shared contracts.
Do not change them casually.

## Eventing rules
`IEventPublisher` is intentionally infrastructure-agnostic.

The default `NullEventPublisher` means eventing is optional by default.
Do not remove that default unless you intentionally want bootstrap to require real event infrastructure.

When adjusting event contracts:
- keep Core generic
- avoid embedding capability-specific semantics into shared event interfaces
- prefer additive evolution over breaking changes

## ASP.NET Core rules
Core.AspNetCore should stay thin.

Its job is to provide shared HTTP-layer concerns such as:
- exception normalization
- correlation header parsing

It should not absorb capability-specific endpoint publication or request/response choreography.

Also note that the current middleware behavior is intentionally limited:
- `ExceptionMiddleware` catches `ArgumentException`
- `ExceptionMiddleware` catches `InvalidOperationException`
- it returns HTTP 400 with `ApiErrorContract`

Do not document it as a universal exception pipeline unless the implementation actually changes.

## Shared conversion helpers
The `Kaleido/Json` helpers are foundational utilities for value conversion and serialization behavior.

Changes there can affect:
- transport normalization
- enum serialization
- metadata consumers that depend on stable conversion behavior

Review these carefully before making broad changes:
- [`KaleidoEnumConverter`](./Kaleido/Json/KaleidoEnumConverter.cs)
- [`KaleidoEnumConverterFactory`](./Kaleido/Json/KaleidoEnumConverterFactory.cs)
- [`ValueConverter`](./Kaleido/Json/ValueConverter.cs)

## What not to change casually
- the minimal shape of `IKaleidoBuilder`
- assembly deduplication behavior in `KaleidoBuilder`
- the default no-op event publisher bootstrap behavior
- shared correlation header names
- `DataTypeMapper` scalar/format conventions
- `ConstraintMapper` constraint naming and parameter conventions
- the intentionally thin scope of Core.AspNetCore

## Common pitfalls
- moving feature-specific semantics into Core
- treating `AddAssembly(...)` as if it should also scan and register features
- changing metadata primitives without considering downstream discovery/output implications
- changing correlation fields or header names without treating them as shared contracts
- assuming exception middleware normalizes every failure shape
- documenting behavior that the code does not actually implement
- assuming `IKaleidoCorrelationContextAccessor` is registered with `TryAdd` — it is **not**;
  `AddKaleido()` always appends a new scoped registration, so a pre-existing registration is silently
  shadowed. Use `services.Replace(...)` after `AddKaleido()` if you need to substitute the accessor
  (this matters most in test providers — see `tests/AGENTS.md`).

## Verification
Run the smallest relevant verification set for the area you changed.

Core bootstrap or abstractions:
- `dotnet build src/Core/Kaleido/Kaleido.csproj`
- `dotnet build src/Core/Abstractions/Kaleido.Abstractions.csproj`

Core ASP.NET Core infrastructure:
- `dotnet build src/Core/AspNetCore/Kaleido.AspNetCore.csproj`
- run affected ASP.NET Core tests if transport or middleware behavior changed

If a change touches shared abstractions like metadata mapping, eventing, correlation, or conversion helpers, also verify downstream frameworks that consume those primitives.

## Naming and legacy cautions
A few legacy mismatches are worth knowing before cleanup work:
- [`IEventPublishier.cs`](./Abstractions/Eventing/IEventPublishier.cs) contains `IEventPublisher`
- [`AspNetCoreServiceCollectionExtensions`](./AspNetCore/AspNetCoreServiceCollectionExtensions.cs) currently exposes middleware pipeline registration rather than service-collection registration
- [`KaleidoCorrelationContext`](./Abstractions/Observability/KaleidoCorrelationContext.cs) still includes `OrchestratorId` and `OrchestratorInstanceId`

Understand those mismatches before normalizing names so you do not accidentally broaden the scope of a small change.

## Rule of thumb
- If the concern is bootstrap, shared metadata description, eventing abstraction, correlation identity, or thin HTTP infrastructure, it may belong in Core.
- If the concern is capability-specific execution, discovery, or endpoint behavior, it probably belongs outside Core.
