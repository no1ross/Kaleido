# Choosing options

Every registration on the Kaleido builder (`services.AddKaleido(...)`), when to use it, and where it is
explained. One choice per row group; the startup checks reject combinations that contradict each
other.

## Transport

| I want to | Register | See |
| --- | --- | --- |
| Publish a local Queryable service over HTTP | `AddEventing()` + `AddQueryable()` + `AddServer()` + `AddHttp()`; choose its outbox store and `MapQueryable()`; no central client required | [Queryable HTTP server](../../src/Capabilities/Queryable/Kaleido.Queryable.Http.Server/README.md) |
| Participate in Registry over HTTP | `AddRegistryHttpParticipation()`; one gateway at `Kaleido:BaseUrl` and a hydrated unfiltered Registry | [Registry HTTP client](../../src/Capabilities/Registry/Kaleido.Registry.Http.Client/README.md) |
| Call a remote Queryable source or view | Register `AddRegistryHttpParticipation()` + `AddQueryableHttpClient()`, start the host to hydrate Registry, then use `IKaleidoQueryableClientFactory` with a neutral `QueryRequest` | [Queryable HTTP client](../../src/Capabilities/Queryable/Kaleido.Queryable.Http.Client/README.md) |
| Publish to AI agents | `AddHttp()` + `AddRegistryHttpParticipation()` + `AddProcessHttpClient()` + `AddQueryableHttpClient()` + `AddMcp()` with token exchange; no local capability runtime required for a reader-only host | [MCP README](../../src/Mcp/Mcp/README.md) |
| Publish several services to agents in one place | One reader-only MCP host: general tools use caller-filtered Registry discovery, while hydrated metadata and Process results drive pinned continuations; no per-service client URLs | [MCP guide](getting-started.md#6-publish-it-to-ai-agents-mcp) |

## Central registry

| I want to | Register or configure | See |
| --- | --- | --- |
| Host the control registry over HTTP | `AddControlRegistry()` + `AddRegistryStore<T>()` + `MapKaleidoControlRegistry()` | [Registry README](../../src/Capabilities/Registry/Kaleido.Registry.Server/README.md) |
| Share cached views across registry nodes | register `IDistributedCache` on the registry host; tune `Kaleido:Registry:Cache` | [Registry cache](../../src/Capabilities/Registry/Kaleido.Registry.Server/README.md#cache-kaleidoregistrycache) |
| Participate as a service or Registry reader | `AddRegistryHttpParticipation()` with gateway `Kaleido:BaseUrl` | [Participant README](../../src/Capabilities/Registry/Kaleido.Registry.Http.Client/README.md#taking-part-in-the-control-registry) |

## Process state

| Situation | Register | See |
| --- | --- | --- |
| Tests, local evaluation | [`AddInMemoryProviders()`](../../src/Providers/Kaleido.Provider.InMemory/README.md) or one `AddInMemory*Provider()` explicitly (lost on restart) | [Persistence](persistence.md) |
| Samples, local development, a reference | `AddSqliteContextProvider(connectionString)` | [SQLite README](../../src/Providers/Kaleido.Provider.SQLite/README.md) |
| Production | `AddContextStore<T>()` on your database | [Persistence](persistence.md#your-own-stores-on-ef-core) |

## Events

| Situation | Register | Guarantee |
| --- | --- | --- |
| Every service | `AddOutboxStore<T>()` (or `AddSqliteOutboxProvider`), on the context store's database | its events are written with its state |
| The host that publishes (one per outbox database: a service, or a host with nothing else) | `AddOutboxRelay<TPublisher>()` | at least once, retried, parked after the last attempt |
| You run your own bus or dispatcher | your `IEventPublisher`, on the relay host | the relay's, then your bus's |
| State and event atomic | `AddTransactions<T>()`, with the store and the outbox on the same database | both or neither |

See [Events and the outbox](events-and-outbox.md).

## Retries and idempotency

| Situation | Register |
| --- | --- |
| One instance, tests | `AddInMemoryProviders()` or explicitly select `AddInMemoryIdempotencyProvider()` for that store alone |
| Several instances, or retries across restarts | `AddIdempotencyStore<T>()` (or `AddSqliteIdempotencyProvider`) |

See [Processes - Concurrent and repeated calls](processes.md#concurrent-and-repeated-calls).

## Callers

| Situation | Set or register | See |
| --- | --- | --- |
| Local evaluation | `AuthorizationMode = None` | [Callers](callers-and-correlation.md#authentication-is-the-hosts-authorization-is-declared) |
| Signed-in callers, rules where needed | `Authenticated` (default) | [AUTHORIZATION](../architecture/authorization.md) |
| Nothing published without a rule | `ZeroTrust` | [AUTHORIZATION](../architecture/authorization.md) |
| Calls to other services carry the user's token | nothing (`Kaleido:OnBehalfOfCalls = Forward`, the default) | [Callers](callers-and-correlation.md#which-token-a-service-sends) |
| Exchange the user's token (required on an MCP host) | `Kaleido:OnBehalfOfCalls = Exchange` + `AddKaleidoClientHandler<T>()` | [Callers](callers-and-correlation.md#which-token-a-service-sends) |
| The service's own calls carry its app token | `AddKaleidoClientHandler<T>()` (client credentials, any library) | [Client README](../../src/Transports/Http/Kaleido.Http.Client/README.md) |

## Redaction and observability

| Situation | Register | See |
| --- | --- | --- |
| Classified fields in events | `AddEventRedaction(...)` or `AddEventRedactor<T>()` | [Events - Redaction](events-and-outbox.md#redaction) |
| Export traces and metrics | `AddOpenTelemetry()` | [OpenTelemetry README](../../src/Observability/Observability.OpenTelemetry/README.md) |

## Queries on EF Core

| Situation | Register | See |
| --- | --- | --- |
| Count full pages in the database | the EF Core `IQueryContextExecutor<>` | [Queries](queries.md#counting-and-listing-on-your-database) |
