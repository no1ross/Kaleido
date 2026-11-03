# Kaleido.Http.Registry

The control registry (#553): the central registry every Kaleido service registers with and heartbeats
to. It keeps one record per service and version, each instance's status, and each service's current
version, and serves the full registry of every service to the other Kaleido hosts. It makes no outbound
calls; users never read it directly (a router or orchestrator serves them its own registry, filtered).

> **Status:** the registry itself (PR 1 of #553). Version routes, the Kaleido gateway, wording overrides
> and the admin UI follow.

## A registry host

The registry is a Kaleido host with no capabilities of its own:

```csharp
builder.Services.AddKaleido(builder.Configuration, new()
    {
        ServiceName = "registry",
        Assemblies = [typeof(Program).Assembly],
        TypeFilter = _ => false,
        AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust,
    })
    .AddInMemoryStores()        // a Kaleido host chooses its stores; the registry writes no process state
    // Kaleido:Registry:ReaderPolicy names the policy the other hosts' service tokens satisfy (required)
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
| `GET /registry` | a Kaleido host (`ReaderPolicy`) | the registry, in one call (`KaleidoRegistry`): every processor and query source with its overall `Status`, and `Missing`; unfiltered, with an ETag, 304 and `?strict`; 403 `registry_reader_not_allowed` for anyone else |
| `GET /registry/capabilities` | a Kaleido host keeping its copy fresh | the capabilities alone (no statuses), with an ETag that changes only with a deployment; 403 for anyone else |
| `GET /registry/status` | operations, the registry's UI, a host keeping its copy fresh | each service version's status in detail, its instances included (`KaleidoRegistryStatus`), with its own ETag (heartbeat times alone do not change it); 403 for anyone else |

- **Pushed, never polled.** Instances send registrations and heartbeats; the registry never calls an
  instance. An instance whose heartbeats stop is no longer ready once its lease runs out
  (`HeartbeatInterval` x `MissedHeartbeats`, default 30 s), and it is removed after `Retention`
  (default 24 h).
- **One record per service version.** A registration carries the instance's capabilities. Instances of
  the same build share one revision (a hash of the capabilities), stored once.
- **Rolling deployments.** A version publishes only the steps and query sources every ready instance's
  revision shares, as the longest-running revision defines them.
- **One status per capability.** Every processor and query source carries its service version's overall
  status (`RegistryCapabilityStatus`), so a consumer - MCP, a router serving its users - knows what is
  working without reading any instance:

  | Status | Meaning |
  |---|---|
  | `Available` | every known instance is ready |
  | `Degraded` | some instances are ready, but not all (one failed, a rollout is under way): calls work |
  | `Unknown` | none is ready yet, some have not reported since the registry resumed after it was down |
  | `Unavailable` | none is ready (the version stays listed, with its capabilities): calls will fail |

  The instances themselves (`Ready`, `NotReady`, `Unknown`; last heartbeat; address) are on
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
- **Who may register.** `Kaleido:Registry:Registrants` maps each service to the client ids allowed to
  register it, compared with the caller's actor (`act`, `azp` or `appid`), else its subject. Not checked
  when `AuthorizationMode` is `None`.
- **Who reads it.** Only callers satisfying `Kaleido:Registry:ReaderPolicy` - the other Kaleido hosts
  (services, MCP, the gateway) - read it, and they get all of it, unfiltered: each keeps the full
  registry in memory and filters it for its own callers. The policy is required unless
  `AuthorizationMode` is `None`.
- **One call to consume, two to sync.** A consumer reads `GET /registry`: everything in one call. The
  statuses change whenever an instance starts, stops or fails, the capabilities only with a deployment,
  so a Kaleido host keeping its copy fresh checks `/registry/capabilities` and `/registry/status`
  apart, each with its own ETag, and composes the same registry from them: a few hundred hosts
  download the capabilities again only when they change.
- **Completeness.** `Kaleido:Registry:Expected` lists the processor versions the environment needs; one
  not registered, or a service a published step looks up and nobody registered, is listed in `Missing`
  and makes the registry partial (`?strict` answers 502).

## Settings (`Kaleido:Registry`)

`KaleidoRegistryOptions`: `Registrants`, `ReaderPolicy`, `Expected`, `HeartbeatInterval` (10 s),
`MissedHeartbeats` (3), `Retention` (24 h). An invalid value stops startup (`invalid_options`).

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
leave the capabilities out (`ReadAsync`); the registry reads each revision's once
(`GetCapabilitiesAsync`), since a revision never changes. Choose it explicitly:
`AddInMemoryRegistryStore()` (development and tests: lost on restart, not shared between registry
instances), or a durable one: `AddSqliteRegistryStore(connectionString)` (`Kaleido.Provider.SQLite`, one node) or your own with `AddRegistryStore<T>()`, tested by deriving from the shared contract suite `IKaleidoRegistryStoreTests`. Without one, `MapKaleidoControlRegistry()`
stops startup (`stores_not_chosen`).

## Where to look

- `ControlRegistry.cs` - registration, heartbeats, deregistration and their trust
- `StoreRegistrySource.cs` - the full registry from the store: instance states, the rolling-deployment
  rule, each version's status (on each capability, and in detail) and what is missing; each revision's
  capabilities read once, one view shared for a second
- `RegistryHousekeeping.cs` - each node's watch touch and the retention sweep
- `RegistryRegistrants.cs` - who may register which service
- `ControlRegistryEndpointRouteBuilderExtensions.cs` - the endpoints
