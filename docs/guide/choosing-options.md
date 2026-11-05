# Choosing options

Every registration on the Kaleido builder (`services.AddKaleido(...)`), when to use it, and where it is
explained. One choice per row group; the startup checks reject combinations that contradict each
other.

## Transport

| I want to | Register | See |
|---|---|---|
| Publish the service over HTTP | `AddHttp()` + `MapKaleidoHttp()` | [Getting started](getting-started.md#4-publish-it-over-http) |
| Call other Kaleido services | `AddHttpClients()` (configured under `Kaleido:Clients`) | [client README](../../src/Kaleido.Http.Client/README.md) |
| Publish to AI agents | `AddMcp()` + `MapKaleidoMcp("/mcp")` (needs `AddHttpClients()`) | [Getting started](getting-started.md#6-publish-it-to-ai-agents-mcp) |
| Publish several services to agents in one place | a router host: `AddHttpClients()` + `AddMcp()`, `AggregateRegistry = true` | [README quick start](../../README.md#expose-configured-services-through-an-mcp-router) |

## Process state

| Situation | Register | See |
|---|---|---|
| Tests, local evaluation | nothing (in memory) | [Persistence](persistence.md#the-defaults-are-in-memory) |
| Samples, local development, a reference | `AddSqliteContextStore(connectionString)` | [SQLite README](../../src/Kaleido.Provider.SQLite/README.md) |
| Production | `AddContextStore<T>()` on your database | [Persistence](persistence.md#your-own-stores-on-ef-core) |

## Events

| Situation | Register | Guarantee |
|---|---|---|
| Every service | `AddOutboxStore<T>()` (or `AddSqliteOutboxStore`), on the context store's database | its events are written with its state |
| The host that publishes (one per outbox database: a service, or a host with nothing else) | `AddOutboxRelay<TPublisher>()` | at least once, retried, parked after the last attempt |
| You run your own bus or dispatcher | your `IEventPublisher`, on the relay host | the relay's, then your bus's |
| State and event atomic | `AddTransactions<T>()`, with the store and the outbox on the same database | both or neither |

See [Events and the outbox](events-and-outbox.md).

## Retries and idempotency

| Situation | Register |
|---|---|
| One instance, tests | nothing (in memory) |
| Several instances, or retries across restarts | `AddIdempotencyStore<T>()` (or `AddSqliteIdempotencyStore`) |

See [Processes - Concurrent and repeated calls](processes.md#concurrent-and-repeated-calls).

## Callers

| Situation | Set or register | See |
|---|---|---|
| Local evaluation | `AuthorizationMode = None` | [Callers](callers-and-correlation.md#authentication-is-the-hosts-authorization-is-declared) |
| Signed-in callers, rules where needed | `Authenticated` (default) | [AUTHORIZATION](../AUTHORIZATION.md) |
| Nothing published without a rule | `ZeroTrust` | [AUTHORIZATION](../AUTHORIZATION.md) |
| Calls to other services carry the user's token | nothing (`Kaleido:OnBehalfOfCalls = Forward`, the default) | [Callers](callers-and-correlation.md#which-token-a-service-sends) |
| Exchange the user's token (required on an MCP host) | `Kaleido:OnBehalfOfCalls = Exchange` + `AddKaleidoClientHandler<T>()` | [Callers](callers-and-correlation.md#which-token-a-service-sends) |
| The service's own calls carry its app token | `AddKaleidoClientHandler<T>()` (client credentials, any library) | [Client README](../../src/Kaleido.Http.Client/README.md) |

## Redaction and observability

| Situation | Register | See |
|---|---|---|
| Classified fields in events | `AddEventRedaction(...)` or `AddEventRedactor<T>()` | [Events - Redaction](events-and-outbox.md#redaction) |
| Export traces and metrics | `AddOpenTelemetry()` | [OpenTelemetry README](../../src/Kaleido.Observability.OpenTelemetry/README.md) |

## Queries on EF Core

| Situation | Register | See |
|---|---|---|
| Count full pages in the database | the EF Core `IQueryContextExecutor<>` | [Queries](queries.md#counting-and-listing-on-your-database) |
