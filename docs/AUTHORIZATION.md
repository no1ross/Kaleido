# Authorization

Kaleido never authenticates callers itself — the host owns authentication
(`AddAuthentication`/`UseAuthentication`, JWT, cookies, whatever). Kaleido consumes the
resulting principal via the transport and enforces one gate:

- **Capability authorization** — `KaleidoAuthorizationAttribute` on steps, query sources,
  and views narrows access with `Roles` / `Policy`, or opens it with `AllowAnonymous`.

Kaleido has **no case-level guardrails** ([ADR 0010](./decisions/0010-no-case-level-guardrails.md)):
see [Who may continue a process](#who-may-continue-a-process).

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
[ProcessStep(Version = "1.0.0", DisplayName = "...", Description = "...")]
public sealed record CaptureMemberStep : IProcessStep;           // service default; Authenticated: any logged-in user

[ProcessStep(Version = "1.0.0", DisplayName = "...", Description = "...")]
[KaleidoAuthorization(Roles = "radiology")]
public sealed record ApproveStep : IProcessStep;                 // logged in + role

[ProcessStep(Version = "1.0.0", DisplayName = "...", Description = "...")]
[KaleidoAuthorization(AllowAnonymous = true)]
public sealed record AddItemToCartStep : IProcessStep;           // no login needed
```

When enforcing (`Authenticated` or `ZeroTrust`), requirements are applied to:

- per-step execute endpoints (`/processes/steps/{step}`) and per-source/view query
  endpoints, as endpoint metadata enforced by the host's auth middleware
- the multi-step `POST /processes/execute` handler: every submitted step is checked
  against its own declaration **before anything runs**, and the first denial rejects
  the whole request. Unknown step names and empty requests are checked as undeclared
  (authenticated caller in `Authenticated`, denied in `ZeroTrust`).
- the generic `POST /processes/execute` and `GET /processes/{id}` routes require an
  authenticated caller, unless the processor has an `AllowAnonymous` step (then they
  stay open for anonymous callers and the per-step checks above decide). Anonymous
  callers therefore don't see or reach them on a processor that needs a login
- discovery filtering: `/{service}/registry` (local and aggregate modes) returns only
  capabilities the caller may access; processors whose steps are all filtered out are
  omitted

Denials follow standard ASP.NET Core behavior: the host's authentication scheme
produces the response. Endpoint-metadata denials go through ASP.NET's default
authorization result handler; runtime denials (`KaleidoAuthorizationException`) are
turned into `HttpContext.ChallengeAsync()` (**401**, unauthenticated) or `ForbidAsync()`
(**403**, authenticated but denied) by `ExceptionMiddleware`. So a bearer scheme sends
`WWW-Authenticate`, a cookie scheme redirects, and so on. Kaleido does not replace the
host's authorization result handler and writes no Kaleido body for 401/403.

## Who may continue a process

Kaleido has no case-level guardrails. Whether a user may open or continue a particular
case is a business/data-level authorization concern of the underlying systems. Kaleido's
security role is limited to:

- knowing the caller's identity;
- checking the roles and policies declared on the step or query being called.

So a process may be continued by a different caller than the one who started it — e.g. a
case started through an IVR, taken over by a rep on the phone, finished by the provider in
a portal — subject only to each step's declared roles/policies. A caller the next step does
not admit is still refused (401/403).

`ProcessorContext.CreatedBy` is captured at process creation from
`KaleidoCorrelationContext.Caller` (transport-supplied, post-auth — see
[`CORRELATION.md`](./CORRELATION.md)) as a **tag only** (created by), like `UpdatedBy` and the step tags ([caller tags](./CORRELATION.md#caller-tags-tagging-only)); none is ever
enforced. There is no transfer endpoint.

Keep call identity out of user roles: a service-to-service hop should identify the calling
service with a separate claim (evaluated by a policy), not by adding a role to the
forwarded user.

## Evaluation

`IKaleidoAuthorizationEvaluator` (core, transport-agnostic) evaluates
`AuthorizationMetadata` against the caller fields on
`KaleidoCorrelationContext`, using `KaleidoServiceOptions.AuthorizationMode`.
Declared `Policy` names resolve via a transport-supplied evaluator — HTTP bridges to
`IAuthorizationService`. Transports supply caller identity + policy evaluation; core
owns the rules.
