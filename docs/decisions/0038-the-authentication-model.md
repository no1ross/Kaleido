# 0038. The authentication model: the host authenticates, Kaleido forwards or requires an exchange, ASP.NET authorizes

- **Status:** Accepted
- **Date:** 2026-11-05
- **Related:** #574, #565, #566, #553, #575, ADR 0010, ADR 0022 (superseded in part), ADR 0027 (superseded in part), ADR 0037

## Context
Kaleido carried its own credential seam (`IKaleidoCallCredentialProvider`, `AddCallCredentials<T>()`,
`ForwardCallerToken()`, ADR 0022), its own authorization evaluator next to ASP.NET's, and an Entra-specific
claim (`appid`). Its consumers run behind a gateway (Axway, Apigee, AWS) with any identity provider (Okta,
Entra ID, Keycloak), a UI such as Pega, and AI agents through MCP. Kaleido must never know or assume the
provider, and its rules must be the ones the host already uses.

## Decision
1. **Authentication is the host's.** Each service validates tokens with plain ASP.NET Core (e.g.
   `AddJwtBearer`: the provider's signing keys, issuer, audience, lifetime), locally, and maps its provider's
   claims to standard ones (`IClaimsTransformation` or the JWT handler's mapping). Kaleido reads only `sub`,
   the identity's `RoleClaimType`, `act` and `azp`.
2. **Authorization is ASP.NET's only.** A capability's declaration (`[KaleidoAuthorization]`,
   `DefaultAuthorization`) becomes an ASP.NET `AuthorizationPolicy` (an authenticated user, its roles, its
   named policy), checked by `IAuthorizationService` on `HttpContext.User` on every surface: typed routes,
   generic execute, resumes and discovery. Kaleido evaluates nothing itself; the modes stay (`None`,
   `Authenticated`, `ZeroTrust`). The correlation context's `CallerRoles` and `CallerIsAuthenticated` are
   removed.
3. **Two kinds of call, two kinds of token, which never turn into each other.**
   - A call made **for a caller** (`KaleidoCallPurpose.OnBehalfOfCaller`) carries the caller's token, as
     `Kaleido:OnBehalfOfCalls` says: `Forward` (the default) sends the inbound token unchanged; `Exchange`
     sends the token the host's own handler set for the caller (RFC 8693 token exchange, On-Behalf-Of), never
     the inbound one. An anonymous caller's call carries nothing.
   - A **service's own call** (`ServiceOwn`: registering, heartbeats, registry reads, calls outside a request)
     carries what the host's handlers set, typically an app-only token (client credentials).
4. **The host's handlers are standard `DelegatingHandler`s** on the Kaleido clients
   (`AddKaleidoClientHandler<THandler>()`), from any library. Kaleido exposes each call's context to them,
   read-only (`KaleidoCallContext`: purpose, caller, the inbound token), and its own guard runs last and
   enforces rule 3 whatever they set (`httpclient_caller_token_not_exchanged`).
5. **An MCP host exchanges.** The agent's token is issued for the MCP server, and the MCP authorization
   specification forbids passing it through, so `AddMcp()` requires `Kaleido:OnBehalfOfCalls = Exchange`
   (`mcp_on_behalf_of_calls_not_exchanged`).
6. **`Forward` is the default, `Exchange` an option.** Exchange by default would require every consumer to
   configure token exchange per app at its provider (off by default everywhere; Entra ID has no RFC 8693
   exchange) before a first service could call another.

## Alternatives considered
- **Keep the credential provider seam** (`IKaleidoCallCredentialProvider`): a Kaleido-specific abstraction for
  what standard `DelegatingHandler`s and the existing token libraries already do; it also decided the
  service's own calls, which hosts configure with those libraries anyway.
- **Exchange on every hop by default, the chain in a nested `act`**: provable, but an identity-provider call per
  user, per calling service and per instance on the request path, and not every provider supports delegation.
  The calling chain is recorded by Kaleido instead (#574, a later commit).
- **Two tokens on every service call** (the user's, plus the caller's app token in a second header): no RFC, the
  gateway must pass a custom header, and a replayable app token travels with every call.
- **Mutual TLS between services** to prove the caller: calls go through the gateway, whose TLS ends there.

## Consequences
- Removed (breaking): `IKaleidoCallCredentialProvider`, `KaleidoCallCredential*`, `AddCallCredentials<T>()`,
  `ForwardCallerToken()`, `httpclient_configured_credential_for_caller`, `mcp_call_credentials_not_configured`,
  `KaleidoCorrelationContext.CallerRoles` and `CallerIsAuthenticated`, core's authorization evaluator.
- A host without any handler now forwards the caller's token on a caller's calls (before, it sent the host's
  own client credentials for them).
- The rest of #574 extends this record as it lands: the calling chain, the transport requirement, health
  endpoints and the credential-leak guard.
