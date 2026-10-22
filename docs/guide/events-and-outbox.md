# Events and the outbox

Kaleido writes an event for everything that happens: the audit trail of processes and queries, for a
data warehouse, a compliance store or other services. The examples continue the orders service
(`docs/guide/examples/Events`); a test publishes a process's events and checks the redaction.

## Which events, and when

| Event | `EventType` | Written when | In the state's transaction |
|---|---|---|---|
| `ProcessCreated` | `process.created.v1` | a process starts | yes |
| `ProcessStepCompleted` | `process.step-completed.v1` | a step completes | yes |
| `ProcessPlanBuilt` | `process.plan-built.v1` | the runtime planned what a call may run | no |
| `ProcessExecutionCompleted` | `process.execution-completed.v1` | a step call ended (any outcome, failures included) | no |
| `QueryExecuted` | `query.executed.v1` | a query ran | no |

Every event carries a context (request id, process id, service, step, caller) and its payload; the
schemas are in the [`Kaleido` README](../../src/Kaleido/README.md).

- **In the transaction:** the event is kept if and only if the state change is kept
  ([Persistence](persistence.md#atomicity-one-database-one-transaction)).
- **Not in the transaction:** written after the work; a failure to write it is logged, never returned
  to the caller.

## Kaleido owns *when*; the outbox owns *how*

One choice per service ([ADR 0023](../decisions/0023-outbox-integration.md)):

| Choice | Registration | Delivery |
|---|---|---|
| **Kaleido's built-in outbox** (default) | storage: `AddOutboxStore<T>()` or `AddSqliteOutboxStore`; delivery: `AddEventPublisher<T>()` | a background relay claims stored events and calls your `IEventPublisher`, retrying |
| **Your own outbox** | `AddOutbox<T>()` ([Persistence](persistence.md#your-own-stores-on-ef-core)) | your outbox and dispatcher; Kaleido's store and relay are not used |
| **Direct publication** | `AddDirectPublication()` + `AddEventPublisher<T>()` | published once after the commit; no durability, no retry |

<!-- snippet: events-publisher -->
```csharp
// Kaleido's relay calls the publisher for each stored event, and retries when it throws.
public sealed class WarehousePublisher(IWarehouseFeed feed) : IEventPublisher
{
    public Task PublishAsync<TEvent, TContext>(
        KaleidoEventEnvelope<TEvent, TContext> envelope,
        CancellationToken cancellationToken)
        where TEvent : IKaleidoEvent =>
        feed.SendAsync(envelope.EventType, JsonSerializer.Serialize(envelope), cancellationToken);
}
```

```mermaid
sequenceDiagram
    autonumber
    participant K as Kaleido
    participant O as Outbox store (your database)
    participant R as Relay (background, every instance)
    participant P as Your IEventPublisher

    rect rgba(70, 130, 180, 0.25)
        Note over K,O: In the request
        K->>O: AddAsync(events) (with the state, for a transition)
    end

    rect rgba(70, 130, 180, 0.25)
        Note over R,P: In the background
        R->>O: claim a batch (lease)
        R->>P: PublishAsync(event)
        alt published
            R->>O: mark published
        else failed
            R->>O: retry later (backoff); park after the last attempt
        end
    end
```

Placing, confirming and shipping an order publishes `process.created.v1`, three
`process.step-completed.v1` (one per step), and a `process.execution-completed.v1` and
`process.plan-built.v1` per call.

## The guarantees

| Situation | Built-in outbox (durable store) | Your own outbox | Direct publication |
|---|---|---|---|
| The publisher (broker) is down | events wait in the store; retried with backoff, parked after the last attempt | your dispatcher's policy | the event is lost (logged) |
| The service restarts | nothing lost; the relay continues | your dispatcher's policy | events not yet published are lost |
| The instance crashes mid-publish | the claim's lease expires; another instance publishes it (**at least once**: consumers deduplicate by `EventId`) | your dispatcher's policy | lost |
| In-memory store (default) | lost on restart; each instance relays only its own | - | - |

Without any `IEventPublisher`, the built-in outbox and direct publication discard events (Kaleido logs
a warning); your own outbox always receives them.

## Redaction

Events go to stores that keep them for years, so classified fields are redacted **before** an event
is written. Classify fields with your own `DataClassificationAttribute` subclasses, and give the
events a policy per classification. The policy is separate from log redaction: logs and events often
need different rules ([ADR 0025](../decisions/0025-event-redaction-policy.md)).

<!-- snippet: events-classification -->
```csharp
// Your data classifications: mark the fields; the event policy says what each becomes.
public static class ShopClassifications
{
    public static DataClassification Personal { get; } = new("Shop", "Personal");
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class PiiAttribute() : DataClassificationAttribute(ShopClassifications.Personal);
```

`ShipOrderStep.Address` is `[Pii]` ([Processes](processes.md#which-steps-may-run)); with this
registration, the shipping step's event carries no address:

<!-- snippet: events-registration -->
```csharp
kaleido
    .AddEventPublisher<WarehousePublisher>()
    .AddEventClassificationRedaction(r =>
        r.SetRedactor<ErasingRedactor>(new DataClassificationSet(ShopClassifications.Personal)));
```

| Registration | Effect |
|---|---|
| `AddEventClassificationRedaction(...)` | the events' redactor per classification (`ErasingRedactor`, a hashing redactor, ...) |
| `AddEventRedactor<T>()` | your own `IKaleidoEventRedactor` |
| (neither) | classified fields are erased, with a warning at startup |

## Where to read more

- The outbox store contract, the relay's options (batch size, concurrency, retries):
  [`Kaleido` README](../../src/Kaleido/README.md).
- The SQLite outbox, and joining its transaction from your own outbox:
  [SQLite provider README](../../src/Kaleido.Provider.SQLite/README.md).
