# Registry

Registry is Kaleido's aggregated discovery surface. It provides a single HTTP endpoint that combines the process and queryable registrations from a host processor and all of its registered downstream clients into one response.

---

## What it solves

When a host processor (e.g. Intake) delegates work to one or more downstream processors and queryable services, a consumer would otherwise need to call every individual registry endpoint and merge the results client-side. Registry does that aggregation server-side, behind one call.

A consumer calls `GET /{routePrefix}/registry` and receives a single `AggregatedRegistryResponse` containing:

- **`Processes`** — the host processor's own steps, plus the registry records of every downstream processor registered via `AddProcessClient()`
- **`Queryables`** — the host's own local queryable contexts (when `AddQueryable()` has been called), plus the registry records of every downstream queryable client registered via `AddQueryableClient()`
- **`ClientErrors`** — any downstream clients that were unreachable or returned errors during aggregation

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
      "steps": [ "..." ]
    },
    {
      "name": "radiology",
      "displayName": "Prior Auth Radiology",
      "steps": [ "..." ]
    }
  ],
  "queryables": [
    {
      "name": "members",
      "displayName": "Members",
      "views": [ "..." ]
    }
  ],
  "clientErrors": []
}
```

`Processes` is ordered alphabetically by processor name. `Queryables` is ordered alphabetically by context name. Each entry is the same shape as the individual registry responses returned by `MapProcessor()` and `MapQueryable()` respectively.

When a downstream client fails, `ClientErrors` is non-empty:

```json
{
  "processes": [ "..." ],
  "queryables": [ "..." ],
  "clientErrors": [
    {
      "clientName": "Member",
      "clientType": "Process",
      "reason": "Connection refused (127.0.0.1:8084)"
    },
    {
      "clientName": "CodeSet",
      "clientType": "Queryable",
      "reason": "Connection refused (127.0.0.1:8082)"
    }
  ]
}
```

---

## Fault isolation and partial responses

The endpoint **always returns HTTP 200**. Each downstream client call is wrapped independently — if a client is unavailable, its registrations are absent from the response and a `RegistryClientError` entry is added to `ClientErrors`.

A non-empty `ClientErrors` collection means the response is **partial**: one or more downstream services were unreachable or misconfigured. Consumers must inspect `ClientErrors` to detect missing registrations rather than assuming a 200 response means everything loaded correctly.

`clientName` matches the `Name` passed to `AddProcessClient()` or `AddQueryableClient()`. `clientType` is either `"Process"` or `"Queryable"`.

---

## Dependency model

Registry references only the `AspNetCore.Client` projects for both Process and Queryable. It does not reference `Process/AspNetCore` or `Queryable/AspNetCore` (the server projects). The internal client route-option maps (`KaleidoProcessClientRouteOptionsMap`, `KaleidoQueryableClientRouteOptionsMap`) are accessed via `InternalsVisibleTo` granted from those assemblies.

`ProcessorRegistryResponseFactory` lives in `Process/AspNetCore.Abstractions`. `QueryableRecordResponse.FromRegistryItem` and `QueryableRouteOptions` live in `Queryable/AspNetCore.Abstractions` and `Queryable/Abstractions` respectively — both are reachable transitively through `Queryable/AspNetCore.Client`. Registry does not need a direct reference to the full server projects.

---

## Scope

Registry is intentionally narrow:

- it does **not** proxy execution
- it does **not** own routing or nginx configuration
- it does **not** merge or deduplicate step names across processors

Its only job is aggregating registry metadata across local and downstream sources into one discoverable response.

---

## See also

- [`src/Process/README.md`](../Process/README.md) — process registration and `AddProcessClient()`
- [`src/Queryable/README.md`](../Queryable/README.md) — queryable registration and `AddQueryableClient()`
- [`samples/PriorAuth/AGENTS.md`](../../samples/PriorAuth/AGENTS.md) — local ports and service wiring
- [`samples/PriorAuth/HANDOFF.md`](../../samples/PriorAuth/HANDOFF.md) — cross-processor handoff pattern
