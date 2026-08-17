# Kaleido

Kaleido is a framework for exposing business capabilities through consistent, discoverable contracts.

Rather than building custom APIs, custom validation, custom documentation, and custom consumer experiences for every feature, Kaleido provides standardized models for exposing both business information and business actions.

Kaleido is built around three principles:

- Business capabilities should be discoverable.
- Contracts should be explicit.
- Consumers should understand business capabilities rather than implementation details.

---

## Why Kaleido?

Most applications repeatedly solve the same problems:

- Defining request and response contracts
- Implementing search, filtering, sorting, and paging
- Validating requests
- Building documentation
- Creating consumer integration guidance
- Exposing metadata for tooling and user experiences

As applications grow, these implementations often become inconsistent and difficult for consumers to discover.

Kaleido provides a common model for exposing business information and business actions while allowing developers to focus on business functionality rather than infrastructure.

---

## Core Concepts

Kaleido separates business capabilities into two complementary models.

### Queryable

Queryable exposes business information.

Examples:

- Products
- Customers
- Orders
- Prior Authorizations

Queryable provides:

- Search
- Filtering
- Sorting
- Paging
- Validation
- Metadata
- Discoverability

Queryable answers the question:

> What information does the business know?

### Process

Process exposes business actions.

Examples:

- Add Item To Cart
- Submit Order
- Approve Prior Authorization
- Request Additional Information

Process provides:

- Execution Contracts
- Validation
- Step Dependencies
- Availability Rules
- Metadata
- Discoverability

Process answers the question:

> What can the business do?

---

## Why Separate Queryable and Process?

Business information and business actions solve different problems.

For example:

```text
Find Orders
```

is different from:

```text
Submit Order
```

Likewise:

```text
Find Prior Authorizations
```

is different from:

```text
Approve Prior Authorization
```

Queryable focuses on retrieving information.

Process focuses on performing actions.

Keeping these concerns separate allows each capability to evolve independently while maintaining clear responsibilities.

---

## Discoverability Through Metadata

A core objective of Kaleido is reducing tribal knowledge.

Consumers should not need to inspect source code, reverse engineer APIs, or search through documentation to understand how a capability works.

Kaleido exposes metadata describing:

- Available Queryables
- Available Processes
- Input Requirements
- Validation Rules
- Search Capabilities
- Filter Capabilities
- Sort Capabilities
- Execution Contracts

This metadata can be consumed by:

- Applications
- UI Components
- Documentation
- Tooling
- Validation Services

Metadata is intended to guide consumers rather than generate application behavior automatically.

---

## Getting Started

Register Kaleido capabilities during application startup.

```csharp
builder.Services
    .AddKaleido()
        .AddAssembly(typeof(Program).Assembly)
        .AddAssembly(typeof(AddItemToCartStep).Assembly)
        .AddAssembly(typeof(ProductCatalogQueryContext).Assembly)
        .AddParticipant()
            .AddParticipantAspNetCore()
            .UseSqliteProcessContextStore(
                "Data Source=kaleido-sample-process.sqlite")
        .AddQueryable()
            .AddQueryableAspNetCore();
```

---

## Creating a Queryable

A Queryable Context describes business information and its capabilities.

```csharp
[QueryContext(
    Name = "products",
    DisplayName = "Products",
    Version = "1.0.0",
    Source = "E-Commerce Catalog")]
public sealed class ProductCatalogQueryContext
{
    [Key]
    public Guid ProductId { get; init; }

    [Filterable(
        FilterOperator.Equals,
        FilterOperator.NotEquals,
        FilterOperator.Contains,
        FilterOperator.StartsWith)]
    [Searchable(
        Priority = 1,
        MatchMode = MatchMode.Contains)]
    [Sortable]
    public string ProductName { get; init; }
        = string.Empty;
}
```

Views expose specific ways of retrieving that information.

```csharp
[QueryView(
    Name = "product-list",
    DisplayName = "Product List",
    Version = "1.0.0",
    Description = "Product catalog results.",
    DefaultSortField =
        nameof(ProductCatalogQueryContext.ProductName))]
[Pageable(
    DefaultSize = 25,
    MaxSize = 250)]
internal sealed class ProductListQueryViewSource
    : IQueryViewSource<
        ProductCatalogQueryContext,
        ProductCatalogView>
{
}
```

---

## Creating a Process

A Process Step represents a business action.

```csharp
[ProcessStep(
    Name = "process-cart",
    DisplayName = "Shopping Carts - Process Cart",
    Version = "1.0",
    Description =
        "Processes the shopping cart and starts an order.")]
[AvailableUntil(typeof(SubmitOrderStep))]
[AvailableAfter(typeof(AddItemToCartStep))]
[Repeatable]
public sealed record ProcessCartStep;
```

Business behavior is implemented through handlers.

```csharp
internal sealed class ProcessCartHandler(
    ECommerceDbContext dbContext)
    : IProcessStepHandler<ProcessCartStep>
{
}
```

---

## What Kaleido Provides

Kaleido is designed to help developers expose business capabilities consistently.

Out of the box Kaleido provides:

- Queryable Contracts
- Process Contracts
- Metadata Discovery
- Search
- Filtering
- Sorting
- Paging
- Validation
- Consumer Guidance
- Registry Metadata

This allows teams to focus on business functionality instead of repeatedly building supporting infrastructure.

---

## Documentation

Additional documentation is available within the `/docs` folder.

Suggested starting points:

- Architecture Overview
- Queryable Overview
- Process Overview
- Creating a Queryable
- Creating a Process
- Metadata
- Validation