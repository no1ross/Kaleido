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
- Name the next step by type with `Success<TNext>()` or `ProcessStepHandlerResult.Success(response).Next<TNext>()`; `Success()` lets the rules decide. `HandOff(targetProcessorName, targetResult, targetStepName)` derives the other service's answer/next step **names** and its actual `ServiceVersion` from the neutral `ProcessResult` (#198, #620); a missing/invalid target version is rejected, and the origin persists the selected target version so a later caller never switches to Registry's new current version by accident. An `IHandOffContinuationSource` can supply the same versioned continuation, and the typed overload also returns this step's response. `Reject`, `HandOff` and untyped results pass through a typed handler.
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
    ServiceVersion = "1",  // required for every service (from code or configuration); also scopes state and routes
    Assemblies = [typeof(Program).Assembly], // explicit — never omit (KAL2006)
})
    .AddEventing()                         // outbox-backed event writer; choose its store too
    .AddProcess()                          // explicit step/handler runtime
    .AddContextStore<IntakeContextStore>() // Process state: choose a provider explicitly
    .AddTransactions<IntakeTransactions>() // same provider session as the outbox
    .AddOutboxStore<IntakeOutboxStore>();  // Eventing owns this store

// Separately, a server that uses idempotency admission registers its own store:
// kaleido.AddIdempotencyStore<IntakeIdempotencyStore>(); // Kaleido.Process.Server
```

`AddKaleido()` registers the foundation, **not** Process/Queryable/Eventing. `AddProcess()` selects no in-memory context or idempotency store; the later Provider's `AddInMemoryProviders()` is an explicit development choice. Generic Process owns `IProcessContextStore` and `IProcessTransactions`; its transition state save and Eventing outbox add are atomic only when **both stores join the same provider session/transaction**, regardless of database technology. A common database alone is insufficient. The neutral `Kaleido.Process.Server.Idempotency.IProcessIdempotencyStore` is a separate server-side admission store with opaque replay bytes and a format identifier ([ADR 0044](../../decisions/0044-process-server-idempotency-ownership.md)); a durable implementation is needed across restarts/instances. The Process HTTP-server adapter now uses this neutral store with its own opaque replay format (#618); the [SQLite provider](../../../src/Providers/Kaleido.Provider.SQLite/README.md) now implements the neutral store for fresh schemas (#623/#587), while old HTTP-shaped replay rows have **not** been converted and require deliberate migration before reuse. A host publishing events calls `AddOutboxRelay<TPublisher>()` explicitly ([Eventing architecture](../events.md)). Concurrent saves are conditional on the loaded version: a stale save raises `ProcessConcurrencyException` (`pro_process_conflict`); the handler may have run already, so external effects must be idempotent. Store keys include processor, version and process id; new services require `ServiceVersion`, while persisted `ProcessKey.ProcessorVersion` and `ProcessContext.ProcessorVersion` remain nullable for legacy state. Business effects outside the transaction cannot be made atomic automatically.
