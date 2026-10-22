# Kaleido canonical patterns

Single source of truth for the conventions a consumer (or code generator) must
follow. Each pattern links the enforcing analyzer where one exists — see
[`ANALYZERS.md`](./ANALYZERS.md).

## Vocabulary

- **Processor** — the unit that owns and executes steps (runtime/DI identity).
- **Process** — one executing instance of a processor (wire/transport domain).
- **Step / Handler** — an `IProcessStep` record (described by `[ProcessStep]`) + `IProcessStepHandler<T>` pair.

## Step + handler

```csharp
[ProcessStep(
    Version = "1.0",
    DisplayName = "Capture service",
    Description = "Captures the requested service for the authorization.")]
[AvailableAfter<CaptureMemberStep>]
public sealed record CaptureServiceStep : IProcessStep { /* input fields */ }

public sealed class CaptureServiceHandler(IHistoryClient history)
    : IProcessStepHandler<CaptureServiceStep>
{
    public async Task<ProcessStepHandlerResult> ExecuteAsync(
        CaptureServiceStep step, ProcessStepContext context, CancellationToken ct)
    {
        // ... domain work ...
        return ProcessStepHandlerResult.Success<CaptureProviderStep>();
    }
}
```

Rules:
- **`IProcessStep` is the step's identity**; `[ProcessStep]` only describes it. Every
  `IProcessStep` type needs `[ProcessStep]` (KAL2008), and `[ProcessStep]` is only
  valid on `IProcessStep` types (KAL2007).
- **The step name is the type name** (`Type.Name`, unmodified) — in the registry, on
  the wire, in persisted state, and in events. Renaming the type renames the step.
- `[ProcessStep]` declares non-empty `Version`, `DisplayName`, and `Description`
  (KAL2001). `DisplayName`/`Description` are what UIs and AI agents read — write
  them for that audience.
- **Required inputs are `required` members.** Inputs are validated with DataAnnotations, and
  `[Required]` cannot see an omitted value type (it is already its default). Declare required
  value-type inputs with the C# keyword too (KAL2018):
  `[Required] public required Guid MemberId { get; init; }`. Callers must then supply it: the
  compiler checks in-process callers, and a request body that leaves it out is answered 400
  naming it. The handler reads a plain `Guid`.
- **The next step is named by type**: `Success<TNext>()`, or with a typed response
  `ProcessStepHandlerResult.Success(response).Next<TNext>()` (the response type is inferred);
  `Success()` / `Success(response)` let the process rules decide. Cross-processor routing uses
  `HandOff(targetProcessorName)`. `Reject`, `HandOff` and untyped results are returned as they
  are from a typed handler (they convert to `ProcessStepHandlerResult<T>`).
- **Saying no** ([ADR 0008](./decisions/0008-rejected-outcome-and-retryable-exceptions.md)):
  `Reject(messages)` for input the caller can correct, or a call that can be retried
  (unknown code, ineligible date, downstream unavailable). The step is not completed and the
  process is left exactly as it was. Reject before writing anything, or make the handler safe
  to run again. `Failure(messages)` only when the request cannot proceed: it ends the process.
  An unexpected exception leaves the process as it was, so the step can be retried.
- Relationships are typed: `[DependsOn<T>]`, `[AvailableAfter<T>]`,
  `[AvailableUntil<T>]`, constrained to `IProcessStep`. What they mean:
  - `[DependsOn<T>]`: the step runs only after `T` completed, in the process history or
    earlier in the same request (`T` is then ordered first). It is not offered as available
    until then. Several: all of them.
  - `[AvailableAfter<T>]`: the step becomes available once `T` has completed (at any point
    in the process, not only right after it) and stays available. Several: all of them.
  - `[AvailableUntil<T>]`: the step stops being available once `T` completes. Several: the
    first one completed closes it.
  - `[Repeatable]`: without it, a completed step is not offered again; submitting it again is
    skipped (`pro_already_processed`), not an error.
  - A step with no `[DependsOn]` and no `[AvailableAfter]` (and not an information step) is
    an **initial step**: available when a process starts.
  - At startup: a relationship to itself, a circular `[DependsOn]` graph, or a relationship to a
    step the processor does not register fails with `pro_invalid_registration`.
- Every step needs exactly one handler (checked at startup: `pro_missing_handler`); the handler must not swallow
  `OperationCanceledException` in `catch (Exception)` — filter it or let the
  executor own cancellation (KAL2004).
- **Describe inputs** with `[Description("…")]` or `[Display(Description = "…")]`
  (KAL2014); `[Display(Name, Prompt)]` are optional hints. The registry publishes
  them — UIs and AI agents use them to know what to ask for.
- **Declare what only you know about the step's effects** ([ADR 0015](./decisions/0015-declared-step-behavior.md)) -
  clients and AI agents read it from the registry, and the MCP adapter maps it onto tool hints:
  - `Destructive = true`: irreversible or loss-causing (cancel, delete, withdraw, submit, pay).
  - `External = true`: changes something outside the process state - another system's data, an
    email or message, an order, a payment - wherever that system lives. Reading other systems and
    handing off to another Kaleido processor do not count.
  - `RequiresConfirmation = true`: the user should confirm before a client calls it (a hint).
  - Nothing declared means an ordinary step that records into the process. Idempotence is derived:
    a step without `[Repeatable]` runs once per process.
- **Classify sensitive fields** with your own `DataClassificationAttribute` subclasses (`[PHI]`,
  `[PII]`): the registry publishes them (`classifications`), MCP marks them "Sensitive", and event
  payloads redact them ([ADR 0013](./decisions/0013-event-payloads-classified-and-redacted.md)).
- **Only next steps run:** a request may start with the pending required step or,
  when nothing is required, a step available under the process rules
  (`pro_step_not_available` otherwise).

## Lookup inputs (ids the user cannot type)

A step input that takes an id from a query result declares where it comes from with `[Lookup]`:

```csharp
public sealed record CaptureMemberStep : IProcessStep
{
    [Lookup(Service = "member", Source = "Member", View = "MemberSearch", Field = "MemberId",
        Display = ["DisplayName", "DateOfBirth", "MemberNumber", "PlanName"])]
    public required Guid MemberId { get; init; }

    [Lookup(Service = "member", Source = "Member", View = "MemberSearch", Field = "MemberEnrollmentId")]
    public required Guid MemberEnrollmentId { get; init; }
}
```

Rules:
- `Service` is the publishing service's route prefix; `Source`, `View` (optional) and `Field` are
  published names. The query may live on another service, so the names are checked where the
  registries meet (the MCP adapter logs a lookup it cannot resolve), not at startup.
- Inputs with the same `Group` (by default one group per query) are filled from the **same**
  result. Name groups when a step looks up the same query twice (a requesting and a servicing
  provider). `Display` names the fields that identify a result to a person; declaring it on one
  input of the group is enough.
- Kaleido publishes the link in the registry and never picks the record: a UI renders a lookup,
  and the MCP adapter guides the agent or runs a guided lookup with forms
  ([MCP README](../src/Kaleido.Http.Mcp/README.md#lookups)).
- A lookup guides; it does not enforce. The step's handler still checks what it receives (for
  example that the enrollment belongs to the member).

## Information step (runtime-decided questions)

Known inputs are step properties. Questions decided at runtime (by rules engines,
configuration, clinical systems…) are an **information request**, answered by an
information step. Kaleido owns the schema, never the content.

```csharp
// The step whose handler learns there are questions requires the information step with them.
return ProcessStepHandlerResult.RequireInformation<CaptureOutOfNetworkResponseStep>(
    new InformationRequest
    {
        InformationRequestId = "out-of-network-attestation",   // required, set by you
        Title = "Out-of-network attestation",
        Items =
        [
            new InformationItem
            {
                Id = "reason",
                Text = "Why is an out-of-network provider needed?",
                Type = InformationItemType.Choice,
                Options = [new InformationOption { Value = "no-in-network", Display = "No in-network provider available" }]
            }
        ]
    });

// The information step: its payload is the answers; nothing else (KAL2012).
[ProcessStep(Version = "1.0", DisplayName = "Out-of-network responses", Description = "Answers the out-of-network attestation.")]
public sealed record CaptureOutOfNetworkResponseStep : IInformationStep
{
    public required string InformationRequestId { get; init; }
    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}
```

Rules:
- Require an information step only with `RequireInformation<TNext>(request)` — never
  `Success<TNext>()` (KAL2013).
- Kaleido validates the answers structurally against the pending request before the
  handler runs; the handler translates them into domain values.
- To ask again (adaptive rounds), make the information step `[Repeatable]` and have its
  handler require itself with the next request.
- Kaleido stores only the pending request; recording questions and answers over time is
  yours (`StepCompleted` carries each step's payload).

Full model: [`INFORMATION_REQUESTS.md`](./INFORMATION_REQUESTS.md).

## Process authoring

```csharp
services.AddKaleido(builder.Configuration, o =>
{
    o.ServiceName = "intake";                       // lowercase letters, digits, '-' and '_' (KAL2005)
    // AuthorizationMode: Authenticated unless set - add host authentication, or set None explicitly (ADR 0021)
    o.Assemblies = [typeof(Program).Assembly];      // explicit — never omit (KAL2006)
})
    .AddHttp();
```

`AddKaleido()` auto-registers Processor + Queryable runtimes and binds
`Kaleido:*` config into a `KaleidoServiceOptions` snapshot. Set options via the
`KaleidoServiceOptionsBuilder` configure lambda; `Build()` validates and
normalizes them (service name, assemblies, roles).

**Options are startup inputs.** Every Kaleido options object is configured while
the host composes its services: the `AddKaleido` builder, `AddHttp`'s
`KaleidoHttpOptions`, `MapKaleidoHttp`'s `KaleidoHttpMapOptions`, the
`Kaleido:Clients` options, the `AddMcp` builder and
`KaleidoOpenTelemetryOptions`. Set them in configuration or in the configure
callback, then leave them alone: changing an options object after the `Add…`/
`Map…` call is unsupported, and what it changes is undefined. Kaleido does not
copy options defensively (ADR 0018).

### Kaleido's HTTP behavior is scoped to Kaleido's endpoints

`AddHttp()` adds nothing to the host's middleware pipeline. Error answers and
correlation are endpoint filters on Kaleido's route groups: the exception filter
first (so it also answers a malformed correlation header), then the correlation
filter (headers, trace tags, header echo; it runs after the host's
authentication), then the JSON and caller filters. The host's own endpoints get
no `X-Kaleido-*` headers and no Kaleido error bodies; a host endpoint that calls
a Kaleido client sends no inbound request id, so the downstream service
generates one. All filters are internal; hosts never register them (#295).

### Changing a step, source or view

`Version` is a label for audit and clients: Kaleido publishes and records it but never routes on it
([ADR 0026](decisions/0026-versioning-concept.md)). A capability has one live version, addressed by
its name.

- **Compatible** (keep the name, bump the version): wording, a new optional input, a new output field.
- **Breaking** (publish a new capability, e.g. `CaptureMemberStepV2`): removing, renaming or
  requiring an input; changing a type or a value's meaning; relationships that would strand stored
  processes.
- **In-flight processes continue on the deployed definition.** Completed steps keep the version they
  ran with. To retire a step, close it first (`AvailableUntil`), and remove it in a later deployment,
  once no stored process can require it.

## Transport-neutral response semantics

Design the meaning of response fields before choosing JSON, gRPC, or another
transport. Keep handler-authored `BusinessMessages` separate from framework
`FrameworkMessages`: consumers may present the former and treat the latter as
diagnostics. A transport option may include framework messages, but when it is
off the collection is empty, not absent by a JSON-only rule. Repeated fields
have the same empty-collection meaning in a gRPC consumer. Adapters decide
which messages to populate; serializer-specific omission does not define the
contract or belong in core runtime types.

## Concurrency, idempotency and retries

What Kaleido guarantees, and what stays with the step handler:

- **Concurrent calls cannot overwrite a process.** Every save is conditional on the version the state was loaded with; the second of two concurrent calls gets **409** `pro_process_conflict` and nothing of it is saved. Consumers read the state again and decide; they never repeat the call blindly.
- **Retried calls do not run a step twice.** An execute request with an `Idempotency-Key` (the Kaleido clients send one with every execute call) is answered from the stored result when it arrives again. Third-party callers send their own key per logical call.
- **Client calls are resilient by default:** timeouts, retries of safe requests (reads and requests with an idempotency key), a circuit breaker - configured under `Kaleido:Resilience`, per client in `Kaleido:Clients:{name}:Resilience` ([`Kaleido.Http.Client`](../src/Kaleido.Http.Client/README.md#resilience)).
- **A handler's own side effects are the handler's concern.** On a 409 the handler has already run; only the process state was not saved. A handler that writes to another system or calls one should make that write idempotent (an upsert, a business key, the process id as a reference) so running the step again for the same process is harmless - Kaleido cannot make a handler's external write atomic with its state.
- **Events are handed to the publisher at least once** with Kaleido's built-in outbox (the default). Every event goes to the outbox (a step's `StepCompleted` and a new process's `ProcessCreated` in the same transaction as the state when the persistence provides one, e.g. SQLite), and the event relay of each instance publishes it, retrying with backoff until it succeeds - configured under `Kaleido:Events` (`BatchSize`, `MaxConcurrentPublishes`, `Resilience`). A publisher never slows down or fails a request. Consumers drop duplicates by `IKaleidoEvent.EventId` ([ADR 0012](decisions/0012-events-at-least-once-through-the-store.md)). A host that already runs an outbox plugs it in instead (`AddOutbox<T>()`); `AddDirectPublication()` publishes once after the commit, without retries ([ADR 0023](decisions/0023-outbox-integration.md)).
- **Event payloads are complete; sensitive values are redacted before the outbox.** Mark PHI/PII properties of your view, source and step types with your own `DataClassificationAttribute` subclasses (`[PHI]`, `[PII]`; `[property: PHI]` on positional records) and choose a redactor per classification for events with `AddEventClassificationRedaction(r => ...)` (the standard `IRedactionBuilder` API): erase, HMAC, keep (`NullRedactor`), or your own encrypting redactor. Without it, classified values are erased. Logs are configured separately with `AddRedaction`, so the two channels can differ (e.g. logs: no PHI or PII; events: PII kept) ([ADR 0025](decisions/0025-event-redaction-policy.md)). For anything else (information-request answers, per-view rules) register an `IKaleidoEventRedactor` (`AddEventRedactor<T>()`). Responses to the caller are never redacted ([ADR 0013](decisions/0013-event-payloads-classified-and-redacted.md)).
- **Custom stores** implement the conditional save (state only), the idempotent-result methods, and either the built-in outbox's storage (`IKaleidoOutboxStore`, with leased claims) or their own outbox (`IKaleidoOutbox`), plus `IKaleidoTransactions` to keep a state change and its event atomic on one database ([store contract](../src/Kaleido.Provider.SQLite/README.md#implementing-your-own-store), [ADR 0023](decisions/0023-outbox-integration.md)).

## Queryable: sources, views and query contexts

The **source is the identity**; the **query context** describes how it is queried.

```csharp
// The query context: how the source can be queried. A record with query rules, no identity.
public sealed record MemberQueryContext : IQueryContext
{
    [Key] public Guid MemberEnrollmentId { get; init; }

    [Searchable(Priority = 1, MatchMode = MatchMode.Exact)]
    [Sortable]
    public string MemberNumber { get; init; } = string.Empty;
}

// The source: the published capability. Queryable directly at /{service}/queryable/{source}/query.
[QuerySource(Version = "1.0.0", DisplayName = "Members", Description = "Member enrollments, searchable by member number and name.",
             DefaultSortField = nameof(MemberQueryContext.MemberNumber))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class MemberQuerySource(MemberDbContext db) : IQuerySource<MemberQueryContext>
{
    public IQueryable<MemberQueryContext> CreateQuery(QueryExecutionContext ctx) =>
        db.MemberEnrollments.AsNoTracking().Select(e => new MemberQueryContext { /* ... */ });
}

// A view: a projection over one source, at /{service}/queryable/{source}/{view}/query.
[QueryView(Version = "1.0.0", DisplayName = "Member search", Description = "Searchable member results.",
           DefaultSortField = nameof(MemberQueryContext.MemberNumber))]
[Pageable(DefaultSize = 25, MaxSize = 250)]
internal sealed class MemberSearchView : IQueryViewSource<MemberQuerySource, MemberQueryContext, MemberSearchResult>
{
    public IQueryable<MemberSearchResult> CreateView(IQueryable<MemberQueryContext> query, QueryExecutionContext ctx) =>
        query.Select(x => new MemberSearchResult { /* ... */ });
}
```

Rules:
- **Interfaces are the identity; attributes describe** (as for steps). Discovery is
  interface-only:
  - every source needs `[QuerySource]`, every view needs `[QueryView]` (KAL2010);
  - an attribute without its interface is never discovered (KAL2009);
  - both attributes require a non-empty `Version`, `DisplayName` and `Description`
    (KAL2002, KAL2003).
- **Public names are type names:** a source's name is its `Type.Name`, unique per
  service; a view's name is its `Type.Name`, unique per source.
- **Query contexts implement `IQueryContext`** and carry the query rules
  (`[Filterable]`, `[Searchable]`, `[Sortable]`). Those attributes on any other type
  have no effect (KAL2011). A context can back several sources.
- **Parameter records implement `IQueryParameters`.** View and result records are
  plain output shapes, with no marker.
- **Paging:** a source's `[Pageable]` applies to direct queries of that source, a
  view's to that view. Nothing is inherited. A pageable view or local source
  declares a sortable `DefaultSortField` (prefer a unique field); requests without
  a sort are ordered by it, so pages are stable.
- **Sync vs async:** prefer `IQuerySource<T>` / `IQueryViewSource<…>` when the
  queryable is already in hand (in-memory, pre-fetched, or deferred LINQ). Use
  `IQuerySourceAsync<T>` / `IQueryViewSourceAsync<…>` when producing it needs async I/O.
  Never `.Result` / `.GetAwaiter().GetResult()` inside a sync implementation.
- **Delegated sources** (`IDelegatedQuerySource<TQueryContext, TResult, TParameters>`)
  are facades. Kaleido validates the consumer's query against `TQueryContext`; the
  source then:
  - translates it into a downstream query, adding internal criteria the consumer
    never sees;
  - calls a remote source or view (forward with `query.ToApiBody()`);
  - maps the response to its own `TResult`.

  Paging, filtering and sorting happen downstream, and Kaleido passes the returned
  `QueryResult` through untouched. Delegated sources have no views.
- **How a source is fulfilled (local or delegated) is not published:** consumers see
  every source the same way.
- **An async query provider (EF Core, MongoDB, ...): register its executor.** Kaleido
  counts the results when a page is full, through `IQueryContextExecutor<TView>`.
  Core knows no provider, so its default can only use what every `IQueryable` offers:
  a query that is also `IAsyncEnumerable<T>` is counted by reading every matching
  row; any other query by LINQ `Count()`, translated by the provider but run
  synchronously. A host on an async provider registers an executor that calls the
  provider's own async operators, so a count is one server-side count. Measured on EF
  Core with SQLite and a full page of 50 (`tools/benchmarks`, #326):

  | Rows | Default executor | Provider executor (`SELECT COUNT`) |
  |---|---|---|
  | 1,000 | 0.66 ms, 395 KB | 0.13 ms, 84 KB |
  | 10,000 | 5.9 ms, 3.2 MB | 0.37 ms, 84 KB |
  | 100,000 | 55.9 ms, 31.3 MB | 3.0 ms, 84 KB |

  The default grows with the matching rows; the provider executor does not. The EF
  Core executor (a MongoDB one calls `CountDocumentsAsync`/`ToListAsync` the same way). It is
  registered for every source of the host, so it uses EF's operators only for EF queries
  (`IAsyncQueryProvider`) and LINQ for any other source: EF's async operators throw on a query that
  is not EF's, such as an in-memory view next to EF sources (#352):

  ```csharp
  internal sealed class EfCoreQueryContextExecutor<TView> : IQueryContextExecutor<TView>
      where TView : class
  {
      public Task<int> CountAsync(IQueryable<TView> query, CancellationToken cancellationToken = default) =>
          query.Provider is IAsyncQueryProvider       // Microsoft.EntityFrameworkCore.Query
              ? EntityFrameworkQueryableExtensions.CountAsync(query, cancellationToken)
              : Task.FromResult(query.Count());

      public async Task<IReadOnlyList<TView>> ToListAsync(IQueryable<TView> query, CancellationToken cancellationToken = default) =>
          query.Provider is IAsyncQueryProvider
              ? await EntityFrameworkQueryableExtensions.ToListAsync(query, cancellationToken)
              : query.ToList();

      public IQueryable<TView> ApplyPage(IQueryable<TView> query, int size, int offset) =>
          query.Skip(offset).Take(size);
  }

  builder.Services.AddSingleton(typeof(IQueryContextExecutor<>), typeof(EfCoreQueryContextExecutor<>));
  ```

  The framework registers its default with `TryAdd`, so this wins before or after
  `AddKaleido()`. Proven in `tests/Kaleido.IntegrationTests/EfCore/EfCoreQueryContextExecutorTests.cs`.

## Exceptions

Throw `Kaleido*Exception` types only (KAL0002): `KaleidoValidationException`
(400 surface), `KaleidoConfigurationException` (startup), `KaleidoFrameworkException`
(internal integrity). Argument guards (`ArgumentNullException`) are fine.
Exception types are classes, never records (the compiler rejects a record deriving from `Exception`: CS8865).

## Async

Kaleido code awaits without `ConfigureAwait(false)`; CA2007 is `silent` on purpose,
for every project including the host-agnostic `Kaleido`, `Kaleido.Http.Client` and
`Kaleido.Provider.SQLite`.

- **Why:** the supported hosts (ASP.NET Core, the .NET generic host and its
  workers) have no `SynchronizationContext`, so there is no context to return to
  and `ConfigureAwait(false)` would change nothing.
- **Consumer rule:** await Kaleido's async APIs all the way up. Never block on them
  (`.Result`, `.Wait()`, `.GetAwaiter().GetResult()`), above all on a thread with a
  `SynchronizationContext` (UI frameworks such as WPF, WinForms or MAUI): that can
  deadlock.
- **Revisit:** if a host with a `SynchronizationContext` becomes supported, enable
  CA2007 for the host-agnostic projects and add `ConfigureAwait(false)` there.

## Dependency injection

Constructor injection, service abstractions, readonly fields — enforced by
KAL0004–KAL0013 (see the source rules in `ANALYZERS.md`). AddHttpClients /
AddKaleido configure lambdas are composition roots; `IServiceProvider` resolution
belongs there or in `HttpContext.RequestServices` — nowhere else.

## Static helpers vs extension methods

Convention (KAL0001 + MP-027): **a transformation that derives from one type is
an extension on that type** — `query.ToApiBody()`, `type.GetQueryViewInterface()`.
**Orchestration stays a helper/builder** — registry compile passes, metadata
composition, multi-source assembly. `static` classes exist only to host
extension methods.

## Correlation

Never set `X-Kaleido-*` headers by hand; never mint a second `RequestId`.
Ambient identity flows through `IKaleidoCorrelationContextAccessor` (scoped)
and is forwarded automatically by the typed clients. See the invariants in
`src/ARCHITECTURE.md` → Correlation context invariants.

## Unit tests

One fixture per SUT, named `{Sut}Tests`, mirroring the SUT's path
(KAL1001–KAL1004). Inherit `SutFixture<TSut>` and construct only inside
`CreateSut()` (KAL1006–KAL1008). Mock collaborators — `new` on a Kaleido
service interface implementation is KAL1011. `BuildServiceProvider` must pass
`ValidateScopes + ValidateOnBuild` (KAL1005).

## Endpoint metadata

Endpoint names are `const` fields in `*EndpointNames` classes in
`Kaleido.Http.Abstractions` — never inline literals in `WithName()`.
