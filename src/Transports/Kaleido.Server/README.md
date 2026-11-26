# Kaleido.Server

`Kaleido.Server` owns transport-neutral capability authorization mode and service defaults. `AddServer(options?)` binds the existing `Kaleido:AuthorizationMode` and `Kaleido:DefaultAuthorization` keys over code options, validates them, and registers the effective options for host composition. It neither authenticates callers nor adds HTTP, ASP.NET Core, gRPC, or an endpoint; consumers use the higher-level Hosting entry point instead.

HTTP-specific caller, filter, endpoint and registry adapters belong to their respective transport projects. See [authorization architecture](../../../docs/architecture/authorization.md) and [dependency boundaries](../../../docs/architecture/dependencies.md).
