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
| Kaleido delivers, durably | `AddOutboxStore<T>()` (or `AddSqliteOutboxStore`) + `AddEventPublisher<T>()` | at least once, retried, parked after the last attempt |
| You already run an outbox | `AddOutbox<T>()` | your dispatcher's |
| Best effort is enough | `AddDirectPublication()` + `AddEventPublisher<T>()` | at most once, no retry |
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
| Calls to other services carry the user's token | `ForwardCallerToken()` | [Callers](callers-and-correlation.md#which-credential-a-service-sends) |
| Exchange the token per call | `AddCallCredentials<T>()` | [ADR 0022](../decisions/0022-call-credentials.md) |

## Redaction and observability

| Situation | Register | See |
|---|---|---|
| Classified fields in events | `AddEventClassificationRedaction(...)` or `AddEventRedactor<T>()` | [Events - Redaction](events-and-outbox.md#redaction) |
| Export traces and metrics | `AddOpenTelemetry()` | [OpenTelemetry README](../../src/Kaleido.Observability.OpenTelemetry/README.md) |

## Queries on EF Core

| Situation | Register | See |
|---|---|---|
| Count full pages in the database | the EF Core `IQueryContextExecutor<>` | [Queries](queries.md#counting-and-listing-on-your-database) |
