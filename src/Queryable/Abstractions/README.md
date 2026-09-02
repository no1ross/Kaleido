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
  - [`IQueryContextSource`](./Query/IQueryContextSource.cs)
  - [`IQueryViewSource`](./Query/IQueryViewSource.cs)
  - [`IDelegateQueryViewSource`](./Query/IDelegateQueryViewSource.cs)
- the public execution entry point [`IQueryableService`](./IQueryableService.cs)

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

Those live in:
- [`../Queryable/README.md`](../Queryable/README.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)

## When to be careful

Changes here usually ripple into:
- runtime registration and validation
- ASP.NET Core transport contracts
- tests
- samples

If you change a public contract, verify affected runtime and transport behavior.