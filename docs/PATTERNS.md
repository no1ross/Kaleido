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
  `IProcessStep` type needs `[ProcessStep]` (KAL2011), and `[ProcessStep]` is only
  valid on `IProcessStep` types (KAL2010).
- **The step name is the type name** (`Type.Name`, unmodified) — in the registry, on
  the wire, in persisted state, and in events. Renaming the type renames the step.
- `[ProcessStep]` declares non-empty `Version`, `DisplayName`, and `Description`
  (KAL2001). `DisplayName`/`Description` are what UIs and AI agents read — write
  them for that audience.
- **The next step is named by type**: `Success<TNext>()` (or
  `ProcessStepHandlerResult<T>.Success<TNext>(response)`); `Success()` lets the
  process rules decide. Cross-processor routing uses `HandOff(targetProcessorName)`.
- Relationships are typed: `[DependsOn<T>]`, `[AvailableAfter<T>]`,
  `[AvailableUntil<T>]`, constrained to `IProcessStep`.
- Every step needs exactly one handler (KAL2008); the handler must not swallow
  `OperationCanceledException` in `catch (Exception)` — filter it or let the
  executor own cancellation (KAL2004).
- **Describe inputs** with `[Description("…")]` or `[Display(Description = "…")]`
  (KAL2017); `[Display(Name, Prompt)]` are optional hints. The registry publishes
  them — UIs and AI agents use them to know what to ask for.
- **Only next steps run:** a request may start with the pending required step or,
  when nothing is required, a step available under the process rules
  (`pro_step_not_available` otherwise).

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

// The information step: its payload is the answers; nothing else (KAL2015).
[ProcessStep(Version = "1.0", DisplayName = "Out-of-network responses", Description = "Answers the out-of-network attestation.")]
public sealed record CaptureOutOfNetworkResponseStep : IInformationStep
{
    public required string InformationRequestId { get; init; }
    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}
```

Rules:
- Require an information step only with `RequireInformation<TNext>(request)` — never
  `Success<TNext>()` (KAL2016).
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
    o.ServiceName = "intake";                       // lowercase, no separators (KAL2005)
    o.Assemblies = [typeof(Program).Assembly];      // explicit — never omit (KAL2009)
})
    .AddHttp();
```

`AddKaleido()` auto-registers Processor + Queryable runtimes and binds
`Kaleido:*` config into an immutable `KaleidoServiceOptions` snapshot. Set
options via the `KaleidoServiceOptionsBuilder` configure lambda — the
registered options are init-only.

`AddHttp()` always wires `ExceptionMiddleware` + `ObservabilityMiddleware` via
`KaleidoStartupFilter`; there is no opt-out.

### Middleware ordering

The startup filter registers `ExceptionMiddleware` **outside**
`ObservabilityMiddleware`, before the rest of the host pipeline. The outer
boundary turns exceptions from correlation setup and endpoints into Kaleido
HTTP error responses; the inner middleware initializes correlation and echoes
its headers. Reversing them would leave a failure during observability setup
outside Kaleido's exception handler. Both middleware types are internal; hosts
never register them manually.

## Transport-neutral response semantics

Design the meaning of response fields before choosing JSON, gRPC, or another
transport. Keep handler-authored `BusinessMessages` separate from framework
`FrameworkMessages`: consumers may present the former and treat the latter as
diagnostics. A transport option may include framework messages, but when it is
off the collection is empty, not absent by a JSON-only rule. Repeated fields
have the same empty-collection meaning in a gRPC consumer. Adapters decide
which messages to populate; serializer-specific omission does not define the
contract or belong in core runtime types.

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
[QuerySource(Version = "1.0.0", DisplayName = "Members", Description = "Member enrollments, searchable by member number and name.")]
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
  - every source needs `[QuerySource]`, every view needs `[QueryView]` (KAL2013);
  - an attribute without its interface is never discovered (KAL2012);
  - both attributes require a non-empty `Version`, `DisplayName` and `Description`
    (KAL2002, KAL2003).
- **Public names are type names:** a source's name is its `Type.Name`, unique per
  service; a view's name is its `Type.Name`, unique per source.
- **Query contexts implement `IQueryContext`** and carry the query rules
  (`[Filterable]`, `[Searchable]`, `[Sortable]`). Those attributes on any other type
  have no effect (KAL2014). A context can back several sources.
- **Parameter records implement `IQueryParameters`.** View and result records are
  plain output shapes, with no marker.
- **Paging:** a source's `[Pageable]` applies to direct queries of that source, a
  view's to that view. Nothing is inherited.
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

## Exceptions

Throw `Kaleido*Exception` types only (KAL0002): `KaleidoValidationException`
(400 surface), `KaleidoConfigurationException` (startup), `KaleidoFrameworkException`
(internal integrity). Argument guards (`ArgumentNullException`) are fine.
Exception types are classes, never records (KAL0004/KAL1011).

## Dependency injection

Constructor injection, service abstractions, readonly fields — enforced by
KAL0005–KAL0014 (see the source rules in `ANALYZERS.md`). AddHttpClients /
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
service interface implementation is KAL1012. `BuildServiceProvider` must pass
`ValidateScopes + ValidateOnBuild` (KAL1005).

## Endpoint metadata

Endpoint names are `const` fields in `*EndpointNames` classes in
`Kaleido.Http.Abstractions` — never inline literals in `WithName()`.
