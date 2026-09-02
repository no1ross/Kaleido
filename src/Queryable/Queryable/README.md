# Queryable runtime

This project contains the runtime implementation of Queryable.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)

## What lives here

This project contains:
- `AddQueryable()` registration and assembly scanning
- context and view registries
- registration validators
- request validation and compilation
- query dispatch and execution
- local and delegated execution engines
- compiled query application
- materialization
- observability and query event publishing

Key files include:
- [`QueryableServiceCollectionExtensions`](./QueryableServiceCollectionExtensions.cs)
- [`QueryableService`](./QueryableService.cs)
- [`QueryContextEngine`](./Query/QueryContextEngine.cs)
- [`DelegatedQueryViewEngine`](./Query/DelegatedQueryViewEngine.cs)
- [`QueryContextRegistry`](./Records/QueryContextRegistry.cs)
- [`QueryViewRegistry`](./Records/QueryViewRegistry.cs)
- [`DelegatedQueryViewRegistry`](./Records/DelegatedQueryViewRegistry.cs)

## What this project is for

Work here when you are changing:
- registration behavior
- execution dispatch
- validators
- registries
- query compilation or query application
- delegated-view runtime behavior
- observability around query execution

## Execution lanes

This project owns the runtime behavior for:
- direct context queries
- local view queries
- delegated view queries

Current dispatch order is:
1. delegated view registry
2. local view registry
3. direct context fallback

## Verification

Typical verification for this project:

- `dotnet build src/Queryable/Queryable/Kaleido.Queryable.csproj`
- `dotnet test tests/Queryable/UnitTests/Kaleido.Queryable.UnitTests.csproj`

Also run ASP.NET Core tests if your runtime change affects discovery or endpoint behavior:
- `dotnet test tests/Queryable/AspNetCore.UnitTests/Kaleido.Queryable.AspNetCore.UnitTests.csproj`
- `dotnet test tests/Queryable/AspNetCore.FunctionalTests/Kaleido.Queryable.AspNetCore.FunctionalTests.csproj`