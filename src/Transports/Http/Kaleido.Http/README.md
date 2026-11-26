# Kaleido.Http

`Kaleido.Http` is the ASP.NET-free HTTP wire base shared by future Kaleido HTTP clients and servers. It owns the versioned `KaleidoHttpRoutePrefix`, `KaleidoCorrelationHeaders`, `HttpHeaderSanitizerExtensions`, `KaleidoJsonOptions` and the structured `KaleidoErrorResponse`/`KaleidoError`/`KaleidoErrorCodes` shape. These were moved here from the mixed Platform package without changing route/header/error strings or camelCase JSON and string-enum behavior. The route prefix and sanitizer are documented public utilities because capability HTTP adapters and outbound clients call them across project boundaries; there is no Platform project reference.

This project does **not** map endpoints, implement ASP.NET filters, call the central registry or own Process/Queryable/Registry-specific URL paths or request bodies. Those capability and registry routes have been Git-moved to their eventual `.Http` owners and are not claimed runnable until their own migrations. The old HTTP server/client projects still reside under `src/Http/`; their active gates have not been run for this shared-wire unit.

See [HTTP transport boundaries](../../../../docs/architecture/transports/http.md) and the [transport scope](../AGENTS.md) for the dependency direction and change rules.
