# Core Architecture

This document describes the current architecture of the Core subsystem in `src/Core`. It is intended for contributors working on Kaleido's foundational infrastructure rather than higher-level capability frameworks.

Core is the shared substrate for Kaleido. It does not define business-capability runtimes on its own. Instead, it provides the bootstrap, metadata primitives, eventing abstractions, correlation context, and minimal ASP.NET Core integration that other framework areas build on.

The code for Core is split into three main projects:

- [`Abstractions`](./Abstractions)
- [`Kaleido`](./Kaleido)
- [`AspNetCore`](./AspNetCore)

---

## 1. Project structure

### [`Abstractions`](./Abstractions)
Contains:
- shared metadata and type-mapping primitives
- validation constraint metadata mapping
- eventing abstractions and event markers
- correlation context contracts
- shared validation exception types

Examples:
- [`DataTypeMapper`](./Abstractions/DataTypeMapper.cs)
- [`ConstraintMapper`](./Abstractions/ConstraintMapper.cs)
- [`IEventPublisher`](./Abstractions/Eventing/IEventPublishier.cs)
- [`KaleidoCorrelationContext`](./Abstractions/Observability/KaleidoCorrelationContext.cs)
- [`ValidationException`](./Abstractions/Exceptions/ValidationException.cs)

### [`Kaleido`](./Kaleido)
Contains:
- the root framework bootstrap entry point
- the shared builder abstraction and implementation
- assembly registration tracking
- default event publisher registration
- correlation context accessor initialization support
- shared JSON/value-conversion helpers

Examples:
- [`KaleidoServiceCollectionExtensions`](./Kaleido/KaleidoServiceCollectionExtensions.cs)
- [`IKaleidoBuilder`](./Kaleido/IKaleidoBuilder.cs)
- [`KaleidoBuilder`](./Kaleido/KaleidoBuilder.cs)
- [`KaleidoCorrelationContextAccessor`](./Kaleido/Observability/KaleidoCorrelationContextAccessor.cs)
- [`ValueConverter`](./Kaleido/Json/ValueConverter.cs)

### [`AspNetCore`](./AspNetCore)
Contains:
- shared exception-handling middleware
- shared HTTP error response contract
- shared correlation header names and parsing logic

Examples:
- [`AspNetCoreServiceCollectionExtensions`](./AspNetCore/AspNetCoreServiceCollectionExtensions.cs)
- [`ExceptionMiddleware`](./AspNetCore/Middleware/ExceptionMiddleware.cs)
- [`ApiErrorContract`](./AspNetCore/Contracts/ApiErrorContract.cs)
- [`KaleidoAspNetCoreCorrelation`](./AspNetCore/Observability/KaleidoAspNetCoreCorrelation.cs)

---

## 2. Core concepts

### Kaleido bootstrap
`AddKaleido()` is the root DI/bootstrap entry point for the framework.

It:
- validates the input `IServiceCollection`
- registers the scoped correlation accessor
- exposes that accessor through both read and initialization interfaces
- installs a default no-op event publisher
- returns a builder for later assembly registration

See [`KaleidoServiceCollectionExtensions`](./Kaleido/KaleidoServiceCollectionExtensions.cs).

### Kaleido builder
`IKaleidoBuilder` is the minimal shared registration contract exposed by Core.

It carries:
- `Services`
- `Assemblies`

The concrete `KaleidoBuilder` stores assemblies in a deduplicated set keyed by assembly identity.

See:
- [`IKaleidoBuilder`](./Kaleido/IKaleidoBuilder.cs)
- [`KaleidoBuilder`](./Kaleido/KaleidoBuilder.cs)

### Assembly registration
`AddAssembly(...)` records an assembly on the builder so higher-level frameworks can later inspect those assemblies.

Important distinction:
- Core stores assemblies
- Core does not itself scan them for business capability registrations

See [`KaleidoServiceCollectionExtensions`](./Kaleido/KaleidoServiceCollectionExtensions.cs).

### Correlation context
`KaleidoCorrelationContext` is the shared ambient identity model for a request or workflow.

It currently supports:
- request identity
- process identity
- processor identity
- processor instance identity
- orchestrator identity
- orchestrator instance identity

It also exposes `IsEmpty` to describe an uninitialized context.

See [`KaleidoCorrelationContext`](./Abstractions/Observability/KaleidoCorrelationContext.cs).

### Event publishing
Core defines an infrastructure-agnostic event publication seam through `IEventPublisher` and `IKaleidoEvent`.

By default, `AddKaleido()` registers `NullEventPublisher`, which makes eventing optional until a real publisher is supplied.

See:
- [`IEventPublisher`](./Abstractions/Eventing/IEventPublishier.cs)
- [`KaleidoEventAttribute`](./Abstractions/Eventing/KaleidoEventAttribute.cs)
- [`NullEventPublisher`](./Abstractions/Eventing/NullEventPublisher.cs)

### Data type metadata
`DataTypeMapper` converts CLR types and property metadata into transport-friendly `DataTypeDescriptor` values.

This includes support for:
- scalar kinds such as string, integer, boolean, and number
- formats such as `uuid`, `date`, `time`, `date-time`, and `duration`
- nullability
- enum value metadata
- runtime value conversion

See [`DataTypeMapper`](./Abstractions/DataTypeMapper.cs).

### Validation constraint metadata
`ConstraintMapper` converts `ValidationAttribute` usage into metadata contracts that can be surfaced to consumers.

Supported patterns include:
- `Required`
- `StringLength`
- `Range`
- `RegularExpression`
- `EmailAddress`
- `Phone`
- `Url`

See [`ConstraintMapper`](./Abstractions/ConstraintMapper.cs).

### Shared validation exception
`ValidationException` and `ValidationError` provide a reusable error shape for contract-level validation failures.

See [`ValidationException`](./Abstractions/Exceptions/ValidationException.cs).

---

## 3. Bootstrap and request lifecycle

Core's lifecycle is intentionally small.

### 3.1 Service bootstrap
A consumer starts by calling `AddKaleido()`.

That establishes the shared baseline services and returns a builder.

### 3.2 Assembly registration
The consumer may call `AddAssembly(...)` one or more times to record assemblies on the builder.

Those assemblies become shared registration input for higher-level frameworks.

### 3.3 Request correlation initialization
During a request, transport code can create a `KaleidoCorrelationContext` and initialize the scoped accessor.

Within the current codebase, the ASP.NET Core correlation helper reads request headers and constructs a `KaleidoCorrelationContext` from them.

### 3.4 Runtime use of shared primitives
Once initialized, other framework layers can use Core services and abstractions to:
- read current correlation identity
- publish framework events
- describe request and response shapes using data type metadata
- describe validation rules using constraint metadata
- normalize transport failures through shared middleware

Core stops there. It does not itself implement business capability execution, registry publication, or endpoint mapping for specific feature areas.

---

## 4. Metadata model

Core provides the building blocks for metadata-driven APIs.

### Property type description
`DataTypeMapper` is the canonical type-description utility.

From a CLR property or type, it can produce a `DataTypeDescriptor` containing:
- type
- format
- nullable flag
- enum values when applicable
- item type when collection/item modeling is supported

### Validation rule description
`ConstraintMapper` is the canonical validation-description utility.

From a reflected property, it can project `ValidationAttribute`s into a sequence of `ConstraintContract` values.

### Value conversion
Core also includes shared runtime conversion helpers:
- `DataTypeMapper.TryConvertValue(...)`
- `ValueConverter`

These support scenarios where input values arrive in untyped or loosely typed forms but must be converted to known CLR targets.

See:
- [`DataTypeMapper`](./Abstractions/DataTypeMapper.cs)
- [`ValueConverter`](./Kaleido/Json/ValueConverter.cs)

---

## 5. ASP.NET Core integration model

Core's ASP.NET Core layer is intentionally thin.

### Exception handling
`UseKaleidoExceptionHandling()` adds `ExceptionMiddleware`.

That middleware currently catches:
- `ArgumentException`
- `InvalidOperationException`

For those exceptions, it:
- logs a warning
- returns HTTP 400
- writes `ApiErrorContract`

Important distinction:
- this is targeted exception normalization
- it is not a general-purpose global exception handler for all exception types

### Correlation headers
`KaleidoAspNetCoreHeaders` defines the shared HTTP header names used to populate `KaleidoCorrelationContext`.

`KaleidoAspNetCoreCorrelation.Create(HttpContext)` reads those headers and parses GUID-backed values, failing fast when a GUID header is malformed.

This makes correlation identity a shared transport concern at the Core layer rather than something each higher-level framework needs to redefine.

---

## 6. What Core does and does not own

Core owns:
- the root builder and bootstrap model
- shared assembly registration tracking
- shared metadata and validation projection utilities
- shared eventing contracts
- shared correlation context
- shared ASP.NET Core exception/correlation support

Core does not own:
- capability-specific runtime registries
- business query execution
- business action execution
- capability-specific endpoint publishing
- feature-specific metadata semantics beyond the shared primitives

This boundary is important. When a new framework concern is specific to one capability area, it usually should not live in Core unless it is truly reusable across multiple higher-level subsystems.

---

## 7. Contributor cautions

### Core changes ripple broadly
Changes in Core can affect many downstream behaviors because Core defines shared primitives.

Be especially careful when changing:
- `DataTypeMapper`
- `ConstraintMapper`
- `KaleidoCorrelationContext`
- `IEventPublisher`
- shared JSON/value conversion helpers

### Avoid capability leakage
Do not move business-capability semantics into Core unless they are genuinely cross-cutting and reusable.

### Keep AspNetCore thin
Core.AspNetCore should remain a place for shared transport concerns, not for feature-specific endpoint mapping.

### Filename/type mismatches exist
Some legacy naming mismatches remain and should be understood before broad cleanup work:
- [`IEventPublishier.cs`](./Abstractions/Eventing/IEventPublishier.cs) contains `IEventPublisher`
- [`AspNetCoreServiceCollectionExtensions`](./AspNetCore/AspNetCoreServiceCollectionExtensions.cs) currently exposes middleware pipeline registration rather than DI registration

---

## 8. Where to look

- Start with [`Kaleido/KaleidoServiceCollectionExtensions.cs`](./Kaleido/KaleidoServiceCollectionExtensions.cs) for bootstrap behavior
- Read [`Kaleido/IKaleidoBuilder.cs`](./Kaleido/IKaleidoBuilder.cs) and [`Kaleido/KaleidoBuilder.cs`](./Kaleido/KaleidoBuilder.cs) for builder behavior
- Read [`Abstractions/DataTypeMapper.cs`](./Abstractions/DataTypeMapper.cs) and [`Abstractions/ConstraintMapper.cs`](./Abstractions/ConstraintMapper.cs) for shared metadata behavior
- Read [`Abstractions/Observability/KaleidoCorrelationContext.cs`](./Abstractions/Observability/KaleidoCorrelationContext.cs) for shared correlation identity
- Read [`AspNetCore/Middleware/ExceptionMiddleware.cs`](./AspNetCore/Middleware/ExceptionMiddleware.cs) and [`AspNetCore/Observability/KaleidoAspNetCoreCorrelation.cs`](./AspNetCore/Observability/KaleidoAspNetCoreCorrelation.cs) for HTTP-layer behavior
