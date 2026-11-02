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

Every event carries a context (request id, process id, service and its version, step, caller) and its
payload; the schemas are in the [`Kaleido` README](../../src/Kaleido/README.md). A process event also
carries `ProcessVersion`, the version of the process state it describes: there is no ordering guarantee,
so order a process's events by it.

- **In the transaction:** the event is kept if and only if the state change is kept
  ([Persistence](persistence.md#atomicity-one-database-one-transaction)).
- **Not in the transaction:** written after the work; a failure to write it is logged, never returned
  to the caller.

## Services write; a relay publishes

Every service writes every event to its outbox store, in the same transaction as its state, and needs no
publisher. One relay publishes them ([ADR 0036](../decisions/0036-stores-scoped-by-service-and-a-shared-outbox-relay.md)):

| Part | Registration | Runs where |
|---|---|---|
| **The outbox store** (every service) | `AddOutboxStore<T>()` or `AddSqliteOutboxStore`, on the context store's database | in every service |
| **The relay** and its `IEventPublisher` | `AddOutboxRelay<TPublisher>()` | on one host per outbox database: one of the services, or a host with nothing else |

Several services, and versions of one, may share an outbox database. Each entry names its writer
(`ServiceName`, `ServiceVersion`), and the relay drains every one: it polls and claims a batch at a time,
and several relay instances coordinate through leased claims. A database no relay reads keeps growing.

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
    participant R as Relay (AddOutboxRelay, one host per database)
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

| Situation | What happens (durable outbox store) |
|---|---|
| The publisher (broker) is down | events wait in the store; retried with backoff, parked after the last attempt |
| A service or the relay host restarts | nothing lost; the relay continues |
| A relay instance crashes mid-publish | the claim's lease expires; another relay instance publishes it (**at least once**: consumers deduplicate by `EventId`) |
| No relay runs for the database | the events wait and the database grows |
| In-memory store (`AddInMemoryStores()`) | lost on restart; drained only by a relay in the same process |

## Is a relay running?

Nothing at startup can tell whether some other host relays a service's outbox, so every service checks
it at run time, in its own database ([ADR 0036](../decisions/0036-stores-scoped-by-service-and-a-shared-outbox-relay.md)):

- **Every relay instance keeps a heartbeat** in the outbox it drains (`IKaleidoOutboxStore.RecordRelayAsync`,
  every `Kaleido:Events:RelayHeartbeatInterval`, default 30 s).
- **Every service's health check `kaleido-outbox`** (registered by `AddKaleido()`) reads it there, with the
  backlog. It fails when:
  - no relay was seen within `RelaySeenWithin` (default 2 minutes), including when none ever ran;
  - the oldest due event is older than `BacklogMaxAge` (default 5 minutes);
  - more than `BacklogMaxCount` events are due (default 10,000).
- **It reports `Degraded`** (`OutboxHealthFailureStatus`): the service still works and its events wait.
  Set `Unhealthy` to fail readiness on it.

It calls no other service, so no check depends on another service's health.

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
// This service writes its events and also runs the relay that publishes them; with services
// sharing an outbox database, one host runs the relay for all of them.
kaleido
    .AddOutboxRelay<WarehousePublisher>()
    .AddEventRedaction(r =>
        r.SetRedactor<ErasingRedactor>(new DataClassificationSet(ShopClassifications.Personal)));
```

| Registration | Effect |
|---|---|
| `AddEventRedaction(...)` | the events' redactor per classification (`ErasingRedactor`, a hashing redactor, ...) |
| `AddEventRedactor<T>()` | your own `IKaleidoEventRedactor` |
| (neither) | classified fields are erased, with a warning at startup |

## Where to read more

- The outbox store contract, the relay's options (batch size, concurrency, retries):
  [`Kaleido` README](../../src/Kaleido/README.md).
- The SQLite outbox, and an outbox store of your own joining its transaction:
  [SQLite provider README](../../src/Kaleido.Provider.SQLite/README.md).
