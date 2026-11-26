# Kaleido.Hosting.Http

This HTTP host composition package provides the opt-in fluent endpoint surface `MapKaleido()` on an ASP.NET `IEndpointRouteBuilder`. It does **not** choose capabilities, a provider or Registry participation. It maps no routes by itself. Each explicit `IKaleidoHttpEndpointBuilder` extension delegates to its owning adapter: `MapProcess()` to Process.Http.Server's `MapProcessor()`, `MapQueryable()` to Queryable.Http.Server's `MapQueryable()`, and `MapCentralRegistry()` to Registry.Http.Server's `MapKaleidoControlRegistry()`. The returned interface carries one shared root route group, so later calls remain siblings rather than nesting their routes; conventions applied before or after selections reach only those Kaleido routes, not other host endpoints. If a host leaves the neutral authorization mode unspecified, it defaults to `Authenticated`: mapping requires a host authentication scheme, and an undeclared step refuses anonymous callers even without a host-wide `RequireAuthorization()` convention.

For a service that registered local Process and Queryable via `AddHosting()`, calls `AddHttp()` and `AddProcessHttpServer()`, and explicitly chose its required stores:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.MapKaleido().MapQueryable().MapProcess().RequireAuthorization();
```

A dedicated central Registry host instead calls `AddServer()`, `AddHttp()`, `AddControlRegistry()`, chooses a Registry store, calls `AddRegistryHttpServer()`, then maps **only** its fixed authority endpoints:

```csharp
app.MapKaleido().MapCentralRegistry();
```

The central Registry remains the only authority for distributed discovery and route selection. An HTTP service that must be remotely discovered **separately opts into** `AddRegistryHttpParticipation()` and publishes its capability HTTP bindings; mapping local routes alone never publishes them. A Registry authority host does not register itself as a client. There is no `MapLocalRegistry()`: the old per-service Registry HTTP route was removed. The underlying three owner mappers remain public for hosts that prefer to call them directly. The [composed TestServer suite](../../../../tests/Composed/Hosts/Kaleido.Hosting.Http.FunctionalTests/KaleidoHttpCompositionFunctionalTests.cs) checks that host endpoints and host exception pages keep their own responses while selected Kaleido routes alone echo correlation and answer malformed headers/state paths with structured 400s. An explicit `MapProcess()` or `MapQueryable()` checks `AddHttp()` and the effective neutral Server authorization mode; `MapProcess()` also requires `AddProcessHttpServer()`. Both require a configured host authentication scheme when enforcement is on; the capability adapters own per-step/source/view ZeroTrust rules. Generic Process submission cannot bypass the rule that keeps an undeclared step off a direct route, while declared steps still enforce their roles. See [ADR 0050](../../../../docs/decisions/0050-explicit-http-host-mapping.md) and [HTTP architecture](../../../../docs/architecture/transports/http.md).
