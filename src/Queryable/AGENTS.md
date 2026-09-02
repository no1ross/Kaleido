# Queryable contributor guide

This directory contains the Queryable framework:
- [`Abstractions/`](./Abstractions) for public contracts, metadata, attributes, and source/view interfaces
- [`Queryable/`](./Queryable) for runtime registration, validation, execution, and observability
- [`AspNetCore/`](./AspNetCore) for HTTP contracts, endpoint publishing, and request normalization

Read [`ARCHITECTURE.md`](./ARCHITECTURE.md) first for the full subsystem model. Use this file as the short operating guide when making changes.

## Execution lanes
Queryable currently has three execution lanes:

1. **Direct context**
   - query the context type itself
   - only valid when `QueryContextKind == Direct`

2. **Local view**
   - named view over a local `IQueryable<TContext>`
   - implemented with [`IQueryViewSource`](./Abstractions/Query/IQueryViewSource.cs)
   - framework applies search/filter/sort/page and materializes results

3. **Delegated view**
   - named async/orchestrated view
   - implemented with [`IDelegateQueryViewSource`](./Abstractions/Query/IDelegateQueryViewSource.cs)
   - delegated implementation returns a fully materialized `QueryResult<TView>`

Current runtime dispatch order is:
1. delegated view registry
2. local view registry
3. direct context registry fallback

Do not change that order casually. It is part of the current framework semantics.

## What to use when
Use [`IQueryContextSource`](./Abstractions/Query/IQueryContextSource.cs) when:
- the dataset is locally queryable
- the framework should build the base `IQueryable<TContext>`

Use [`IQueryViewSource`](./Abstractions/Query/IQueryViewSource.cs) when:
- the view is a local projection/shaping step over `IQueryable<TContext>`
- the framework should own filtering, sorting, paging, and materialization
- no async orchestration is required before results exist

Use [`IDelegateQueryViewSource`](./Abstractions/Query/IDelegateQueryViewSource.cs) when:
- the view must call another service
- the backend must derive hidden/internal context before searching
- the UI should not know downstream/internal query details
- the query must perform async orchestration before returning results

## Registry rules
- Normal contexts are registered in [`IQueryContextRegistry`](./Abstractions/Query/IQueryContextRegistry.cs)
- Local views are registered in [`IQueryViewRegistry`](./Abstractions/Query/IQueryViewRegistry.cs)
- Delegated views are registered in [`IDelegatedQueryViewRegistry`](./Abstractions/Query/IDelegatedQueryViewRegistry.cs)

Delegated contexts still carry context metadata for discovery.
They do **not** expose a direct context query URL.

Do not collapse delegated views back into the normal local context/source model unless there is a strong, verified reason.

## Metadata and discovery rules
- Queryable discovery is context-centric, even for delegated execution
- Context field metadata drives validation, filtering, search, and sort behavior
- View metadata drives visibility, parameters, and pageable settings
- Only public views are published in registry output
- Direct `QueryUrl` is only published for direct contexts

When changing metadata contracts or registry behavior, always verify both:
- runtime execution behavior
- ASP.NET Core discovery/registry output

## What not to change casually
- `QueryContextKind` semantics
- `QueryableService` dispatch order
- the split between local views and delegated views
- context-centric discovery publishing
- pageable/default sort validation behavior
- public contracts in [`Abstractions/`](./Abstractions) without checking runtime and transport impact

Changes in [`Abstractions/`](./Abstractions) usually ripple into:
- runtime registration and validation
- endpoint publishing
- tests
- samples

Prefer additive changes over broad refactors when possible.

## Async guidance
Keep local query views synchronous.
[`IQueryViewSource`](./Abstractions/Query/IQueryViewSource.cs) is for `IQueryable` composition, not async orchestration.

The async orchestration boundary is [`IDelegateQueryViewSource`](./Abstractions/Query/IDelegateQueryViewSource.cs).

Also note: the default local materializer in [`QueryContextExecutor`](./Queryable/Runtime/QueryContextExecutor.cs) is async-shaped but currently sync-backed.
Do not document local execution as provider-native async unless that implementation changes.

## Verification
Run the smallest relevant verification set for the area you changed.

Core framework:
- `dotnet build src/Queryable/Queryable/Kaleido.Queryable.csproj`
- `dotnet test tests/Queryable/UnitTests/Kaleido.Queryable.UnitTests.csproj`

ASP.NET Core transport/discovery:
- `dotnet test tests/Queryable/AspNetCore.UnitTests/Kaleido.Queryable.AspNetCore.UnitTests.csproj`
- `dotnet test tests/Queryable/AspNetCore.FunctionalTests/Kaleido.Queryable.AspNetCore.FunctionalTests.csproj`

Abstractions-only contract changes:
- `dotnet test tests/Queryable/Abstractions.UnitTests/Kaleido.Queryable.Abstractions.UnitTests.csproj`

If the change touches samples or consumers of Queryable, also build or test the affected sample/project.

## Which tests to update
Update [`tests/Queryable/UnitTests`](../../tests/Queryable/UnitTests) when changing:
- `QueryableService`
- service registration
- validators
- registries
- execution engines
- compiled query behavior

Update [`tests/Queryable/AspNetCore.UnitTests`](../../tests/Queryable/AspNetCore.UnitTests) when changing:
- endpoint naming
- route publishing
- registry response shaping
- metadata endpoint behavior

Update [`tests/Queryable/AspNetCore.FunctionalTests`](../../tests/Queryable/AspNetCore.FunctionalTests) when changing:
- request/response transport behavior
- actual mapped endpoint execution
- end-to-end metadata or query behavior through ASP.NET Core

Update [`tests/Queryable/Abstractions.UnitTests`](../../tests/Queryable/Abstractions.UnitTests) when changing:
- public contracts
- request/result types
- metadata records
- attribute behavior

Testing conventions from the repo test guide:
- scope unit tests to a single class
- mock injected dependencies with `Moq`
- verify behavior at the seam/contract boundary
- keep workflow/transport breadth in functional tests, not unit tests

## Common pitfalls
- forgetting that delegated contexts still have context metadata
- assuming delegated execution should expose direct context query endpoints
- treating local query views as the async boundary
- changing metadata behavior without checking registry output
- changing pageable behavior without checking default sort requirements
- changing runtime dispatch without updating both unit tests and ASP.NET Core endpoint tests
- documenting behavior the code does not actually implement

## Rule of thumb
- If the data is locally queryable and the result is a projection, use a local context + local view
- If the context itself is the result shape, use a direct context
- If the backend must orchestrate async work or hide internal service logic from the caller, use a delegated view
