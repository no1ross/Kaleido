# Queryable.AspNetCore

This project contains the ASP.NET Core transport layer for Queryable.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)

## What lives here

This project contains:
- ASP.NET Core service registration via [`AddQueryableAspNetCore`](./QueryableAspNetCoreServiceCollectionExtensions.cs)
- endpoint publishing via [`MapQueryable`](./QueryableEndpointRouteBuilderExtensions.cs)
- request/response contracts in [`Contracts/`](./Contracts)
- request normalization in [`QueryableValueNormalizer`](./QueryableValueNormalizer.cs)
- OpenAPI-related support

## What this project is for

Work here when you are changing:
- endpoint shapes or route naming
- queryable catalog/registry publishing
- metadata response shaping
- transport request contracts
- ASP.NET Core normalization behavior
- HTTP-facing error behavior

## HTTP surface

This project publishes:
- the queryable catalog endpoint
- the full registry endpoint
- context metadata endpoints
- direct context query endpoints
- local view query endpoints
- delegated view query endpoints

It remains context-centric for discovery, even when execution is delegated through views.

## Verification

Typical verification for this project:

- `dotnet test tests/Queryable/AspNetCore.UnitTests/Kaleido.Queryable.AspNetCore.UnitTests.csproj`
- `dotnet test tests/Queryable/AspNetCore.FunctionalTests/Kaleido.Queryable.AspNetCore.FunctionalTests.csproj`

Also run core runtime tests if the transport change depends on runtime dispatch or registry behavior:
- `dotnet test tests/Queryable/UnitTests/Kaleido.Queryable.UnitTests.csproj`