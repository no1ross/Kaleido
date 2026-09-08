# Registry

Registry is Kaleido's aggregated discovery surface. It provides a single HTTP endpoint that combines the process and queryable registrations from a host processor and all of its registered downstream clients into one response.

---

## What it solves

When a host processor (e.g. Intake) delegates work to one or more downstream processors and queryable services, a consumer would otherwise need to call every individual registry endpoint and merge the results client-side. Registry does that aggregation server-side, behind one call.

A consumer calls `GET /{routePrefix}/registry` and receives a single `AggregatedRegistryResponse` containing:

- **`Processes`** — the host processor's own steps, plus the registry records of every downstream processor registered via `AddProcessClient()`
- **`Queryables`** — the registry records of every downstream queryable client registered via `AddQueryableClient()`

All step URLs in the response are fully resolved (`executeUrl`, `metadataUrl`) — the consumer can navigate directly without knowing service topology.

---

## Usage

### 1. Add the project reference

```xml
<ProjectReference Include="..\..\src\Registry\Kaleido.Registry.csproj" />
```

### 2. Map the endpoint

```csharp
app.MapProcessor();
app.MapQueryable();
app.MapRegistry();                              // → GET /kaleido/registry (default)
```

Or with a custom prefix matching the service's own route prefix:

```csharp
app.MapRegistry(o => o.RoutePrefix = "intake"); // → GET /intake/kaleido/registry
```

### 3. No additional DI registration needed

`MapRegistry()` resolves everything it needs from existing DI registrations (`IProcessorRegistry`, `IKaleidoProcessClientFactory`, `IKaleidoQueryableClientFactory`, and the internal client route-option maps). Adding a new downstream client via `AddProcessClient()` or `AddQueryableClient()` makes it appear in the aggregated response automatically.

---

## Route options

`RegistryRouteOptions` controls the endpoint URL:

```csharp
public sealed class RegistryRouteOptions
{
    // Defaults to "kaleido", producing /kaleido/registry.
    // Override to match the host service's own route prefix.
    public string RoutePrefix { get; set; } = "kaleido";
}
```

Setting `RoutePrefix = ""` produces `/registry`.

---

## Response shape

```json
{
  "processes": [
    {
      "name": "intake",
      "displayName": "Prior Auth Intake",
      "steps": [ ... ]
    },
    {
      "name": "radiology",
      "displayName": "Prior Auth Radiology",
      "steps": [ ... ]
    }
  ],
  "queryables": [
    {
      "name": "members",
      "displayName": "Members",
      "views": [ ... ]
    }
  ]
}
```

`Processes` is ordered alphabetically by processor name. `Queryables` is ordered alphabetically by context name. Each entry is the same shape as the individual registry responses returned by `MapProcessor()` and `MapQueryable()` respectively.

---

## Fault isolation

Each downstream client call is wrapped in a `try/catch`. If a downstream processor or queryable client is unavailable at request time, that service's registrations are omitted from the aggregated response — the endpoint does not fail. Consumers should treat missing entries as transient and can handle them in their own retry or fallback logic.

---

## Dependency model

Registry references only the `AspNetCore.Client` projects for both Process and Queryable. It does not reference `Process/AspNetCore` or `Queryable/AspNetCore` (the server projects). The internal client route-option maps (`KaleidoProcessClientRouteOptionsMap`, `KaleidoQueryableClientRouteOptionsMap`) are accessed via `InternalsVisibleTo` granted from those assemblies.

`ProcessorRegistryResponseFactory` lives in `Process/AspNetCore.Abstractions` — not in the server project — so Registry can use it without pulling in the full ASP.NET Core server stack.

---

## Scope

Registry is intentionally narrow:

- it does **not** proxy execution
- it does **not** own routing or nginx configuration
- it does **not** merge or deduplicate step names across processors
- it does **not** include the host's own local queryable registry (that is already served by `MapQueryable()` at its existing endpoint)

Its only job is aggregating registry metadata across clients into one discoverable response.

---

## See also

- [`src/Process/README.md`](../Process/README.md) — process registration and `AddProcessClient()`
- [`src/Queryable/README.md`](../Queryable/README.md) — queryable registration and `AddQueryableClient()`
- [`samples/PriorAuth/AGENTS.md`](../../samples/PriorAuth/AGENTS.md) — local ports and service wiring
- [`samples/PriorAuth/HANDOFF.md`](../../samples/PriorAuth/HANDOFF.md) — cross-processor handoff pattern
