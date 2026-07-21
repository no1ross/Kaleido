# Kaleido

> A metadata-driven framework for exposing queryable business records through a consistent, strongly typed contract.

Kaleido simplifies the creation, discovery, and querying of reference data, lookup data, configuration datasets, and other business-defined records.

Rather than building custom endpoints, repositories, filters, sorting logic, and search capabilities for every dataset, Kaleido provides a common metadata-driven model that allows records to be exposed consistently across an application.

---

# Why Kaleido Exists

Most enterprise applications contain dozens or hundreds of datasets that share common characteristics:

- Clients
- Products
- Regions
- Lines of Business
- Provider Types
- Statuses
- Categories
- Code Sets
- Configuration Values
- Reference Data

These datasets nearly always require support for:

- Searching
- Filtering
- Sorting
- Paging
- Validation
- Metadata Discovery

Traditionally every dataset is implemented separately.

This often leads to:

- Duplicate code
- Different API patterns
- Inconsistent query behavior
- Custom filtering implementations
- Repeated documentation
- Increased maintenance costs

Kaleido was created to solve this problem by providing a single framework for exposing queryable business records.

The goal is simple:

> Define the metadata once. Let the framework handle the querying behavior.

---

# Design Goals

Kaleido was designed around several core principles.

## Metadata First

The framework is driven by metadata.

Consumers define:

- What fields exist
- What fields are searchable
- What fields are filterable
- What fields are sortable
- What named queries are available

Kaleido uses this metadata to validate, compile, and execute requests.

---

## Strongly Typed

Records are represented by strongly typed records.

```csharp
public sealed record ClientRecord
{
    public string ClientId { get; init; }

    public string Name { get; init; }

    public bool Active { get; init; }
}
```

The framework never requires consumers to expose dynamic objects or schema-less structures.

---

## Provider Agnostic

Kaleido does not assume where data comes from.

A record may be backed by:

- Entity Framework
- In-memory collections
- LINQ providers
- External APIs
- Custom repositories

The framework focuses on query orchestration rather than storage.

---

## Consumer Simplicity

Using a record should be simple.

Consumers should only need to:

1. Define a record
2. Define a source
3. Optionally define named queries
4. Register services

Everything else should be handled by the framework.

---

## Separation of Responsibilities

Kaleido favors small focused components.

Responsibilities are intentionally separated into:

- Metadata discovery
- Validation
- Query compilation
- Query orchestration
- Query execution
- Metadata exposure

This makes the framework easier to test, understand, and maintain.

---

# Architecture Overview

A typical request flows through the following pipeline.

```text
QueryRequest
      |
      v
Validation
      |
      v
Compilation
      |
      v
Source Creation
      |
      v
Named Query Application
      |
      v
Filter Application
      |
      v
Search Application
      |
      v
Sort Application
      |
      v
Paging Application
      |
      v
Execution
      |
      v
QueryResponse
```

Each stage is responsible for a single concern.

---

# Creating a Value Set

A record is represented by a strongly typed record and metadata attributes.

```csharp
[Record(
    Key = "client",
    Name = "Clients",
    Description = "Available clients")]
public sealed record ClientRecord
{
    [Filterable]
    [Sortable]
    public string ClientId { get; init; }

    [Filterable]
    [Searchable]
    [Sortable]
    public string Name { get; init; }

    [Filterable]
    public bool Active { get; init; }
}
```

This metadata describes the capabilities available to consumers.

---

# Creating a Source

Sources provide the data backing a record.

```csharp
public sealed class ClientRecordSource
    : IQueryableRecordSource<ClientRecord>
{
    private readonly ApplicationDbContext _db;

    public ClientRecordSource(
        ApplicationDbContext db)
    {
        _db = db;
    }

    public IQueryable<ClientRecord> CreateQuery(
        RecordExecutionContext context)
    {
        return _db.Clients;
    }
}
```

Sources define where data originates.

They do not define search, filtering, sorting, or paging behavior.

---

# Creating Named Queries

Named queries provide reusable business-specific filters.

```csharp
public sealed class ActiveClientsQuery
    : IQueryableRecordNamedQuery<ClientRecord>
{
    public string Name => "active-clients";

    public IQueryable<ClientRecord> Apply(
        IQueryable<ClientRecord> query,
        IReadOnlyDictionary<string, object?>? parameters)
    {
        return query.Where(x => x.Active);
    }
}
```

These are typically used to expose common business views.

---

# Registering Kaleido

Register the framework and scan an assembly for records.

```csharp
services.AddKaleido();

services.AddQueryableRecordsFromAssembly(
    typeof(ClientRecord).Assembly);
```

Once registered, all discovered records become available through the catalog.

---

# Querying a Value Set

Retrieve the catalog:

```csharp
public sealed class ClientController
{
    private readonly IRecordCatalog _catalog;

    public ClientController(
        IRecordCatalog catalog)
    {
        _catalog = catalog;
    }
}
```

Execute a query:

```csharp
var result = await _catalog.QueryAsync(
    "client",
    request);
```

---

# Discovering Metadata

Metadata is available at runtime.

```csharp
var all = _catalog.GetAll();
```

Retrieve a specific record:

```csharp
var client = _catalog.Get("client");
```

Metadata may be used to:

- Build dynamic user interfaces
- Generate documentation
- Describe query capabilities
- Support OpenAPI generation
- Validate requests

---

# Supported Features

Current framework capabilities include:

- Strongly Typed Value Sets
- Metadata Discovery
- Filtering
- Searching
- Sorting
- Paging
- Named Queries
- Metadata Exposure
- Provider Abstraction
- Assembly Scanning
- Query Validation
- Query Compilation
- Provider-Neutral Execution Model

---

# Project Structure

```text
Kaleido.Abstractions
    Public contracts
    Metadata models
    Query models

Kaleido.Core
    Metadata discovery
    Registry
    Catalog
    Validation
    Compilation
    Query orchestration

Kaleido.Queryable
    IQueryable implementation
    Expression generation
    LINQ execution

Kaleido.InMemory
    In-memory implementation
```

---

# Testing Philosophy

Kaleido favors isolated unit tests.

Every production class should have a corresponding test class.

Tests should validate:

- Responsibility
- Behavior
- Contract

Rather than testing the entire framework at once.

Dependencies should be mocked whenever possible to ensure each class is tested independently.

---

# Future Vision

Kaleido began as a framework for exposing reference data and lookup data.

The long-term vision is broader:

- Metadata-driven datasets
- Consistent querying
- Discoverable capabilities
- Provider independence
- Dynamic consumer experiences

The focus remains the same:

> Define the data once.
> Define the metadata once.
> Let the framework handle the rest.

---

# License

TBD