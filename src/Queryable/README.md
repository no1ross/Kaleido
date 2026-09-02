# Queryable

Queryable is Kaleido’s metadata-driven query framework for discoverable record search, filtering, sorting, and paging.

It is organized into three projects:

- [`Abstractions`](./Abstractions/README.md) — public contracts, metadata, attributes, request/result types, and source/view interfaces
- [`Queryable`](./Queryable/README.md) — runtime registration, validation, dispatch, execution, and observability
- [`AspNetCore`](./AspNetCore/README.md) — HTTP integration, endpoint publishing, request normalization, and transport contracts

For the full subsystem model, see:
- [`ARCHITECTURE.md`](./ARCHITECTURE.md)
- [`AGENTS.md`](./AGENTS.md)

## Execution model

Queryable currently supports three execution lanes:

1. **Direct context**
   - query the context type itself
   - valid only for contexts marked `Direct`

2. **Local view**
   - named view over a local `IQueryable<TContext>`
   - implemented with `IQueryViewSource`

3. **Delegated view**
   - named async/orchestrated view
   - implemented with `IDelegateQueryViewSource`

Use:
- **direct context** when the context itself is the result shape
- **local view** when the result is a local projection over a queryable source
- **delegated view** when the backend must do async orchestration or hide internal downstream query logic from the caller

## Getting started

At a high level, Queryable setup looks like:

1. register one or more assemblies with the Kaleido builder
2. call `AddQueryable()`
3. optionally call `AddQueryableAspNetCore(...)`
4. map endpoints with `MapQueryable()`

The runtime will:
- discover contexts and views
- build metadata registries
- validate requests
- execute direct, local-view, or delegated-view queries
- publish discovery metadata through ASP.NET Core if enabled

## Where to look

- Start with [`Abstractions/README.md`](./Abstractions/README.md) for public contracts
- Read [`Queryable/README.md`](./Queryable/README.md) for runtime behavior
- Read [`AspNetCore/README.md`](./AspNetCore/README.md) for HTTP/discovery behavior
- Read [`ARCHITECTURE.md`](./ARCHITECTURE.md) for the full architecture
- Read [`AGENTS.md`](./AGENTS.md) before making framework changes