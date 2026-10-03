# Authorization

Kaleido never authenticates callers itself — the host owns authentication
(`AddAuthentication`/`UseAuthentication`, JWT, cookies, whatever). Kaleido consumes the
resulting principal via the transport and enforces two orthogonal gates:

- **Capability authorization** — `KaleidoAuthorizationAttribute` on steps, contexts,
  and views narrows access with `Roles` / `Policy`, or opens it with `AllowAnonymous`.
- **Process ownership** — `ProcessorContext.Owner`/`OwnerRoles` records who created a
  process; resumable access is restricted to the owner and role-mates.

## The switch: `EnforceAuthorization`

```csharp
services.AddKaleido(configuration, o => o.EnforceAuthorization = true);
```

| | `EnforceAuthorization = false` (default) | `EnforceAuthorization = true` |
|---|---|---|
| no attribute / empty attribute | open | authenticated caller required |
| `Roles = "a,b"` | not enforced | authenticated + any of the roles |
| `Policy = "p"` | not enforced | authenticated + policy |
| `Roles` + `Policy` | not enforced | both (AND) |
| `AllowAnonymous = true` | open | open to anonymous callers |
| process ownership | not enforced | enforced |
| registry filtering | none (full registry) | per caller, same rules |
| route auth metadata | none (hosts without auth keep working) | attached |
| no authentication scheme registered | fine | startup error (`authentication_not_configured`) |

Off is for samples, local development, and hosts without authentication. Production
deployments set it on **every** host, including registry aggregators/routers: a router
filters the cached registry per caller with its own setting, so a router left off
returns everything to everyone.

`AllowAnonymous` cannot be combined with `Roles`/`Policy` — that fails at registration
(`conflicting_authorization`).

## Capability authorization

```csharp
[ProcessStep(Name = "CaptureMember", Version = "1.0.0", ...)]
public sealed record CaptureMemberStep;                          // any logged-in user

[ProcessStep(Name = "Approve", Version = "1.0.0", ...)]
[KaleidoAuthorization(Roles = "radiology")]
public sealed record ApproveStep;                                // logged in + role

[ProcessStep(Name = "AddItemToCart", Version = "1.0.0", ...)]
[KaleidoAuthorization(AllowAnonymous = true)]
public sealed record AddItemToCartStep;                          // no login needed
```

When enforcing, requirements are applied to:

- per-step execute endpoints (`/processes/steps/{step}`) and per-context/view query
  endpoints, as endpoint metadata enforced by the host's auth middleware
- the multi-step `POST /processes/execute` handler: the route carries no auth of its
  own; every submitted step is checked against its own declaration **before anything
  runs**, and the first denial rejects the whole request. Unknown step names and empty
  requests are checked as undeclared (authenticated caller).
- `POST /processes/{id}/transfer`: authenticated caller
- discovery filtering: `/{service}/registry` (local and aggregate modes) returns only
  capabilities the caller may access; processors whose steps are all filtered out are
  omitted

Denials throw `KaleidoAuthorizationException` → **401** unauthenticated / **403**
authenticated-but-denied, and increment `kaleido.endpoint.errors` with
`error_code = "unauthorized" | "forbidden"`.

## Process ownership

`ProcessorContext.Owner`/`OwnerRoles` are captured at process creation from
`KaleidoCorrelationContext.CallerName`/`CallerRoles` (transport-supplied, post-auth —
see [`CORRELATION.md`](./CORRELATION.md)).

Rules (when enforcing):

- **Unowned** (`Owner == null`, created by an `AllowAnonymous` step): any caller holding
  the process id may resume/read until it is claimed.
- **Owned**: the owner (name match) or any caller sharing an `OwnerRoles` entry
  (role-mate) may resume, read state, or transfer.

`POST /{service}/processes/{processId}/transfer` transfers ownership to the caller —
allowed for unowned processes (any authenticated caller) or owned ones (owner /
role-mate). The new `OwnerRoles` snapshot is the caller's current roles. This is how an
anonymous flow (e.g. a cart) is claimed after login.

Because `OwnerRoles` grants role-mate access, keep call identity out of user roles:
a service-to-service hop should identify the calling service with a separate claim
(evaluated by a policy), not by adding a role to the forwarded user.

## Evaluation

`IKaleidoAuthorizationEvaluator` (core, transport-agnostic) evaluates
`AuthorizationMetadata` and ownership against the caller fields on
`KaleidoCorrelationContext`, using `KaleidoServiceOptions.EnforceAuthorization`.
Declared `Policy` names resolve via a transport-supplied evaluator — HTTP bridges to
`IAuthorizationService`. Transports supply caller identity + policy evaluation; core
owns the rules.
