# Kaleido.Server (temporary options boundary)

`Kaleido.Server.Next` is a non-packable, transport-neutral project holding the service's capability authorization mode and default rule. `AddServer(options?)` binds the existing `Kaleido:AuthorizationMode` and `Kaleido:DefaultAuthorization` keys over code options, validates them, and registers the effective options for host composition. It neither authenticates callers nor adds HTTP, ASP.NET Core, gRPC, or an endpoint; consumers will use the higher-level Hosting entry point instead.

The directory already has the intended `Kaleido.Server` name. Only the temporary `.Next.csproj` and assembly names distinguish it from the existing shipped `src/Server/Kaleido.Server.csproj` until that project's code and history are migrated. See [authorization architecture](../../../docs/architecture/authorization.md) and [dependency boundaries](../../../docs/architecture/dependencies.md).
