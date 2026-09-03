# Queryable.AspNetCore.Abstractions

This project contains the ASP.NET Core-facing contract types for Queryable.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)

## What lives here

This project contains:
- HTTP request contracts such as [`QueryApiRequest`](./Contracts/QueryApiRequest.cs)
- HTTP error contracts such as [`QueryErrorResponse`](./Contracts/QueryErrorResponse.cs)
- HTTP registry/discovery contracts such as [`QueryableRecordResponse`](./Contracts/QueryableRecordResponse.cs)
- HTTP route URL helpers used by those contracts

These contracts are transport-facing, but they are not the ASP.NET Core runtime implementation.

## What this project is for

Reference this project when you need to:
- deserialize Queryable catalog or registry responses
- issue Queryable HTTP requests using the shared request contract shapes
- build documentation or tooling against the published Queryable HTTP surface
- consume the HTTP contract types without referencing the full ASP.NET Core runtime package

## Relationship to core Queryable metadata

The HTTP registry contracts in this project inherit from the core Queryable registry items and descriptors.

That means:
- Queryable core defines the semantic capability shape
- this project adds HTTP-specific properties like metadata/query URLs
- the full ASP.NET Core runtime project is responsible for actually publishing endpoints

## What this project does not do

This project does **not** contain:
- ASP.NET Core service registration
- endpoint mapping
- request normalization
- runtime query execution

Those live in:
- [`../AspNetCore/README.md`](../AspNetCore/README.md)
- [`../Queryable/README.md`](../Queryable/README.md)

## Typical usage

A normal service exposing Queryable over HTTP will reference the full ASP.NET Core project at runtime.
A client, orchestrator, or documentation generator that only needs the published request/response contract shapes can reference this abstractions project instead.
