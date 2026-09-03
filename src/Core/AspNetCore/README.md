# Core.AspNetCore

This project contains the ASP.NET Core transport support shared by Kaleido's foundational layer.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)

## What lives here

This project currently contains:
- shared exception-handling middleware
- a shared HTTP error response contract
- shared correlation-header names and parsing behavior

## What this project is for

Reference this project when you need to:
- add Kaleido's shared exception middleware to an ASP.NET Core pipeline
- return the standard Core HTTP error payload for handled request failures
- read shared Kaleido correlation headers into a `KaleidoCorrelationContext`

## Main entry point

### `UseKaleidoExceptionHandling()`
Adds the shared `ExceptionMiddleware` to the application pipeline.

```csharp
app.UseKaleidoExceptionHandling();
```

## Exception behavior

`ExceptionMiddleware` currently catches:
- `ArgumentException`
- `InvalidOperationException`

For those failures it:
- logs a warning
- returns HTTP 400
- writes [`ApiErrorContract`](./Contracts/ApiErrorContract.cs)

Important distinction:
- this is targeted exception normalization
- it is not a general-purpose global exception handler for all exception types

## Correlation headers

[`KaleidoAspNetCoreHeaders`](./Observability/KaleidoAspNetCoreCorrelation.cs) defines the shared header names used to populate Core correlation identity.

[`KaleidoAspNetCoreCorrelation`](./Observability/KaleidoAspNetCoreCorrelation.cs) reads those headers and parses GUID-backed values, failing fast when a GUID header is malformed.

## What this project does not do

This project does **not** contain:
- capability-specific endpoint registration
- business request orchestration
- runtime capability registries
- feature-specific request/response contracts beyond the shared error payload

Its scope should remain thin and transport-focused.

## Where to look

- [`AspNetCoreServiceCollectionExtensions`](./AspNetCoreServiceCollectionExtensions.cs)
- [`ExceptionMiddleware`](./Middleware/ExceptionMiddleware.cs)
- [`ApiErrorContract`](./Contracts/ApiErrorContract.cs)
- [`KaleidoAspNetCoreCorrelation`](./Observability/KaleidoAspNetCoreCorrelation.cs)
