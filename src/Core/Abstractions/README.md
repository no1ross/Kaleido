# Core.Abstractions

This project contains the shared contracts and metadata primitives used across Kaleido's foundational layer.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)

## What lives here

This project contains:
- shared CLR type-to-metadata mapping via [`DataTypeMapper`](./DataTypeMapper.cs)
- validation attribute-to-metadata mapping via [`ConstraintMapper`](./ConstraintMapper.cs)
- eventing abstractions such as [`IEventPublisher`](./Eventing/IEventPublishier.cs) and [`IKaleidoEvent`](./Eventing/IEventPublishier.cs)
- event type markers such as [`KaleidoEventAttribute`](./Eventing/KaleidoEventAttribute.cs)
- shared correlation contracts such as [`KaleidoCorrelationContext`](./Observability/KaleidoCorrelationContext.cs)
- shared validation exception types such as [`ValidationException`](./Exceptions/ValidationException.cs)

## What this project is for

Reference this project when you need to:
- describe CLR types as transport-friendly metadata
- expose validation rules as discovery metadata
- represent request or workflow correlation identity
- publish or consume generic Kaleido events
- use a shared validation exception shape

## Key public concepts

### Data type metadata
[`DataTypeMapper`](./DataTypeMapper.cs) projects CLR types into `DataTypeDescriptor` values that include type, format, nullability, enum metadata, and conversion support.

### Constraint metadata
[`ConstraintMapper`](./ConstraintMapper.cs) projects `ValidationAttribute` usage into `ConstraintContract` values that can be surfaced to consumers.

### Correlation context
[`KaleidoCorrelationContext`](./Observability/KaleidoCorrelationContext.cs) represents shared ambient request and workflow identity.

### Eventing
[`IEventPublisher`](./Eventing/IEventPublishier.cs) and [`IKaleidoEvent`](./Eventing/IEventPublishier.cs) define the generic event publication seam for the framework.

### Validation errors
[`ValidationException`](./Exceptions/ValidationException.cs) and `ValidationError` provide a reusable contract-level validation error shape.

## What this project does not do

This project does **not** contain:
- service bootstrap logic
- assembly scanning
- runtime capability registration
- HTTP endpoint mapping
- business-capability execution behavior

Those concerns live outside this project.

## Where to look

- [`DataTypeMapper`](./DataTypeMapper.cs)
- [`ConstraintMapper`](./ConstraintMapper.cs)
- [`KaleidoCorrelationContext`](./Observability/KaleidoCorrelationContext.cs)
- [`IEventPublisher`](./Eventing/IEventPublishier.cs)
- [`ValidationException`](./Exceptions/ValidationException.cs)
