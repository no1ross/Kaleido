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

`ExceptionMiddleware` catches the following exception types:

| Exception | HTTP status | Error code | Log level |
|-----------|------------|------------|-----------|
| `KaleidoFrameworkException` | 500 | `framework_error` | Error |
| `ArgumentException` | 400 | `argument_error` | Warning |
| `InvalidOperationException` | 400 | `invalid_operation` | Warning |

For each it writes a `KaleidoErrorResponse` body.

`KaleidoFrameworkException` is thrown by internal runtime paths that detect integrity violations
(broken DI wiring, unexpected type mismatches in engine dispatch). It is not a user-facing error.

`QueryableValidationException` subtypes (user query-input errors) are handled directly at the
Queryable endpoint level and do not reach this middleware.

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
