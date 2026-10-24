# 0027. `ForwardCallerToken()` stays, as a stated single-audience choice; credentials over plain HTTP are reported; route prefixes are service names

- **Status:** Accepted
- **Date:** 2026-10-24
- **Related:** #434, #430, ADR 0022

## Context
ADR 0022 made one host-owned provider decide every outgoing call's credential and added the built-in
`ForwardCallerToken()` for deployments where every service accepts the caller's token (one audience).
It also noted that MCP's authorization rules forbid passing a token issued for the MCP endpoint on to
other services. The pre-1.0 review (#430) found three gaps around that choice:
- The README's MCP router quick start used `ForwardCallerToken()` without saying that, for an
  OAuth-protected MCP endpoint, passing the MCP token through is exactly what the MCP authorization
  specification forbids.
- Nothing checked the transport: with `ForwardCallerToken()` (or any provider), a client whose
  `Kaleido:Clients:{name}:BaseUrl` is `http://` on a remote host sends the caller's credential in clear
  text.
- `RoutePrefix` was not validated, although every Kaleido route is `/{ServiceName}/...`: a value that
  is not a service name can only produce failed calls, and `..` or `/` would leave the prefix.

## Decision
1. **`ForwardCallerToken()` stays.** It is the right tool for a deployment whose services deliberately
   share one audience (for example an internal router and its services behind one identity provider).
   Its documentation, the client and MCP READMEs and the root README state the caveat: for an
   OAuth-protected MCP endpoint that follows the MCP authorization specification, register
   `AddCallCredentials<TProvider>()` with a provider that obtains a token for each service (for example
   OAuth on-behalf-of), not `ForwardCallerToken()`. Kaleido does not try to detect which deployment a
   host is.
2. **Credentials over plain HTTP are reported, not refused.** When a call-credential provider is
   registered, each configured client whose base URL is neither `https` nor loopback is logged once at
   startup as a Warning (`KaleidoClientTransportCheck`, an `IHostedService`: the provider can be
   registered after `AddHttpClients()`, and registration has no logger). A private network or a
   TLS-terminating sidecar can make such a URL deliberate, so it is not a startup error.
3. **A route prefix is a service name.** `AddHttpClients()` refuses a client whose `RoutePrefix` (or,
   without one, its lowercased client name) does not match the service-name rule
   `[a-z0-9][a-z0-9_-]*`, with `httpclient_invalid_route_prefix`.

## Alternatives considered
- **Refuse `ForwardCallerToken()` with `AddMcp()`.** Strictest, but it removes a legitimate
  single-audience setup and breaks the router quick start; the decision is the host's (ADR 0022).
- **Fail startup on a non-HTTPS base URL.** Would break local and sidecar setups that are deliberate;
  a warning makes the risk visible without taking that decision for the host.
- **Warn on first use** (when a client's handler pipeline is built). No extra service, but the warning
  arrives on the first call instead of at startup and repeats on handler rotation.

## Consequences
- Breaking (preview): a configured client whose route prefix is not a service name fails at startup.
- Hosts with call credentials and plain-HTTP remote clients see a startup warning per client.
- ADR 0022 is amended (its decision stands): its point 4 now carries the OAuth MCP caveat above.
