# Authorization

Kaleido never authenticates callers itself — the host owns authentication
(`AddAuthentication`/`UseAuthentication`, JWT, cookies, whatever). Kaleido consumes the
resulting principal via the transport and enforces two orthogonal gates:

- **Capability authorization** — `KaleidoAuthorizationAttribute` on steps, contexts,
  and views declares `Roles` / `Policy` requirements per capability.
- **Process ownership** — `ProcessorContext.Owner`/`OwnerRoles` records who created a
  process; resumable access is restricted to the owner and role-mates.

## Capability authorization

```csharp
[ProcessStep(Name = "CaptureMember", Version = "1.0.0", ...)]
[KaleidoAuthorization(Roles = "intake")]
public sealed record CaptureMemberStep;
```

At map time, declared requirements become endpoint `RequireAuthorization` metadata —
enforced by the host's auth middleware. Enforcement covers:

- per-step execute endpoints (`/processes/steps/{step}/execute`)
- per-context query + metadata endpoints
- the multi-step `POST /processes/execute` handler, which authorizes each submitted
  step inside the request (endpoint metadata can't express per-item requirements)
- discovery filtering: catalogs, registries, `MapRegistry()` aggregates, and context
  metadata return only capabilities the caller may access

`KaleidoHttpOptions.RequireAuthorization` additionally requires an *authenticated*
caller for any capability without a `[KaleidoAuthorization]` declaration (off by
default).

Denials throw `KaleidoAuthorizationException` → **401** unauthenticated / **403**
authenticated-but-denied, and increment `kaleido.endpoint.errors` with
`error_code = "unauthorized" | "forbidden"`.

## Process ownership

`ProcessorContext.Owner`/`OwnerRoles` are captured at process creation from
`KaleidoCorrelationContext.CallerName`/`CallerRoles` (transport-supplied, post-auth —
see [`CORRELATION.md`](./CORRELATION.md)).

Rules:

- **Unowned** (`Owner == null`, created anonymously): any caller may resume/read.
- **Owned**: the owner (name match) or any caller sharing an `OwnerRoles` entry
  (role-mate) may resume, read state, or transfer.
- `KaleidoHttpOptions.RequireProcessOwnership` requires an authenticated caller to
  *create* a process, so every process is owned (401 otherwise).

`POST /{service}/processes/{processId}/transfer` transfers ownership to the caller —
allowed for unowned processes (any authenticated caller) or owned ones (owner /
role-mate). The new `OwnerRoles` snapshot is the caller's current roles.

## Evaluation

`IKaleidoAuthorizationEvaluator` (core, transport-agnostic) evaluates
`AuthorizationMetadata` and ownership against the caller fields on
`KaleidoCorrelationContext`. Declared `Policy` names resolve via a transport-supplied
evaluator — HTTP bridges to `IAuthorizationService`. Transports supply caller identity
+ policy evaluation; core owns the rules.
