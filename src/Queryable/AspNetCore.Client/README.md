# Queryable.AspNetCore.Client

This project contains an HTTP client for consuming Queryable query endpoints published by `Queryable.AspNetCore`.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AspNetCore/README.md`](../AspNetCore/README.md)
- [`../AspNetCore.Abstractions/README.md`](../AspNetCore.Abstractions/README.md)

## What lives here

This project contains:
- [`IKaleidoQueryableClient`](./IKaleidoQueryableClient.cs) — typed client interface for registry, metadata, view queries, and direct context queries
- [`IKaleidoQueryableClientFactory`](./IKaleidoQueryableClientFactory.cs) — factory interface resolved by registered name
- [`KaleidoQueryableClient`](./KaleidoQueryableClient.cs) — concrete HTTP client implementation
- [`KaleidoQueryableClientException`](./KaleidoQueryableClientException.cs) — exception type wrapping non-success HTTP responses
- [`KaleidoQueryableClientServiceCollectionExtensions`](./KaleidoQueryableClientServiceCollectionExtensions.cs) — `AddQueryableClient(name, baseUrl)` builder extension

## What this project is for

Use this project when a service needs to query a remote Queryable endpoint over HTTP.

Typical reasons:
- a process handler needs to look up reference data from another service before executing its business logic
- a delegated view implementation needs to call a downstream queryable service
- a service must query data from another bounded context without owning that context's runtime

## Registration

Register a named queryable client on the Kaleido builder:

```csharp
builder.Services.AddKaleido()
    .AddQueryableClient("MemberService", "https://member-service-host")
    .AddQueryableClient("CodeSet", "https://codeset-service-host")
    .AddQueryableClient("Radiology", "https://radiology-service-host", routePrefix: "radiology");
```

The optional `routePrefix` parameter must match the `RoutePrefix` configured on the remote server's `QueryableRouteOptions`. When omitted, no prefix is used (equivalent to `RoutePrefix = ""`).

Multiple named clients can be registered for different remote services.

## Usage

Inject `IKaleidoQueryableClientFactory` and resolve a client by name.

### Get the full registry

Fetches all context and view metadata from the remote service. The registry is lazily fetched and cached for the lifetime of the client instance — subsequent calls return the cached result without a new HTTP request.

```csharp
var registry = await clientFactory
    .GetClient("MemberService")
    .GetRegistryAsync(cancellationToken);

foreach (var context in registry)
{
    Console.WriteLine($"{context.Name}: {context.Views.Count} views");
}
```

### Get metadata for a single context

Fetches the full metadata record for one named context, including its fields, views, and query URLs.

```csharp
var metadata = await clientFactory
    .GetClient("MemberService")
    .GetContextMetadataAsync("Members", cancellationToken);

foreach (var view in metadata.Views)
{
    Console.WriteLine($"{view.Name}: {view.QueryUrl}");
}
```

### View query with typed parameters

```csharp
var result = await clientFactory
    .GetClient("MemberService")
    .QueryViewAsync<MemberDetailsParameters, MemberDetailsView>(
        "Members",
        "MemberDetails",
        new QueryApiRequest<MemberDetailsParameters>
        {
            Parameters = new MemberDetailsParameters
            {
                MemberId = memberId
            }
        },
        cancellationToken);

var member = result.Results.SingleOrDefault();
```

### Direct context query

```csharp
var result = await clientFactory
    .GetClient("CodeSet")
    .QueryContextAsync<ProcedureCodeView>(
        "ProcedureCodes",
        new QueryApiRequest
        {
            Query = new QueryBody(
                SearchText: codeValue,
                Filter: QueryFilterNode.CreateCondition(
                    "CodeSystem",
                    FilterOperator.Equals,
                    codeSystem.ToString()),
                Page: new QueryPage(Size: 25, Offset: 0))
        },
        cancellationToken);

var code = result.Results.SingleOrDefault();
```

## Result shape

Both methods return `QueryResult<TView>`, which contains:
- `TotalCount` — total matching records before paging
- `Offset` — page offset applied
- `PageSize` — page size applied
- `Results` — the materialized page of `TView` items

## Correlation

`KaleidoQueryableClient` automatically forwards Kaleido correlation headers on outbound requests.

## Error handling

Non-success HTTP responses throw `KaleidoQueryableClientException`. Inspect its properties for the HTTP status and any error body returned by the remote endpoint.

## What this project does not do

This project does **not** contain:
- server-side endpoint mapping or registration
- query validation or execution
- context or view registry behavior

Those live in:
- [`../AspNetCore/README.md`](../AspNetCore/README.md)
- [`../Queryable/README.md`](../Queryable/README.md)
