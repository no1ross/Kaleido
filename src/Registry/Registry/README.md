# Kaleido.Registry

The control registry (#553): the central registry every Kaleido service registers with and heartbeats
to. It keeps one record per service and version, each instance's status, and each service's current
version. It serves the registry to everyone through one endpoint: the full registry to the other Kaleido
hosts, and to any other caller (the UI, a user) only what that caller may use. It makes no outbound calls.

> [ADR 0040](../../../docs/decisions/0040-central-registry-only-and-versioned-routes.md) supersedes
> the earlier staged design in [ADR 0037](../../../docs/decisions/0037-the-control-registry.md).
> HTTP services now map versioned routes; the registry and gateway never infer instance addresses.

## A registry host

The registry is a Kaleido host with no capabilities of its own. Reference `Kaleido.Registry.Http`
for the fixed HTTP API; supply a durable `IKaleidoRegistryStore` in production:

```csharp
builder.Services.AddKaleido(builder.Configuration, new()
    {
        ServiceName = "registry",
        Assemblies = [typeof(Program).Assembly],
        TypeFilter = _ => false,
        AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust,
    })
    .AddInMemoryStores()        // a Kaleido host chooses its stores; the registry writes no process state
    // Kaleido:Registry:RegistrantPolicy and ReaderPolicy name the host's policies (required; see Trust)
    .AddHttp()
    .AddControlRegistry()
    .AddInMemoryRegistryStore(); // or a durable registry store (AddRegistryStore<T>())

app.UseAuthentication();
app.UseAuthorization();
app.MapKaleidoControlRegistry();
```

## Endpoints

| Endpoint | Caller | Answer |
|---|---|---|
| `POST /registry/instances` | an instance, with its service's own token | `RegistryLease`; 403 `registry_registrant_not_allowed`; 400 `registry_invalid_registration` |
| `PUT /registry/instances/{instanceId}/heartbeat` | the instance (`RegistryHeartbeat`: its local health) | `RegistryLease`; 404 `registry_instance_unknown` (register again) |
| `DELETE /registry/instances/{instanceId}` | the instance, at shutdown | 204; 404 |
| `GET /registry` | everyone | `KaleidoRegistry`: processors, query sources, logical version routes, overall status and `Missing`, with ETag, 304 and `?strict`. Unfiltered for a Kaleido host (`ReaderPolicy`); otherwise capability- and route-filtered for that caller, with its own ETag |
| `GET /registry/capabilities` | a Kaleido host when availability names a new revision | capabilities and versioned transport bindings (no statuses), with an ETag that changes on a capability or route change; 403 for anyone else |
| `GET /registry/availability` | a Kaleido host keeping its copy fresh | each service version's status, current marker and published transports, no instances (`KaleidoRegistryAvailability`), with an ETag; 403 for anyone else |
| `GET /registry/status` | operations, the registry's UI | each service version's status, published transports and instances (`KaleidoRegistryStatus`), with its own ETag (heartbeat times alone do not change it); 403 for anyone else |

Every endpoint is described for OpenAPI (tag `Control registry`, summary, description, what it accepts,
its success type and each error it can answer, as a `KaleidoErrorResponse`), as the process endpoints
are (#564 brings every Kaleido endpoint to the same).

- **Pushed, never polled.** Instances send registrations and heartbeats; the registry never calls an
  instance. An instance whose heartbeats stop is no longer ready once its lease runs out
  (`HeartbeatInterval` x `MissedHeartbeats`, default 30 s), and it is removed after `Retention`
  (default 24 h).
- **One revision per published shape.** A registration carries the instance's capabilities and its
  service version's transport bindings (`RegistryTransportRoute`: `http` by default, later `grpc`)
  with public logical gateway paths, never addresses. Identical registrations share one hash of
  that shape; changing a route makes a new capabilities revision without a store schema change.
- **Rolling deployments.** A version publishes only the steps and query sources every live revision
  shares, as the longest-running revision defines them. Only transports all live revisions support
  are advertised; the oldest revision's binding wins until it leaves. No common transport makes
  the version `Unavailable`.
- **One status per capability.** Every processor and query source carries its service version's overall
  status (`RegistryCapabilityStatus`), so a consumer - MCP, a router serving its users - knows what is
  working without reading any instance:

  | Status | Meaning |
  |---|---|
  | `Available` | every known instance is ready |
  | `Degraded` | some instances are ready, but not all (one failed, a rollout is under way): calls work |
  | `Unknown` | none is ready yet, some have not reported since the registry resumed after it was down |
  | `Unavailable` | none is ready (the version stays listed, with its capabilities): calls will fail |

  The instances themselves (`Ready`, `NotReady`, `Unknown`; last heartbeat) are on
  `GET /registry/status`, for operations. Nothing writes a state: each is worked out on every read from
  what the instances reported.
- **After a restart.** The registry serves what its store holds at once. A new registry node joining
  running ones changes nothing. After every node was down (a deployment), the registry's watch - one
  record every node touches every `Lease` / 3 - resumes: an instance not heard from since is `Unknown`
  for one lease (its silence says nothing; live instances report within a heartbeat, without
  registering again), then `NotReady`. The retention sweep waits until the watch has run unbroken for
  `Retention`, so an outage never removes the registry.
- **Current version.** A service's first registered version is its current one
  (`RegistryServiceStatus.IsCurrent`); a later version never takes over by registering.
- **Who may register, and who reads.** See [Trust](#trust): service applications only, each as itself;
  Kaleido hosts only, unfiltered.
- **One call to consume, two to sync.** A consumer reads `GET /registry`: everything in one call. A
  Kaleido host checks `/registry/availability` at the registry's pace and fetches
  `/registry/capabilities` only when `CapabilitiesRevision` changes. Each document has its own ETag:
  many hosts download capabilities only when their published shape changes, never the instances.
- **Completeness.** `Kaleido:Registry:Expected` lists the processor versions the environment needs; one
  not registered, or a service a published step looks up and nobody registered, is listed in `Missing`
  and makes the registry partial (`?strict` answers 502).

## What the registry does not know

Where anything runs. A service never registers an address or a host: the router maps each
`/{service}/{version}` route to that version's host, from its own configuration deployed with the
version. The registry describes services, versions, capabilities, statuses and logical routes, and
rejects registrations with absolute or unsafe paths. No registration can redirect credential-bearing
traffic to a host or DNS name.

## Trust

The registry trusts the identity provider, never what a caller says about itself: the host's
authentication validates each token, and Kaleido reads who the caller is from it.

| Question | Answered by |
|---|---|
| Is the caller a service application, not a user? | `Kaleido:Registry:RegistrantPolicy`, on every registration, heartbeat and deregistration: a host policy only app-only tokens satisfy |
| Which service may it register? | its app roles naming the services, `{RegistrantRolePrefix}{service}` (`Kaleido.Registrant.code-sets`), assigned to each service's app registration in the IdP; and, as a fallback, the `Kaleido:Registry:Registrants` map (service -> client ids, compared with the token's actor `act`/`azp`/`appid`, else its subject) |
| What does it read? | `GET /registry`: all of it, unfiltered, for a caller satisfying `Kaleido:Registry:ReaderPolicy` (the Kaleido hosts: services, MCP), which filter it for their own callers; for anyone else, only the capabilities that caller may use. The documents for keeping a copy fresh and the status are for the Kaleido hosts only |

- A deployment that is not the service fails: it has neither the service's credential nor the app role
  naming the service. A real service registering another one's name gets 403
  `registry_registrant_not_allowed`, and a registration may only carry its own service's capabilities.
- A user signed in through a service's client id is not that service: their token fails
  `RegistrantPolicy`.
- Both policies are required unless `AuthorizationMode` is `None` (development only).
- With 150+ services, the registry needs no per-service configuration: the IdP admin gives each
  service's app registration the role naming itself when creating it, and the reader role to the hosts
  that read.
- Which capabilities a caller may *use* is a separate matter: each source's, view's and step's
  authorization is published in the registry and enforced by its service. Filtering `GET /registry` for a
  user is discovery only, by those published rules: define on the registry host the policies they name
  (as MCP hosts do); a capability naming a policy the registry host lacks is hidden, with a Warning,
  never a failed read.

An example with Entra ID: the registry's app registration defines the app roles (`allowedMemberTypes:
Application`) `Kaleido.RegistryReader` and one `Kaleido.Registrant.{service}` per service, each assigned
to that service's app; app-only tokens carry `idtyp: app`.

```csharp
builder.Services.AddAuthorization(o =>
{
    o.AddPolicy("KaleidoServiceApplication", p => p.RequireClaim("idtyp", "app"));
    o.AddPolicy("KaleidoRegistryReader", p => p.RequireClaim("idtyp", "app").RequireRole("Kaleido.RegistryReader"));
});
// Kaleido:Registry:RegistrantPolicy = "KaleidoServiceApplication", Kaleido:Registry:ReaderPolicy = "KaleidoRegistryReader"
```

Validate the token's audience (the registry's own API) in the host's authentication, so a token issued
for another API is not accepted. Which policies Kaleido needs overall, and their defaults, is #565;
a production guard and the host-side docs are #566.

## Settings (`Kaleido:Registry`)

`KaleidoRegistryOptions`: `RegistrantPolicy`, `ReaderPolicy`, `RegistrantRolePrefix`
(`Kaleido.Registrant.`), `Registrants` (optional), `Expected`, `HeartbeatInterval` (10 s),
`MissedHeartbeats` (3), `Retention` (24 h), and `Cache`. An invalid value stops startup (`invalid_options`).

The registry sets every participant's pace: the heartbeat interval in each lease, and the interval at
which the hosts check the availability (`HeartbeatInterval`) with each availability. The availability
also names the capabilities' revision (`CapabilitiesRevision`): a host reads the capabilities exactly
when they changed, so a deployment shows on every host within one check. A change reaches every host at
its next check.

### Cache (`Kaleido:Registry:Cache`)

`AddControlRegistry()` registers HybridCache with a local in-memory tier. If the host registers an
`IDistributedCache` (for example Redis), HybridCache also shares entries across registry nodes;
the registry store is still the source of truth. `KaleidoRegistryCacheOptions` binds these settings:

- `KeyPrefix` defaults to `kaleido:registry`; give environments sharing a distributed cache distinct prefixes.
- `ViewTtl` defaults to one second. The registry, status, availability and capabilities reads share this merged view; `ViewLocalTtl` may shorten its local lifetime.
- `CapabilitiesTtl` defaults to one year. Each immutable capability revision has its own key and is read again from the store after expiry; `CapabilitiesLocalTtl` may shorten its local lifetime.

Both local lifetimes default to their overall TTL and cannot exceed it. All durations must be positive.
A failed distributed read or write falls back to the registry store; a failed shared invalidation is logged
and the short view TTL bounds staleness. HybridCache does not support an unlimited lifetime safely on
all distributed backends, so revisions use a long finite TTL instead of literal no-expiry.

## Health

| Check | Tags | Means |
|---|---|---|
| `kaleido-registry-store` | `kaleido`, `registry`, `ready` | the store answers: the registry's readiness depends on nothing else |
| `kaleido-registry-completeness` | `kaleido`, `registry` | Degraded while an expected version is missing, a version has no ready instance, or instances have not reported since the registry resumed (never Unhealthy: it keeps serving) |

## The store

`IKaleidoRegistryStore` holds registrations (per service, version and revision, with their
capabilities), instances, each service's current version and the registry's watch; the rules above are
the registry's. Its
writes are atomic on their own, for many instances and several registry nodes at once: a
registration is insert-if-absent, the current version set-if-absent, a heartbeat
(`RenewInstanceAsync`) and a deregistration (`RemoveInstanceAsync`) one conditional statement each,
checking the instance's service against what the caller may register; the watch touch
(`TouchWatchAsync`) one statement too, resuming the watch when no node touched it within the lease; a
sweep is idempotent. Reads
leave the capabilities out (`ReadAsync`); the registry reads each revision on a HybridCache miss
(`GetCapabilitiesAsync`). Choose the store explicitly:
`AddInMemoryRegistryStore()` (development and tests: lost on restart, not shared between registry
instances), or a durable one: `AddSqliteRegistryStore(connectionString)` (`Kaleido.Provider.SQLite`, one node) or your own with `AddRegistryStore<T>()`, tested by deriving from the shared contract suite `IKaleidoRegistryStoreTests`. Without one, `MapKaleidoControlRegistry()`
stops startup (`stores_not_chosen`).

## Where to look

- `ControlRegistry.cs` - registration, heartbeats, deregistration and their trust
- `StoreRegistrySource.cs` - the full registry from the store: instance states, the rolling-deployment
  rule, each version's status (on each capability, and in detail) and what is missing
- `RegistryHybridCache.cs` - the merged view and revision-keyed capabilities through HybridCache, with
  cache lifetimes and key namespace from `KaleidoRegistryCacheOptions`
- `RegistryHousekeeping.cs` - each node's watch touch and the retention sweep
- `RegistryRegistrants.cs` - who may register which service
- `ControlRegistryEndpointRouteBuilderExtensions.cs` - the endpoints
