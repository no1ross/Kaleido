# Kaleido.Queryable

The Queryable capability owns query source and view declarations, registration, validation, compilation, execution and its own events and telemetry. It depends on the transport-free [`Kaleido` foundation](../../../Core/Kaleido/README.md) and the generic [`Kaleido.Eventing`](../../../Events/Kaleido.Eventing/README.md) writer/outbox seams; this placement is temporary pending #592. It does not own HTTP/gRPC parsing, Process execution or the central control registry.

Call `AddKaleido(configuration, options)` with an explicit `Assemblies` scan set, then call `AddEventing().AddQueryable()` on its builder to discover sources and views. The scan validates source/view identity, metadata, names, parameters and paging; an empty capability set is valid. `IQueryContextExecutor<TView>` is the extension point for provider-native asynchronous materialization. Queryable's runtime registrations are internal; transports adapt requests before invoking it.

`AddQueryableEventDispatch()` independently registers typed Queryable event delivery for a host that only relays an outbox and does not register query sources. A host with query sources gets this dispatch from `AddQueryable()`.

## Sources, views and execution

The source is the identity; the query context describes how it is queried. Discovery is interface-only: local `IQuerySource<T>` / `IQuerySourceAsync<T>` and delegated `IDelegatedQuerySource<…>` types need `[QuerySource]`; `IQueryViewSource<…>` / async views need `[QueryView]`. Each needs a nonempty display name and description. One type is exactly one capability; a source name is unique within a service and a view name is unique within its source. One query context may back several sources.

`QueryableService` dispatches by queried type to a local source, a view over its local source, or a delegated source; there is no fallback. It validates the consumer's request against the source context before execution, including delegated requests. A local source can always be queried directly. A delegated source returns its downstream `QueryResult<T>` unchanged; `QuerySourceKind` is unpublished internal metadata.

Paging is declared separately on a source's direct query or a view; it is never inherited. Both `DefaultSize` and `MaxSize` must be positive and ordered. Pageable local sources and views declare a sortable `DefaultSortField` for stable default ordering; delegated ordering belongs to the downstream source. Query parameters are checked with their DataAnnotations before execution. Case-insensitive in-memory matching uses invariant casing, while provider-backed expressions retain translatable `ToLower()`. A partial page establishes the total without an extra count; a full page may require counting. Transports convert their wire request body to the CLR `QueryBody` before invoking this runtime.

Queryable does not assemble the central registry; it projects metadata about the sources and views it owns for Registry to consume. For the repository model and behavior rules, see [Queryable architecture](../../../../docs/architecture/capabilities/queryable.md) and [Queryable authoring patterns](../../../../docs/architecture/capabilities/queryable-authoring.md).
