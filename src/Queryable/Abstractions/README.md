# Queryable.Abstractions

This project contains the public contracts used by the Queryable subsystem.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)

## What lives here

This project contains:
- query attributes such as [`QueryContextAttribute`](./Attributes/QueryContextAttribute.cs) and [`QueryViewAttribute`](./Attributes/QueryViewAttribute.cs)
- metadata contracts such as [`QueryRegistration.cs`](./Metadata/QueryRegistration.cs)
- request/result types such as [`QueryRequest`](./Query/QueryRequest.cs)
- source/view interfaces such as:
  - [`IQueryContextSource`](./Query/IQueryContextSource.cs) — sync local context source
  - [`IQueryContextSourceAsync`](./Query/IQueryContextSourceAsync.cs) — async local context source (for async setup before composing `IQueryable`)
  - [`IQueryViewSource`](./Query/IQueryViewSource.cs) — sync local view
  - [`IQueryViewSourceAsync`](./Query/IQueryViewSourceAsync.cs) — async local view (for async setup before composing `IQueryable<TView>`)
  - [`IDelegateQueryViewSource`](./Query/IDelegateQueryViewSource.cs) — delegated/orchestrated view returning pre-materialized results
- the public execution entry point [`IQueryableService`](./IQueryableService.cs)
- shared Queryable options such as [`QueryableRouteOptions`](./QueryableRouteOptions.cs)

## What a consumer uses from this project

If you are building on top of Queryable, this is the project that defines the types you implement and reference directly.

You typically use this project to:
- mark a context with [`QueryContextAttribute`](./Attributes/QueryContextAttribute.cs)
- mark a view with [`QueryViewAttribute`](./Attributes/QueryViewAttribute.cs)
- annotate fields with query semantics such as filter/search/sort attributes
- implement [`IQueryContextSource`](./Query/IQueryContextSource.cs) for local/direct queryable data (sync)
- implement [`IQueryContextSourceAsync`](./Query/IQueryContextSourceAsync.cs) for local/direct queryable data when building the query requires `await`
- implement [`IQueryViewSource`](./Query/IQueryViewSource.cs) for local projected views (sync)
- implement [`IQueryViewSourceAsync`](./Query/IQueryViewSourceAsync.cs) for local projected views when composing the view requires `await`
- implement [`IDelegateQueryViewSource`](./Query/IDelegateQueryViewSource.cs) for delegated/orchestrated views that call another service and return pre-materialized results
- issue typed requests through [`IQueryableService`](./IQueryableService.cs)

## Common consumer workflow

Most Queryable feature code uses the abstractions in this order:

1. define a context type with `[QueryContext]`
2. define a source with `IQueryContextSource<TContext>` (or `IQueryContextSourceAsync<TContext>` if async setup is needed) if the data is locally queryable
3. define one or more views with `[QueryView]` and `IQueryViewSource<...>` (or `IQueryViewSourceAsync<...>`)
4. optionally define typed parameter objects for those views
5. let the runtime discover everything through `AddQueryable()` assembly scanning

For a full consumer walkthrough, see [`../README.md`](../README.md).

## What this project is for

Change this project when you are changing:
- public metadata contracts
- request or result shapes
- developer-facing attributes
- extension interfaces implemented by consumers

## What this project does not do

This project does **not** contain:
- assembly scanning
- runtime registration
- query execution
- HTTP endpoint publishing
- HTTP request/response contract publication

Those live in:
- [`../Queryable/README.md`](../Queryable/README.md)
- [`../AspNetCore.Abstractions/README.md`](../AspNetCore.Abstractions/README.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)

## When to be careful

Changes here usually ripple into:
- runtime registration and validation
- ASP.NET Core transport contracts
- tests
- samples

If you change a public contract, verify affected runtime and transport behavior.