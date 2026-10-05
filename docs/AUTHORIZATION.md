# Authorization

Kaleido never authenticates callers itself — the host owns authentication
(`AddAuthentication`/`UseAuthentication`, JWT, cookies, whatever). Kaleido consumes the
resulting principal via the transport and enforces two orthogonal gates:

- **Capability authorization** — `KaleidoAuthorizationAttribute` on steps, contexts,
  and views narrows access with `Roles` / `Policy`, or opens it with `AllowAnonymous`.
- **Process ownership** — `ProcessorContext.Owner`/`OwnerRoles` records who created a
  process; resumable access is restricted to the owner and role-mates.

## The mode: `AuthorizationMode`

```csharp
services.AddKaleido(configuration, o =>
{
    o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust;
    o.DefaultAuthorization = new AuthorizationMetadata("staff", []); // optional service default
});
```

Both can also be bound from configuration (`Kaleido:AuthorizationMode`,
`Kaleido:DefaultAuthorization:Policy`, `Kaleido:DefaultAuthorization:Roles:0`, …).

| | `None` (default) | `Authenticated` | `ZeroTrust` |
|---|---|---|---|
| no rule (no attribute and no service default, or an empty attribute) | open | any authenticated caller | omitted from discovery and endpoint mapping; denied in generic execute |
| `Roles = "a,b"` | not enforced | authenticated + any of the roles | authenticated + any of the roles |
| `Policy = "p"` | not enforced | authenticated + policy | authenticated + policy |
| `Roles` + `Policy` | not enforced | both (AND) | both (AND) |
| `AllowAnonymous = true` | open | open to anonymous callers | open to anonymous callers |
| process ownership | not enforced | enforced | enforced |
| registry filtering | none (full registry) | per caller, same rules | per caller, same rules |
| route auth metadata | none (hosts without auth keep working) | attached | attached |
| no authentication scheme registered | not checked | startup error (`authentication_not_configured`) | startup error (`authentication_not_configured`) |

**Effective rule.** A capability's `[KaleidoAuthorization]` attribute, when present,
**replaces** `DefaultAuthorization` entirely (it is not merged). Without an attribute the
service default applies. An empty attribute (no `Roles`, `Policy` or `AllowAnonymous`)
overrides the default with "no rule".

`None` is for samples, local development, and hosts without authentication; a startup
warning is logged. `Authenticated` treats undeclared capabilities as "any logged-in
caller". `ZeroTrust` exposes nothing without an explicit rule and logs a startup warning
listing the omitted capabilities. Production deployments use `ZeroTrust` on **every**
host, including registry aggregators/routers: a router filters the cached registry per
caller with its own mode, so a router left at `None` returns everything to everyone.

`AllowAnonymous` cannot be combined with `Roles`/`Policy` — on an attribute or on
`DefaultAuthorization` — that fails at registration (`conflicting_authorization`).

## Capability authorization

```csharp
[ProcessStep(Name = "CaptureMember", Version = "1.0.0", ...)]
public sealed record CaptureMemberStep;                          // service default; Authenticated: any logged-in user

[ProcessStep(Name = "Approve", Version = "1.0.0", ...)]
[KaleidoAuthorization(Roles = "radiology")]
public sealed record ApproveStep;                                // logged in + role

[ProcessStep(Name = "AddItemToCart", Version = "1.0.0", ...)]
[KaleidoAuthorization(AllowAnonymous = true)]
public sealed record AddItemToCartStep;                          // no login needed
```

When enforcing (`Authenticated` or `ZeroTrust`), requirements are applied to:

- per-step execute endpoints (`/processes/steps/{step}`) and per-context/view query
  endpoints, as endpoint metadata enforced by the host's auth middleware
- the multi-step `POST /processes/execute` handler: the route carries no auth of its
  own; every submitted step is checked against its own declaration **before anything
  runs**, and the first denial rejects the whole request. Unknown step names and empty
  requests are checked as undeclared (authenticated caller in `Authenticated`, denied in
  `ZeroTrust`).
- `POST /processes/{id}/transfer`: authenticated caller
- discovery filtering: `/{service}/registry` (local and aggregate modes) returns only
  capabilities the caller may access; processors whose steps are all filtered out are
  omitted

Denials throw `KaleidoAuthorizationException` → **401** unauthenticated / **403**
authenticated-but-denied, and increment `kaleido.http.endpoint_errors` with
`kaleido.error.code = "unauthorized" | "forbidden"`.

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
`KaleidoCorrelationContext`, using `KaleidoServiceOptions.AuthorizationMode`.
Declared `Policy` names resolve via a transport-supplied evaluator — HTTP bridges to
`IAuthorizationService`. Transports supply caller identity + policy evaluation; core
owns the rules.
