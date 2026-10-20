# 0021. Authorization is enforced by default: an open host is an explicit choice

- **Status:** Accepted
- **Date:** 2026-10-20
- **Related:** #288, #284, ADR 0010

## Context
`KaleidoServiceOptions.AuthorizationMode` defaulted to `None`: a host that never chose a mode
published every capability without any authorization, and the only signal was a startup Warning.
The pre-1.0 review rated this a release blocker: a service could ship open without anyone deciding
it should be.

## Decision
- **The default is `Authenticated`.** A host that sets no mode (in code or as
  `Kaleido:AuthorizationMode`) requires an authenticated caller for every capability; undeclared
  capabilities are open to any authenticated caller, `[KaleidoAuthorization]` narrows or opens them
  as before.
- **No authentication, no start.** With an enforcing mode and no authentication scheme registered,
  the host fails at startup with `authentication_not_configured`; the message names the two ways
  out (register authentication, or set `AuthorizationMode = None`).
- **Open is explicit.** `None` stays for samples, local development and hosts without
  authentication, but a host must state it.
- Capability authorization (Kaleido) and authentication (the host's ASP.NET Core schemes) stay
  separate; Kaleido still authenticates nothing itself.

## Alternatives considered
- **Reject an omitted mode at startup** (no default at all). Equally safe, but every host - even
  one with authentication that wants the common `Authenticated` behavior - must restate it.
  Rejected for more churn with no extra safety.
- **Keep `None` and warn louder.** Leaves the unsafe outcome as the default. Rejected.
- **Default to `ZeroTrust`.** Safest, but omits every undeclared capability; a host adding
  authentication would see its capabilities disappear until each declares a rule. Kept as the
  recommended production mode, not the default.

## Consequences
- Breaking for hosts that relied on the open default: they now fail at startup (no authentication)
  or require an authenticated caller. Each adds `AuthorizationMode = None` or authentication. The
  in-repo samples and test hosts without authentication now set `None` explicitly.
- The enum's numeric values are unchanged (`None = 0`); only the options' default changes.
