# Queryable.AspNetCore

This project contains the ASP.NET Core transport layer for Queryable.

See also:
- [`../README.md`](../README.md)
- [`../ARCHITECTURE.md`](../ARCHITECTURE.md)
- [`../AGENTS.md`](../AGENTS.md)

## What lives here

This project contains:
- ASP.NET Core service registration via [`AddQueryableAspNetCore`](./QueryableAspNetCoreServiceCollectionExtensions.cs)
- endpoint publishing via [`MapQueryable`](./QueryableEndpointRouteBuilderExtensions.cs)
- route/path helpers used by endpoint publishing
- request normalization in [`QueryableValueNormalizer`](./QueryableValueNormalizer.cs)
- OpenAPI-related support

HTTP request/response contract types now live in:
- [`../AspNetCore.Abstractions/README.md`](../AspNetCore.Abstractions/README.md)

## What this project is for

Work here when you are changing:
- endpoint shapes or route naming
- queryable catalog/registry publishing
- how ASP.NET Core publishes or enriches metadata responses
- ASP.NET Core normalization behavior
- HTTP runtime error behavior

## How a developer uses this project

Use this project when you want to expose Queryable over ASP.NET Core.

Typical setup:

1. register Queryable with `AddQueryable()`
2. call `AddQueryableAspNetCore(...)`
3. optionally configure route options such as `RoutePrefix`
4. map the endpoints with `app.MapQueryable()`

Example:

```csharp
builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MyDbContext).Assembly)
    .AddQueryable()
        .AddQueryableAspNetCore(options =>
        {
            options.RoutePrefix = "my-service";
        });

var app = builder.Build();
app.MapQueryable();
```

The published HTTP contracts consumed by clients and documentation tooling live in:
- [`../AspNetCore.Abstractions/README.md`](../AspNetCore.Abstractions/README.md)

## HTTP surface

This project publishes:
- the queryable catalog endpoint
- the full registry endpoint
- context metadata endpoints
- direct context query endpoints
- local view query endpoints
- delegated view query endpoints

It remains context-centric for discovery, even when execution is delegated through views.

## Verification

Typical verification for this project:

- `dotnet test tests/Queryable/AspNetCore.UnitTests/Kaleido.Queryable.AspNetCore.UnitTests.csproj`
- `dotnet test tests/Queryable/AspNetCore.FunctionalTests/Kaleido.Queryable.AspNetCore.FunctionalTests.csproj`

Also run core runtime tests if the transport change depends on runtime dispatch or registry behavior:
- `dotnet test tests/Queryable/UnitTests/Kaleido.Queryable.UnitTests.csproj`