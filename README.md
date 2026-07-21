# Kaleido

Kaleido is a metadata-driven reference data framework for .NET.

Rather than creating a dedicated API, repository, service, controller, query model, validation layer, paging implementation, and metadata endpoint for every reference dataset, Kaleido provides a common framework for:

- Record discovery
- Metadata generation
- Filtering
- Searching
- Sorting
- Paging
- Named queries
- Typed access
- Dynamic access
- REST-friendly request and response contracts

Kaleido separates **data retrieval** from **query behavior**, allowing consumers to interact with reference data consistently regardless of where the underlying data resides.

Supported source implementations can include:

- CSV files
- SQLite
- SQL Server
- Sybase
- External REST APIs
- In-memory collections
- Custom providers

The goal is simple:

> Add one record, add one source, optionally add named queries, and let Kaleido provide the common metadata and query infrastructure.

---

## Why Kaleido Exists

Enterprise environments often accumulate many small APIs that expose reference data.

Examples include:

- Regions
- Products
- Status values
- Categories
- Clients
- Configuration values
- Lookup tables
- Legacy reference tables

Although the underlying data differs, the required functionality is usually identical:

- Retrieve metadata
- Search records
- Filter records
- Sort results
- Page results
- Execute reusable business-specific queries

Without a common framework, every new dataset tends to create another custom API, another custom request model, another custom controller, another custom paging approach, and another custom implementation of filtering and sorting.

Kaleido standardizes these behaviors so new reference datasets can be onboarded with minimal effort while still supporting multiple underlying data sources.

---

## Key Features

Kaleido provides:

- Metadata-driven record discovery
- Attribute-based record descriptions
- Source discovery and registration
- Named query support
- Typed query execution
- Dynamic query execution by record key
- Query validation
- Query compilation
- Filtering
- Searching
- Sorting
- Paging
- REST-friendly query contracts
- Data-source independence
- Dependency injection integration
- Functional and unit test coverage

---

## Quick Start

A working Kaleido implementation requires:

- **One Record**
- **Exactly one Source for that Record**
- **Zero or more Named Queries**
- **Registration using `AddKaleido(...)`**

The minimum implementation is:

```text
1 Record
1 Source
0..N Named Queries
```

---

## Installation and Setup

Register Kaleido during application startup.

```csharp
builder.Services.AddKaleido(options =>
{
    options.ValidateRegistrations = true;

    options.Assemblies.Add(
        typeof(RegionRecord).Assembly);
});
```

Kaleido scans the configured assemblies and automatically discovers:

- Records
- Sources
- Named Queries

If no assemblies are registered, Kaleido will throw during startup.

```csharp
builder.Services.AddKaleido(options =>
{
    options.Assemblies.Add(typeof(RegionRecord).Assembly);
});
```

For most applications, the assembly containing the record, source, and named query implementations is enough.

If those types are split across multiple assemblies, register each assembly.

```csharp
builder.Services.AddKaleido(options =>
{
    options.ValidateRegistrations = true;

    options.Assemblies.Add(typeof(RegionRecord).Assembly);
    options.Assemblies.Add(typeof(RegionSource).Assembly);
    options.Assemblies.Add(typeof(ActiveRegionsQuery).Assembly);
});
```

---

## Registration Rules

Kaleido validates registrations so configuration errors fail early.

### Records

Every record must be discoverable and decorated with `KaleidoRecordAttribute`.

```csharp
[KaleidoRecord("regions", "1.0.0", "Region Reference Data")]
public sealed class RegionRecord
{
}
```

### Sources

Every record must have exactly one source.

Valid:

```text
RegionRecord -> RegionSource
```

Invalid:

```text
RegionRecord -> No Source
RegionRecord -> Multiple Sources
```

Kaleido intentionally enforces one source per record for version 1 so record ownership and execution behavior remain unambiguous.

### Named Queries

Named queries are optional.

A record may have:

```text
0 named queries
1 named query
Many named queries
```

Named queries are reusable query operations that apply business-specific filtering logic.

---

## Complete Runnable Console Example

The following example contains a complete working Kaleido setup in a single console application.

It demonstrates:

- A record
- A source
- A named query
- Dependency injection registration
- `AddKaleido(...)`
- A typed query
- Filtering
- Sorting
- Paging

> This example is intentionally small and in-memory so the Kaleido concepts are clear. Real providers can use CSV, SQLite, SQL Server, Sybase, APIs, or any other source.

```csharp
using Kaleido;
using Kaleido.Queryable;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddSingleton<RegionData>();

services.AddKaleido(options =>
{
    options.ValidateRegistrations = true;

    options.Assemblies.Add(
        typeof(RegionRecord).Assembly);
});

using var provider = services.BuildServiceProvider();

var catalog =
    provider.GetRequiredService<IKaleidoCatalog>();

var request = new KaleidoQueryRequest(
    QueryName: "active-regions",
    Query: new KaleidoQueryBody(
        Search: null,
        Filter: new QueryFilterNode(
            Condition: new QueryFilterCondition(
                Field: "Name",
                Operator: FilterOperator.Contains,
                Values: new List<object?> { "a" }),
            Group: null),
        Sort: new List<QuerySort>
        {
            new QuerySort(
                Field: "Name",
                Direction: SortDirection.Ascending)
        },
        Page: new QueryPage(
            Size: 25,
            Offset: 0)),
    Parameters: null);

var response =
    await catalog.QueryAsync<RegionRecord>(request);

Console.WriteLine($"Total Count: {response.TotalCount}");

foreach (var region in response.Items)
{
    Console.WriteLine($"{region.Code} - {region.Name}");
}

[KaleidoRecord(
    "regions",
    "1.0.0",
    "Region Reference Data")]
[AllowedQuery(
    "active-regions",
    "Returns active regions.")]
[Pageable(
    defaultSize: 25,
    maxSize: 100)]
public sealed class RegionRecord
{
    [Key]
    [Filterable(FilterOperator.Eq)]
    [Sortable]
    public string Code { get; init; } = string.Empty;

    [Filterable(
        FilterOperator.Eq,
        FilterOperator.Contains,
        FilterOperator.StartsWith,
        FilterOperator.EndsWith)]
    [Searchable(
        priority: 1,
        MatchMode.Exact,
        MatchMode.Contains,
        MatchMode.StartsWith,
        MatchMode.EndsWith)]
    [Sortable]
    public string Name { get; init; } = string.Empty;

    [Filterable(FilterOperator.Eq, FilterOperator.IsTrue, FilterOperator.IsFalse)]
    [Sortable]
    public bool IsActive { get; init; }
}

public sealed class RegionSource
    : IQueryableRecordSource<RegionRecord>
{
    private readonly RegionData _data;

    public RegionSource(RegionData data)
    {
        _data = data;
    }

    public IQueryable<RegionRecord> Query()
    {
        return _data.Regions.AsQueryable();
    }
}

public sealed class ActiveRegionsQuery
    : IQueryableRecordNamedQuery<RegionRecord>
{
    public string Name => "active-regions";

    public IQueryable<RegionRecord> Apply(
        IQueryable<RegionRecord> query,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        return query.Where(x => x.IsActive);
    }
}

public sealed class RegionData
{
    public IReadOnlyList<RegionRecord> Regions { get; } =
    [
        new RegionRecord
        {
            Code = "US",
            Name = "United States",
            IsActive = true
        },
        new RegionRecord
        {
            Code = "CA",
            Name = "Canada",
            IsActive = true
        },
        new RegionRecord
        {
            Code = "MX",
            Name = "Mexico",
            IsActive = true
        },
        new RegionRecord
        {
            Code = "XX",
            Name = "Legacy Region",
            IsActive = false
        }
    ];
}
```

Expected output:

```text
Total Count: 2
CA - Canada
US - United States
```

The inactive record is removed by the named query, the remaining records are filtered by name, sorted by name, and paged.

---

## Core Concepts

### Record

A record describes a reference dataset.

Records define:

- Record key
- Version
- Source description
- Fields
- Filterable fields
- Searchable fields
- Sortable fields
- Pageable behavior
- Allowed named queries

Example:

```csharp
[KaleidoRecord("products", "1.0.0", "Product Reference Data")]
public sealed class ProductRecord
{
    [Key]
    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
```

### Source

A source supplies data for a record.

```csharp
public sealed class ProductSource
    : IQueryableRecordSource<ProductRecord>
{
    public IQueryable<ProductRecord> Query()
    {
        return GetProducts().AsQueryable();
    }
}
```

The source does not need to implement filtering, sorting, searching, paging, or metadata generation. Kaleido handles those behaviors.

### Named Query

A named query applies reusable business logic.

```csharp
public sealed class ActiveProductsQuery
    : IQueryableRecordNamedQuery<ProductRecord>
{
    public string Name => "active-products";

    public IQueryable<ProductRecord> Apply(
        IQueryable<ProductRecord> query,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        return query.Where(x => x.IsActive);
    }
}
```

Named queries are optional.

---

## Querying Records

Kaleido supports both typed and dynamic query execution.

### Typed Query Execution

Use typed queries when the record type is known at compile time.

```csharp
var response =
    await catalog.QueryAsync<RegionRecord>(request);

foreach (var item in response.Items)
{
    Console.WriteLine(item.Name);
}
```

Typed queries return strongly typed items.

### Dynamic Query Execution

Use dynamic queries when the record key is selected at runtime.

```csharp
var response =
    await catalog.QueryAsync(
        "regions",
        request);
```

Dynamic queries are useful for:

- REST APIs
- Metadata-driven user interfaces
- Administrative tools
- Generic record browsers

---

## Query Request Contract

Kaleido uses a JSON-friendly query request model.

```csharp
public record KaleidoQueryRequest(
    string? QueryName,
    KaleidoQueryBody? Query,
    IReadOnlyDictionary<string, object?>? Parameters = null);

public record KaleidoQueryBody(
    QuerySearchNode? Search,
    QueryFilterNode? Filter,
    IReadOnlyList<QuerySort>? Sort,
    QueryPage? Page);
```

The request contract intentionally avoids interface-based polymorphism so REST clients, OpenAPI tools, Postman, JavaScript, Java, Python, and other consumers can construct requests without custom serializer configuration.

---

## Filtering

A filter can be either a condition or a group.

### Filter Condition

```json
{
  "query": {
    "filter": {
      "condition": {
        "field": "Category",
        "operator": "Eq",
        "values": [
          "Finance"
        ]
      }
    }
  }
}
```

### Filter Group

```json
{
  "query": {
    "filter": {
      "group": {
        "operator": "And",
        "filters": [
          {
            "condition": {
              "field": "Category",
              "operator": "Eq",
              "values": [
                "Finance"
              ]
            }
          },
          {
            "condition": {
              "field": "Status",
              "operator": "Eq",
              "values": [
                "Active"
              ]
            }
          }
        ]
      }
    }
  }
}
```

A filter node must contain exactly one of:

- `condition`
- `group`

It cannot contain both, and it cannot contain neither.

---

## Searching

A search can be either a condition or a group.

### Search Condition

```json
{
  "query": {
    "search": {
      "condition": {
        "searchText": "north",
        "matchMode": "Contains"
      }
    }
  }
}
```

### Search Condition for a Specific Field

```json
{
  "query": {
    "search": {
      "condition": {
        "searchText": "north",
        "matchMode": "Contains",
        "field": "Region"
      }
    }
  }
}
```

### Search Group

```json
{
  "query": {
    "search": {
      "group": {
        "operator": "Or",
        "searches": [
          {
            "condition": {
              "searchText": "north",
              "matchMode": "Contains",
              "field": "Region"
            }
          },
          {
            "condition": {
              "searchText": "admin",
              "matchMode": "Contains",
              "field": "GroupName"
            }
          }
        ]
      }
    }
  }
}
```

A search node must contain exactly one of:

- `condition`
- `group`

---

## Sorting

```json
{
  "query": {
    "sort": [
      {
        "field": "Name",
        "direction": "Ascending"
      }
    ]
  }
}
```

Multiple sorts may be supplied.

```json
{
  "query": {
    "sort": [
      {
        "field": "Category",
        "direction": "Ascending",
        "sequence": 1
      },
      {
        "field": "Name",
        "direction": "Descending",
        "sequence": 2
      }
    ]
  }
}
```

---

## Paging

```json
{
  "query": {
    "page": {
      "size": 25,
      "offset": 50
    }
  }
}
```

Paging behavior is controlled by record metadata.

```csharp
[Pageable(defaultSize: 25, maxSize: 500)]
public sealed class RegionRecord
{
}
```

If a request does not specify a page size, Kaleido uses the record default. If a requested size exceeds the configured maximum, the compiled query clamps the size to the maximum.

---

## Named Queries

Named queries are invoked by name.

```json
{
  "queryName": "active-regions"
}
```

Named queries may also require parameters.

```json
{
  "queryName": "regions-by-country",
  "parameters": {
    "countryCode": "US"
  }
}
```

Named query parameters are validated before execution.

---

## REST API Integration

Kaleido supports dynamic query execution, which makes it straightforward to expose records through HTTP.

Example controller:

```csharp
using Kaleido;
using Kaleido.Metadata;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("v1/records")]
public sealed class KaleidoRecordController : ControllerBase
{
    private readonly IKaleidoCatalog _catalog;

    public KaleidoRecordController(IKaleidoCatalog catalog)
    {
        _catalog = catalog;
    }

    [HttpGet]
    public ActionResult<IEnumerable<RecordMetadata>> GetRecords()
    {
        return Ok(_catalog.GetAll());
    }

    [HttpGet("{recordKey}")]
    public ActionResult<RecordMetadata> GetRecord(string recordKey)
    {
        var metadata = _catalog.Get(recordKey);

        if (metadata is null)
        {
            return NotFound();
        }

        return Ok(metadata);
    }

    [HttpPost("{recordKey}/query")]
    public async Task<ActionResult<KaleidoQueryResponse>> Query(
        string recordKey,
        [FromBody] KaleidoQueryRequest request)
    {
        var response =
            await _catalog.QueryAsync(recordKey, request);

        return Ok(response);
    }
}
```

Only one API sample is needed because HTTP behavior is independent of the underlying data source. Other samples focus on provider implementation.

---

## Query Response Contract

Dynamic queries return a non-generic response suitable for REST APIs.

```csharp
public sealed record KaleidoQueryResponse(
    RecordDescriptor Descriptor,
    int TotalCount,
    IReadOnlyList<object> Items);
```

Typed queries return a strongly typed response.

```csharp
public sealed record KaleidoQueryResponse<TRecord>(
    RecordDescriptor Descriptor,
    int TotalCount,
    IReadOnlyList<TRecord> Items)
    where TRecord : class;
```

---

## Metadata

Kaleido exposes metadata for each record so consumers can understand available fields and query capabilities.

Metadata includes:

- Record name
- Version
- Source description
- Fields
- Field types
- Filterable fields
- Supported filter operators
- Searchable fields
- Supported match modes
- Sortable fields
- Named queries
- Paging configuration

This enables metadata-driven clients, generic admin tools, and dynamic user interfaces.

---

## What Kaleido Provides

Once a record and source are registered, Kaleido automatically provides:

- Discovery
- Metadata generation
- Registration validation
- Query validation
- Query compilation
- Filtering
- Searching
- Sorting
- Paging
- Named query execution
- Typed access
- Dynamic access
- REST-friendly query contracts

---

## Creating a Custom Provider

Providers should focus only on retrieving data.

Example:

```csharp
public sealed class CustomerSource
    : IQueryableRecordSource<CustomerRecord>
{
    private readonly CustomerRepository _repository;

    public CustomerSource(CustomerRepository repository)
    {
        _repository = repository;
    }

    public IQueryable<CustomerRecord> Query()
    {
        return _repository
            .GetCustomers()
            .AsQueryable();
    }
}
```

You do not need to implement the same query behavior repeatedly for every provider.

Kaleido handles:

- Filtering
- Searching
- Sorting
- Paging
- Metadata generation
- Named query execution

---

## Samples

The repository includes sample implementations demonstrating different source technologies.

| Sample | Purpose |
| --- | --- |
| `Kaleido.Samples.Csv` | CSV-backed source |
| `Kaleido.Samples.SQLite` | SQLite-backed source |
| `Kaleido.Samples.SqlServer` | SQL Server-backed source |
| `Kaleido.Samples.Sybase` | Sybase-backed source |
| `Kaleido.Samples.Api` | REST API integration |

The API sample demonstrates exposing Kaleido through HTTP.

The remaining samples demonstrate how to implement and register providers for different data sources.

---

## Testing

Kaleido includes unit and functional tests.

### Unit Tests

Unit tests validate:

- Registration
- Discovery
- Metadata generation
- Record registry behavior
- Query validation
- Query compilation
- Dispatching
- Filtering
- Searching
- Sorting
- Paging
- Service registration

The core framework maintains greater than 90% code coverage.

### Functional Tests

Functional tests validate end-to-end behavior with a CSV-backed source.

They cover:

- Dependency injection registration
- Record discovery
- Metadata generation
- Typed query execution
- Dynamic query execution
- REST API execution through `WebApplicationFactory`
- Request serialization
- Response serialization
- Filtering
- Searching
- Sorting
- Paging
- Named queries

Functional tests intentionally focus on validating the framework behavior rather than testing every possible provider implementation.

---

## Architecture Overview

```text
Consumer
    |
    v
IKaleidoCatalog
    |
    v
Record Dispatcher
    |
    v
Record Query Validator
    |
    v
Record Query Compiler
    |
    v
Query Engine
    |
    v
Record Source
    |
    v
Data Provider
```

The same pipeline is used regardless of whether data originates from CSV, SQLite, SQL Server, Sybase, an external API, or another custom provider.

---

## Design Decisions

### One Source Per Record

Kaleido enforces exactly one source per record for version 1.

This keeps execution behavior predictable and avoids ambiguity around which provider owns a given record.

### REST-Friendly Request Model

The public request model avoids interface-based polymorphism.

Instead of requiring custom JSON converters or serializer-specific known-type configuration, Kaleido uses explicit node models:

- `QueryFilterNode`
- `QueryFilterCondition`
- `QueryFilterGroup`
- `QuerySearchNode`
- `QuerySearchCondition`
- `QuerySearchGroup`

This makes the contract easier to use from REST clients, OpenAPI tools, Postman, JavaScript, Java, Python, and other platforms.

### Internal Generic Pipeline

Internally, Kaleido can still use generic services and strongly typed execution.

This allows typed .NET consumers to work with concrete record types while still supporting dynamic REST scenarios.

---

## Design Goals

Kaleido was designed with the following principles:

- Minimal boilerplate
- Metadata-driven discovery
- Consistent query behavior
- Provider independence
- REST-friendly contracts
- Strong testability
- Enterprise maintainability
- Long-term extensibility
- Easy onboarding of new datasets

A consumer should be able to onboard a new dataset by implementing:

```text
1 Record
1 Source
0..N Named Queries
```

and allow Kaleido to provide the remaining infrastructure automatically.

---

## When To Use Kaleido

Kaleido is a good fit when:

- Many reference datasets require similar query capabilities
- Consumers need consistent metadata and query behavior
- Data may live in different systems
- Teams want to avoid creating one-off APIs for every dataset
- Dynamic or metadata-driven clients are needed
- REST exposure is useful
- Legacy and modern sources need to coexist

---

## When Not To Use Kaleido

Kaleido may not be the right fit when:

- The dataset requires highly specialized business workflows
- The query model is completely custom
- The data is not reference-like
- The source cannot expose queryable or enumerable records
- A dedicated domain API is more appropriate

---

## Status

Kaleido is actively evolving and currently focuses on simplifying enterprise reference data access while reducing the proliferation of one-off APIs.

The framework is designed so consumers focus on defining records and data sources while Kaleido handles the common metadata and query infrastructure.
