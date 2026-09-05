# Queryable Architecture

This document describes the current architecture of the Queryable subsystem in `src/Queryable`. It is intended for contributors working on the framework itself, not just consumers using it from feature code.

Queryable is a metadata-driven query framework for discoverable record search, filtering, sorting, and paging. It currently supports three execution models:

1. **Direct context**: query a context type directly
2. **Local view**: project a named view from a local `IQueryable` context
3. **Delegated view**: execute a named view through an async orchestration or service boundary

The code for Queryable is split into four projects:

- [`Abstractions`](./Abstractions)
- [`Queryable`](./Queryable)
- [`AspNetCore`](./AspNetCore)
- [`AspNetCore.Client`](./AspNetCore.Client)

---

## 1. Project structure

### [`Abstractions`](./Abstractions)
Contains:
- public attributes
- query request/result types
- metadata contracts
- public source/view interfaces
- public service interface
- observability constants

Examples:
- [`IQueryableService`](./Abstractions/IQueryableService.cs)
- [`QueryRequest`](./Abstractions/Query/QueryRequest.cs), `QueryResult`
- [`QueryContextAttribute`](./Abstractions/Attributes/QueryContextAttribute.cs), [`QueryViewAttribute`](./Abstractions/Attributes/QueryViewAttribute.cs)
- [`IQueryContextSource`](./Abstractions/Query/IQueryContextSource.cs)
- [`IQueryViewSource`](./Abstractions/Query/IQueryViewSource.cs)
- [`IDelegateQueryViewSource`](./Abstractions/Query/IDelegateQueryViewSource.cs)

### [`Queryable`](./Queryable)
Contains:
- assembly scanning and registration
- runtime registries
- validation
- query compilation
- query execution engines
- delegated execution engine
- query application and materialization
- observability/event publishing

Examples:
- [`QueryableServiceCollectionExtensions`](./Queryable/QueryableServiceCollectionExtensions.cs)
- [`QueryableService`](./Queryable/QueryableService.cs)
- [`QueryContextRegistry`](./Queryable/Records/QueryContextRegistry.cs)
- [`QueryViewRegistry`](./Queryable/Records/QueryViewRegistry.cs)
- [`DelegatedQueryViewRegistry`](./Queryable/Records/DelegatedQueryViewRegistry.cs)
- [`QueryContextEngine`](./Queryable/Query/QueryContextEngine.cs)
- [`DelegatedQueryViewEngine`](./Queryable/Query/DelegatedQueryViewEngine.cs)

### [`AspNetCore`](./AspNetCore)
Contains:
- route generation
- endpoint publication
- request normalization
- OpenAPI-related support

Examples:
- [`QueryableEndpointRouteBuilderExtensions`](./AspNetCore/QueryableEndpointRouteBuilderExtensions.cs)
- [`QueryableValueNormalizer`](./AspNetCore/QueryableValueNormalizer.cs)

### [`AspNetCore.Client`](./AspNetCore.Client)
Contains an HTTP client for consuming Queryable query endpoints published by `AspNetCore`. Registered via `.AddQueryableClient(name, baseUrl)` on the Kaleido builder.

Contains:
- `IKaleidoQueryableClient` — typed client for view queries and direct context queries
- `IKaleidoQueryableClientFactory` — factory resolved by name
- `KaleidoQueryableClient` — concrete HTTP client implementation
- `KaleidoQueryableClientException` — exception type wrapping error responses
- `KaleidoQueryableClientServiceCollectionExtensions` — builder extension

Examples:
- [`IKaleidoQueryableClient`](./AspNetCore.Client/IKaleidoQueryableClient.cs)
- [`IKaleidoQueryableClientFactory`](./AspNetCore.Client/IKaleidoQueryableClientFactory.cs)

---

## 2. Core concepts

### Query Context
A query context defines the searchable/filterable/sortable record metadata for a dataset. It is marked with [`QueryContextAttribute`](./Abstractions/Attributes/QueryContextAttribute.cs).

A context primarily defines:
- identity (`Name`, `Version`)
- display/documentation values
- execution kind (`Local`, `Direct`, `Delegated`)
- field metadata inferred from public properties and attributes

A context is represented at runtime by:
- `QueryContextRegistration`
- `QueryContextMetadata`

See [`QueryRegistration.cs`](./Abstractions/Metadata/QueryRegistration.cs).

### Query Context Kind
`QueryContextKind` currently has three values:

- `Local`
- `Direct`
- `Delegated`

These values are used to distinguish runtime behavior and discovery behavior. See [`QueryRegistration.cs`](./Abstractions/Metadata/QueryRegistration.cs).

### Query View
A query view is a named query surface over a context. It is marked with [`QueryViewAttribute`](./Abstractions/Attributes/QueryViewAttribute.cs).

A view primarily defines:
- identity (`Name`, `Version`)
- display/documentation values
- visibility
- optional paging metadata
- optional parameter metadata
- output contract type

Important distinction:
- query semantics are driven by query context fields and supported parameter metadata
- `TView` is the returned representation shape

That means a `TView` can legitimately represent a richer detail-style payload than the context itself, including nested objects or child collections. This is especially relevant for detail views where the returned shape may include related items such as notes.

There are two kinds of query views in the current architecture:

1. **Local query views**
   - implement [`IQueryViewSource<...>`](./Abstractions/Query/IQueryViewSource.cs)
   - return `IQueryable<TView>`
   - execute inside the normal local query pipeline

2. **Delegated query views**
   - implement [`IDelegateQueryViewSource<...>`](./Abstractions/Query/IDelegateQueryViewSource.cs)
   - return `Task<QueryResult<TView>>`
   - execute through a dedicated delegated engine

### Direct Context Query
A direct context query executes against the context type itself rather than a named view. This is only allowed when the context metadata kind is `Direct`.

### Query Request
A query request consists of:
- `QueryBody? Query`
- optional typed `ViewParameters`

The `QueryBody` contains:
- `SearchText`
- `Filter`
- `Sort`
- `Page`

See [`QueryRequest.cs`](./Abstractions/Query/QueryRequest.cs).

### Query Result
A query result returns:
- `TotalCount`
- `Offset`
- `PageSize`
- `Results`

This same result shape is used for:
- direct context queries
- local view queries
- delegated view queries

`Results` are returned as normal `TView` CLR objects. As a result, complex nested output on `TView` can work as a runtime serialization concern even when Queryable metadata does not fully model that nested structure.

### Registry
Queryable builds runtime registries that represent discovered contexts and views:

- [`IQueryContextRegistry`](./Abstractions/Query/IQueryContextRegistry.cs)
- [`IQueryViewRegistry`](./Abstractions/Query/IQueryViewRegistry.cs)
- [`IDelegatedQueryViewRegistry`](./Abstractions/Query/IDelegatedQueryViewRegistry.cs)

These registries drive both execution and endpoint publishing.

---

## 3. Public framework contracts

### [`IQueryableService`](./Abstractions/IQueryableService.cs)
This is the runtime entry point for query execution.

```csharp
Task<QueryResult<TView>> QueryAsync<TQueryView, TView>(IQueryRequest request, CancellationToken cancellationToken = default)
```

The important design point is that `TQueryView` is interpreted dynamically at runtime as one of:
- a delegated view
- a local view
- a direct query context

Implemented by [`QueryableService`](./Queryable/QueryableService.cs).

### [`IQueryContextSource<TContext>`](./Abstractions/Query/IQueryContextSource.cs)
This interface provides the local source `IQueryable<TContext>` for a normal context.

Use this when the data lives in the local query pipeline and can be queried via LINQ.

### [`IQueryViewSource<TContext, TView, TParameters>`](./Abstractions/Query/IQueryViewSource.cs)
This interface defines a local view.

It is intentionally synchronous:

- input: `IQueryable<TContext>`
- output: `IQueryable<TView>`

This interface is for query composition, not async orchestration.

### [`IDelegateQueryViewSource<TContext, TView, TParameters>`](./Abstractions/Query/IDelegateQueryViewSource.cs)
This interface defines a delegated view.

It is intentionally async:

- input: `IQueryRequest<TParameters>`
- output: `Task<QueryResult<TView>>`

Use this when the view must:
- call another service
- gather process-scoped/internal data
- orchestrate multiple steps
- return fully materialized results

---

## 4. Registration model

Queryable registration starts in [`QueryableServiceCollectionExtensions.AddQueryable()`](./Queryable/QueryableServiceCollectionExtensions.cs).

At a high level, registration does this:

1. ensure the Kaleido builder has assemblies registered
2. scan all candidate types in those assemblies
3. identify query contexts via [`QueryContextAttribute`](./Abstractions/Attributes/QueryContextAttribute.cs)
4. identify query views via [`QueryViewAttribute`](./Abstractions/Attributes/QueryViewAttribute.cs)
5. split contexts into delegated vs non-delegated
6. split views into delegated vs local
7. register local context sources
8. register local context engines
9. register local views
10. register delegated views
11. build registries
12. register framework services

### Assembly scanning
The framework scans discovered assemblies for non-abstract class types and then classifies them by attribute/interface shape.

See [`QueryableServiceCollectionExtensions`](./Queryable/QueryableServiceCollectionExtensions.cs).

### Context partitioning
Contexts are separated into:

- **delegated context types**: contexts with `Kind == Delegated`
- **local context types**: everything else

Only local/direct contexts go through normal source registration and normal context-engine registration.

### View partitioning
Views are separated by implemented interface:

- local views implement [`IQueryViewSource<,>`](./Abstractions/Query/IQueryViewSource.cs) or [`IQueryViewSource<,,>`](./Abstractions/Query/IQueryViewSource.cs)
- delegated views implement [`IDelegateQueryViewSource<,>`](./Abstractions/Query/IDelegateQueryViewSource.cs) or [`IDelegateQueryViewSource<,,>`](./Abstractions/Query/IDelegateQueryViewSource.cs)

This is an important architectural distinction. Delegated execution is now modeled through delegated views, not through a separate delegated context source path.

---

## 5. Registries and metadata

### [`IQueryContextRegistry`](./Abstractions/Query/IQueryContextRegistry.cs)
Holds normal context registrations.

A `QueryContextRegistration` contains:
- `ContextType`
- `SourceType`
- `QueryContextMetadata`

`QueryContextMetadata` includes:
- name
- description
- display name
- version
- source
- kind
- paging metadata
- field metadata

Field metadata is inferred from public properties and attributes like:
- [`FilterableAttribute`](./Abstractions/Attributes/FilterableAttribute.cs)
- [`SearchableAttribute`](./Abstractions/Attributes/SearchableAttribute.cs)
- [`SortableAttribute`](./Abstractions/Attributes/SortableAttribute.cs)
- `DescriptionAttribute`

Implementation: [`QueryContextRegistry`](./Queryable/Records/QueryContextRegistry.cs)

### [`IQueryViewRegistry`](./Abstractions/Query/IQueryViewRegistry.cs)
Holds local view registrations.

A `QueryViewRegistration` contains:
- `QueryViewType`
- `ViewType`
- `ViewParametersType`
- `QueryContextType`
- `QueryViewMetadata`

`QueryViewMetadata` includes:
- name
- version
- display name
- description
- visibility
- pageable metadata
- parameter metadata

Implementation: [`QueryViewRegistry`](./Queryable/Records/QueryViewRegistry.cs)

### [`IDelegatedQueryViewRegistry`](./Abstractions/Query/IDelegatedQueryViewRegistry.cs)
Holds delegated view registrations.

A `DelegatedQueryViewRegistration` contains:
- `QueryViewType`
- `ViewType`
- `ViewParametersType`
- `QueryContextType`
- `QueryMetadata`
- `ViewMetadata`

This is the key difference from the normal local view path: delegated registrations embed both context metadata and view metadata in one record.

That design allows delegated contexts to participate in metadata publishing without requiring a separate delegated context registry.

Implementation: [`DelegatedQueryViewRegistry`](./Queryable/Records/DelegatedQueryViewRegistry.cs)

---

## 6. Validation rules

### Context validation
Normal contexts are validated for:
- duplicate context names
- exactly one registered local source

This applies to local/direct contexts, not delegated execution.

Implementation: [`QueryContextRegistrationValidator`](./Queryable/Records/QueryContextRegistrationValidator.cs)

### Local view validation
Local views are validated for:
- duplicate view names
- implemented [`IQueryViewSource`](./Abstractions/Query/IQueryViewSource.cs)
- referenced context exists
- contract type is valid

Implementation: [`QueryViewRegistrationValidator`](./Queryable/Records/QueryViewRegistrationValidator.cs)

### Pageable view validation
If a view is pageable, it must define a `DefaultSortField`.

That default sort field must:
- exist on the associated context type
- be marked sortable

This applies to both local views and delegated views.

### Request validation
Requests are validated at runtime for:
- filter node structure
- filter field existence
- supported filter operators
- searchability
- sortability
- duplicate sort fields
- page size limits

This validation is driven by metadata, not by handwritten validation in each view.

Implementation: [`QueryRequestValidator`](./Queryable/Query/QueryRequestValidator.cs)

---

## 7. Query execution flow

The runtime entry point is [`QueryableService.QueryAsync<TQueryView, TView>()`](./Queryable/QueryableService.cs).

The dispatch order is:

1. delegated view registry lookup
2. local view registry lookup
3. direct context lookup

That order matters and should not be changed casually.

### 7.1 Delegated view path
If `TQueryView` is found in [`IDelegatedQueryViewRegistry`](./Abstractions/Query/IDelegatedQueryViewRegistry.cs):

1. validate registration matches requested types
2. create a DI scope
3. resolve `IDelegatedQueryViewEngine<TContext, TView>`
4. invoke delegated execution
5. return the materialized result

The delegated engine:
- resolves the delegated view instance from DI
- validates the request parameter type
- invokes the typed delegated source
- awaits the returned `QueryResult<TView>`
- publishes observability and events

This path is for orchestration, not `IQueryable` composition.

See [`DelegatedQueryViewEngine`](./Queryable/Query/DelegatedQueryViewEngine.cs).

### 7.2 Local view path
If `TQueryView` is found in [`IQueryViewRegistry`](./Abstractions/Query/IQueryViewRegistry.cs):

1. validate registration matches requested types
2. resolve its associated context registration
3. resolve `IQueryContextEngine<TContext, TView>`
4. execute the local query pipeline

The local [`QueryContextEngine`](./Queryable/Query/QueryContextEngine.cs) does:

1. validate request
2. create [`QueryExecutionContext`](./Abstractions/Query/QueryExecutionContext.cs)
3. compile request into internal query contracts
4. create the base context query
5. apply search, filter, and sort
6. invoke the local view source to create the view `IQueryable<TView>`
7. materialize count and items
8. return `QueryResult<TView>`

### 7.3 Direct context path
If no delegated view or local view registration exists, the framework attempts direct context execution:

1. resolve the context registration by type
2. ensure the context kind is `Direct`
3. ensure `TView == TContext`
4. execute `IQueryContextEngine<TContext, TContext>`

This path skips named view projection and queries the context type directly.

---

## 8. Query compilation and application

### Request compilation
[`QueryRequestCompiler`](./Queryable/Query/QueryRequestCompiler.cs) converts a request into:
- compiled filter tree
- compiled search definition
- compiled sort list
- compiled page settings

Paging defaults are resolved using:
- request page size if supplied
- metadata default page size if available
- fallback default of `50`

Page size is capped by the metadata max size.

### Query application
[`CompiledQueryApplier<TQueryContext>`](./Queryable/Runtime/CompiledQueryApplier.cs) translates compiled query contracts into LINQ expressions.

It applies:
- filter
- search
- sort

Supported filter/search behavior includes:
- equality / inequality
- string contains / starts with / ends with
- greater-than / less-than
- in / not-in
- between / not-between
- is-null / is-not-null
- is-true / is-false

String comparisons are normalized case-insensitively through lower-casing in the generated expressions.

### Materialization
[`QueryContextExecutor<TView>`](./Queryable/Runtime/QueryContextExecutor.cs) currently uses synchronous LINQ operations:
- `Count()`
- `ToList()`

wrapped in task-returning methods.

That means the API surface is async-shaped, but the default local executor is not yet provider-native async.

Contributors should be careful not to document this as full async query-provider materialization unless that implementation changes.

---

## 9. ASP.NET Core transport layer

Queryable’s ASP.NET Core integration is added with:

- [`AddQueryableAspNetCore(...)`](./AspNetCore/QueryableAspNetCoreServiceCollectionExtensions.cs)
- [`MapQueryable()`](./AspNetCore/QueryableEndpointRouteBuilderExtensions.cs)

### [`AddQueryableAspNetCore`](./AspNetCore/QueryableAspNetCoreServiceCollectionExtensions.cs)
This:
- verifies `AddQueryable()` has already been called
- stores `QueryableRouteOptions`
- adds routing

### [`MapQueryable`](./AspNetCore/QueryableEndpointRouteBuilderExtensions.cs)
This publishes the Queryable HTTP surface.

At a high level it creates a route group under:

- `/queryable`
- or `/{routePrefix}/queryable`

depending on `QueryableRouteOptions`.

It then publishes:

1. **Catalog endpoint**
   - `GET /queryable`
   - returns summary records for discoverable contexts

2. **Registry endpoint**
   - `GET /queryable/registry`
   - returns full context/view metadata

3. **Per-context metadata endpoints**
   - `GET /queryable/{context}/{metadataRoute}`

4. **Direct context query endpoints**
   - `POST /queryable/{context}/{queryRoute}`
   - only for `Direct` contexts

5. **Local view query endpoints**
   - `POST /queryable/{context}/{view}/{queryRoute}`

6. **Delegated view query endpoints**
   - `POST /queryable/{context}/{view}/{queryRoute}`

### Request contract
HTTP requests use:
- [`QueryApiRequest`](./AspNetCore/Contracts/QueryApiRequest.cs)
- `QueryApiRequest<TParameters>`

This mirrors the internal [`QueryRequest`](./Abstractions/Query/QueryRequest.cs) shape.

### Value normalization
The ASP.NET Core layer normalizes incoming filter values to the real field types defined in metadata before execution.

That is especially important because JSON payload values often arrive as untyped or loosely typed values.

Implementation: [`QueryableValueNormalizer`](./AspNetCore/QueryableValueNormalizer.cs)

### Error behavior
Validation exceptions are converted into `400 Bad Request` responses with [`QueryErrorResponse`](./AspNetCore/Contracts/QueryErrorResponse.cs).

### HTTP client (`AspNetCore.Client`)
`AspNetCore.Client` provides `IKaleidoQueryableClientFactory` for consuming remote Queryable endpoints.

Register a named client on the Kaleido builder:

```csharp
builder.Services.AddKaleido()
    .AddQueryableClient("MemberService", "https://member-service-host")
    .AddQueryableClient("Radiology", "https://radiology-host", routePrefix: "radiology");
```

The optional `routePrefix` must match the remote server's `QueryableRouteOptions.RoutePrefix`. When omitted, no prefix is used.

Resolve per-request via `IKaleidoQueryableClientFactory`:

```csharp
// Get the full registry (lazily fetched and cached per client instance)
var registry = await factory
    .GetClient("MemberService")
    .GetRegistryAsync(cancellationToken);

// Get metadata for a single context
var metadata = await factory
    .GetClient("MemberService")
    .GetContextMetadataAsync("Members", cancellationToken);

// View query with typed parameters
var result = await factory
    .GetClient("MemberService")
    .QueryViewAsync<MemberDetailsParameters, MemberDetailsView>(
        "Members", "MemberDetails", request, cancellationToken);

// Direct context query
var result = await factory
    .GetClient("CodeSet")
    .QueryContextAsync<ProcedureCodeView>(
        "ProcedureCodes", request, cancellationToken);
```

`GetRegistryAsync` returns `IReadOnlyList<QueryableRecordResponse>` and uses the same cached fetch that underpins URL resolution for query calls. `GetContextMetadataAsync` resolves the context's `MetadataUrl` from the registry and fetches it directly.

Query methods return `QueryResult<TView>` with `TotalCount`, `Offset`, `PageSize`, and `Results`.

`KaleidoQueryableClient` forwards Kaleido correlation headers automatically and throws `KaleidoQueryableClientException` on non-success responses.

---

## 10. Discovery and metadata publishing

The discovery model is context-centric, even when execution is delegated through views.

### Catalog endpoint
The catalog returns [`QueryableRecordSummary`](./AspNetCore/Contracts/QueryableRecordSummary.cs) values.

For normal contexts, summary records come from [`IQueryContextRegistry`](./Abstractions/Query/IQueryContextRegistry.cs).

For delegated contexts, summary records are projected from the delegated view registry using the embedded `QueryMetadata`.

### Full registry endpoint
The registry returns [`QueryableRecordResponse`](./AspNetCore/Contracts/QueryableRecordResponse.cs).

A `QueryableRecordResponse` contains:
- context identity and descriptions
- `MetadataUrl`
- optional direct `QueryUrl`
- context field metadata
- public views
- view URLs
- view parameter metadata
- view output fields

Current output metadata is intentionally shallow:
- registry output describes the top-level `TView` properties exposed by a view
- complex nested output members currently fall back to a datatype of `object`
- nested object graphs are not yet recursively described in published output metadata

This is a metadata limitation, not necessarily a runtime result limitation.

Future enhancement:
- add recursive/nested output metadata for complex `TView` members so detail views can be fully self-describing for clients, internal services, and orchestrators

### Direct query URL behavior
The presence of `QueryUrl` on the context record depends on execution kind:

- **Direct contexts** include a direct `QueryUrl`
- **Delegated contexts** do not include a direct `QueryUrl`

This is a very important current rule.

Delegated contexts still have context metadata and still appear in discovery, but they are executed through delegated views rather than direct context query endpoints.

### View visibility
Only public views are included in published view metadata.

---

## 11. Delegated view model

The delegated view model is the main architectural extension point for async orchestration.

### Purpose
Use delegated views when the framework consumer should only provide minimal, user-facing input, while the backend must derive additional internal criteria.

Typical reasons:
- process-scoped enrichment
- service-to-service translation
- hiding internal parameters from the UI
- returning data from another bounded context or service

### Design intent
The delegated context still defines:
- the context identity exposed in discovery
- filter/search/sort metadata
- field definitions used to validate incoming query semantics

The delegated view defines:
- the executable query surface
- the output contract
- any additional view parameters

The delegated engine defines:
- async execution
- parameter compatibility enforcement
- result propagation
- observability/event handling

### Why this model exists
This keeps UI contracts simple.

Instead of making the UI know internal dimensions like:
- LOB
- employer group
- plan-derived values
- downstream service-specific fields

the UI can send only:
- user query semantics
- small view parameters such as `ProcessId`

The delegated view implementation can then:
1. derive internal context
2. call downstream services
3. return normalized paged results

That is the main reason delegated views should remain a distinct concept from local query views.

---

## 12. Observability

Queryable includes observability under the [`Kaleido.Queryable`](./Abstractions/Observability/QueryableTelemetry.cs) activity source and meter.

### Activity source and meter
Both currently use:
- `Kaleido.Queryable`

### Execution modes
Queryable tracks:
- `LocalView`
- `DirectContext`
- `DelegatedContext`

Note that `DelegatedContext` is still the current telemetry name even though the runtime model is now delegated-view-centric.

### Metrics
Queryable records:
- execution count
- validation failure count
- execution failure count
- total count histogram
- returned count histogram
- page size histogram
- page offset histogram

### Child activities
Execution observations can create child scopes for:
- source
- view
- materialization
- delegate

This gives a clean place to instrument pipeline stages.

Implementation: [`QueryableObservability`](./Queryable/Observability/QueryableObservability.cs)

---

## 13. Developer guidance

### When to use a direct context
Use a direct context when:
- the context type itself is the desired result shape
- there is no need for a named view abstraction
- you want direct context query endpoints published

### When to use a local view
Use a local view when:
- the dataset comes from a local queryable source
- the result can be expressed as a LINQ projection or shaping step
- the framework should own filtering, sorting, paging, and materialization

Do **not** use a local view when the view must do async orchestration before it can produce results.

### When to use a delegated view
Use a delegated view when:
- the view must call another service
- the backend must enrich the request with hidden/internal data
- the view must do async work before results exist
- the UI should stay unaware of internal backend query details

### Async guidance
At the moment, local query views should stay synchronous.

[`IQueryViewSource`](./Abstractions/Query/IQueryViewSource.cs) is not the async boundary.

The async boundary for orchestration is [`IDelegateQueryViewSource`](./Abstractions/Query/IDelegateQueryViewSource.cs).

This is an important design rule and should not be blurred casually.

---

## 14. Contributor cautions

### 1. Dispatch order is significant
[`QueryableService`](./Queryable/QueryableService.cs) checks delegated views before local views before direct contexts.

A change here would alter framework semantics.

### 2. Discovery is context-centric even for delegated execution
Do not assume delegated execution means “no context”.

Delegated registrations still carry context metadata and publish context records.

### 3. Public abstractions ripple widely
Changes in [`Abstractions`](./Abstractions) usually affect:
- runtime
- endpoint publishing
- tests
- samples

Prefer additive changes where possible.

### 4. Paging metadata must stay aligned with context/view metadata
Especially for delegated views, the context metadata and view metadata need to stay consistent with the execution path, otherwise discovery and runtime behavior drift apart.

### 5. Do not overstate async local execution
The current default materializer is sync-backed.

---

## 15. Glossary

### Context
A queryable record definition with searchable/filterable/sortable metadata.

### Context Kind
The runtime/discovery classification of a context:
- Local
- Direct
- Delegated

### Direct Context
A context that can be executed directly without a named view.

### Local View
A named view that composes `IQueryable<TView>` from a local context query.

### Delegated View
A named view that performs async orchestration and returns a materialized `QueryResult<TView>`.

### Query Metadata
The metadata describing a context and its fields.

### View Metadata
The metadata describing a view, its visibility, paging, and parameters.

### Registry
The runtime-discovered collection of contexts or views.

### Query Body
The portion of a request describing search/filter/sort/page instructions.

### View Parameters
Typed parameters specific to a named view, separate from generic query semantics.

### Materialization
The step where an `IQueryable` is counted, paged, and converted into returned records.

---

## 16. Areas worth documenting further later

These are not necessarily defects, but they deserve explicit explanation or later cleanup:

1. telemetry still uses the term `DelegatedContext` even though the newer runtime model is delegated-view-centric
2. local view validation and delegated view validation are not shaped identically
3. [`QueryContextExecutor<TView>`](./Queryable/Runtime/QueryContextExecutor.cs) is async-shaped but sync-backed
4. context-centric discovery can be slightly counterintuitive for delegated-only execution paths

---

## 17. Practical summary

If you are adding new functionality to Queryable, use this rule of thumb:

- if the data is locally queryable and the result is a projection, use a **local context + local view**
- if the context itself is the result, use a **direct context**
- if the backend must orchestrate async work or hide internal service logic from the caller, use a **delegated view**

That is the current architectural center of gravity for the subsystem.
