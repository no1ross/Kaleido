# Authoring Process steps and handlers

This is the consumer-side pattern for the [Process capability](process.md). [Source and consumer analyzers](../../ANALYZERS.md) enforce the cited KAL rules; the [information-request model](information-requests.md) has the complete question/answer contract.

## Step + handler

```csharp
[ProcessStep(
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

- `IProcessStep` is the identity; `[ProcessStep]` only describes it. Every step interface implementation needs the attribute (KAL2008), and the attribute is valid only on an `IProcessStep` (KAL2007). A step's public/persisted/event name is its unmodified `Type.Name`; renaming the CLR type renames the step. Supply nonempty `DisplayName` and `Description` for UI/agent readers (KAL2001).
- Required inputs are C# `required` members. `[Required]` cannot distinguish an omitted value type from its default, so declare `[Required] public required Guid MemberId { get; init; }` (KAL2018); callers supply it, and omitted request fields receive a 400. Use `[Description("…")]` or `[Display(Description = "…")]` to explain input fields to a UI/agent (KAL2014); `Display.Name`/`Prompt` are optional.
- Name the next step by type with `Success<TNext>()` or `ProcessStepHandlerResult.Success(response).Next<TNext>()`; `Success()` lets the rules decide. `HandOff(targetProcessorName, targetResponse)` returns the other service's answer/next steps/messages (#198); its three-argument form adds this step's typed response. `Reject`, `HandOff` and untyped results pass through a typed handler.
- `Reject(messages)` is for correctable input or a retryable call: the step is not completed and the state stays as it was. Reject before external writes or make the handler safe to run again. `Failure(messages)` ends a process that cannot proceed; an unexpected exception leaves the step retryable ([ADR 0008](../../decisions/0008-rejected-outcome-and-retryable-exceptions.md)). Do not swallow `OperationCanceledException` in `catch (Exception)` (KAL2004).
- Relationships are typed and constrained to `IProcessStep`: `[DependsOn<T>]` requires T's completion first (several means all; order it first if it is in the same request); `[AvailableAfter<T>]` opens a step after all named completions at any later point; `[AvailableUntil<T>]` closes it when the first named step completes. `[Repeatable]` allows another completion; without it a completed step is skipped (`pro_already_processed`). A step with neither DependsOn nor AvailableAfter (and not an information step) is initially available. Self/circular/unknown relationships fail at startup with `pro_invalid_registration`; each step needs exactly one handler (`pro_missing_handler`). Only a pending required step or an available step can run next (`pro_step_not_available`).
- Describe step effects that only the author knows ([ADR 0015](../../decisions/0015-declared-step-behavior.md)): `Destructive = true` warns of irreversible/loss-causing work; `External = true` means a write to a system outside the process state (not a read or Kaleido handoff); `RequiresConfirmation = true` is a client confirmation hint. Without `[Repeatable]` a step is once-per-process. Classify sensitive fields with your own `DataClassificationAttribute` subclasses (`[PHI]`, `[PII]`) so Registry/MCP can describe them and Eventing can redact them ([ADR 0013](../../decisions/0013-event-payloads-classified-and-redacted.md)).

## Lookup inputs (ids the user cannot type)

A step can declare the published query that supplies an id:

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

`Service` is the publishing route prefix; `Source`, optional `View` and `Field` are published names. A lookup may live on another service, so names are checked when registries meet (the MCP adapter logs an unresolved link), not at step startup. Inputs in one `Group` use the **same** result (one group per query by default); name groups when the same query is used twice. `Display` fields identify results for people; one field in the group can declare them. Kaleido publishes the link but never chooses a record: UI/MCP guides the user/agent ([MCP lookups](../../../src/Mcp/Mcp/README.md#lookups)); the handler must still validate the chosen id against its domain rules.

## Information step (runtime-decided questions)

Known inputs are step properties. A handler that learns questions at runtime requires an information step; Kaleido owns the schema, not the question content:

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
[ProcessStep(DisplayName = "Out-of-network responses", Description = "Answers the out-of-network attestation.")]
public sealed record CaptureOutOfNetworkResponseStep : IInformationStep
{
    public required string InformationRequestId { get; init; }
    public required string ExchangeId { get; init; }   // echoes the pending request's exchange (#442)
    public IReadOnlyList<InformationResponseItem> Items { get; init; } = [];
}
```

Require it only with `RequireInformation<TNext>(request)`, not `Success<TNext>()` (KAL2013). Kaleido validates answers structurally against the pending request; the handler interprets them. For adaptive rounds mark the information step `[Repeatable]` and require itself with the next request. Kaleido stores only the pending request; the application's event trail records past questions/answers (`StepCompleted` carries each payload). See the [full information model](information-requests.md).

## Process host and persistence

```csharp
services.AddKaleido(builder.Configuration, new()
{
    ServiceName = "intake", // lowercase letters, digits, '-' and '_' (KAL2005)
    ServiceVersion = "1",  // required for versioned HTTP routes
    Assemblies = [typeof(Program).Assembly], // explicit — never omit (KAL2006)
})
    .AddEventing()                         // outbox-backed event writer; choose its store too
    .AddProcess()                          // explicit step/handler runtime
    .AddContextStore<IntakeContextStore>() // every used store must be chosen (ADR 0028)
    .AddIdempotencyStore<IntakeIdempotencyStore>()
    .AddOutboxStore<IntakeOutboxStore>()   // or AddInMemoryStores() for local development
    .AddHttp()
    .AddControlRegistryClient(); // Kaleido:BaseUrl names the gateway to the central registry
```

`AddKaleido()` registers the foundation, **not** Process/Queryable/Eventing. A provider's state/idempotency stores and outbox must be durable for production, with `IKaleidoTransactions` covering state + event on the same database when atomicity is required. A host publishing the database's events calls `AddOutboxRelay<TPublisher>()` explicitly; other services need no publisher ([Eventing architecture](../events.md)). Concurrent saves are conditional on the loaded version: the losing call receives 409 `pro_process_conflict`; the handler may already have run, so external side effects must be idempotent (upsert, business key or process id). An `Idempotency-Key` returns the stored result for a retry without executing a step twice. A third-party caller supplies its own key per logical call. A custom store implements a conditional save scoped by `ProcessKey` (processor, version, process id), idempotent-result methods and an outbox with leased claims and writer identity ([SQLite provider contract](../../../src/Providers/Kaleido.Provider.SQLite/README.md#implementing-your-own-store), [ADR 0036](../../decisions/0036-stores-scoped-by-service-and-a-shared-outbox-relay.md)). Business side effects outside the store cannot be made atomic automatically.
