# Cross-project implementation boundaries

These are cross-project implementation constraints and their rationale. [Contributor conventions](../CONVENTIONS.md), [analyzer rules](../ANALYZERS.md) and [testing instructions](../../tests/AGENTS.md) remain the authoritative checklists; this page explains why the boundaries exist.

## Exceptions

Framework code throws `Kaleido*Exception` types (KAL0002): `KaleidoValidationException` for validation (400 surface), `KaleidoConfigurationException` for startup and `KaleidoFrameworkException` for integrity failures. Argument guards (`ArgumentNullException`) are fine. Exception types are classes, not records (C# CS8865 forbids records deriving from `Exception`). Stable public codes and their HTTP mapping are tracked in [error codes](../ERROR_CODES.md); domain errors belong to their behavioral owners as projects migrate.

## Async and dependency injection

Kaleido code awaits without `ConfigureAwait(false)`; CA2007 remains silent for the currently supported ASP.NET Core and .NET generic hosts because they have no `SynchronizationContext`. Consumers should await APIs all the way up, never block with `.Result`/`.Wait()`/`.GetAwaiter().GetResult()`, especially on UI synchronization contexts. If such a host is supported later, revisit CA2007 for host-neutral projects and use `ConfigureAwait(false)` where needed.

Use constructor injection, service abstractions and readonly fields (KAL0004–KAL0013). The host's Add* registrations form the composition root; resolve `IServiceProvider` there or in `HttpContext.RequestServices`, not inside runtime behavior. `IKaleidoBuilder` exposes only shared composition inputs, not arbitrary service location.

## Static mechanics versus behavior

KAL0001 and MP-027 allow stateless helpers for values, routes, names, URLs and formats (`ProcessContractUrls.ExecuteStep`, `QueryableEndpointNames`), but meaningful runtime behavior belongs to injectable concrete owners with explicit dependencies. Introduce an interface only at a real abstraction boundary. A transformation derived from one type should be an extension on that type (`query.ToApiBody()`, `type.GetQueryViewInterface()`); registry compilation, metadata composition and multi-source assembly remain helper/builder instances. Do not put mutable state or runtime/domain/service-type behavior in static methods (#459).

## Tests enforce the boundaries

A unit fixture is named `{Sut}Tests`, mirrors the SUT's source path (KAL1001–KAL1004) and uses `SutFixture<TSut>` with construction only in `CreateSut()` (KAL1006–KAL1008). Mock injected collaborators instead of constructing a second Kaleido service (KAL1011). Tests that build a container enable `ValidateScopes` and `ValidateOnBuild` (KAL1005). See [tests/AGENTS.md](../../tests/AGENTS.md) for the full, current test rules; do not duplicate them here. The deleted assembly architecture tests must be replaced by source analyzers under #591 before #585 finishes.
