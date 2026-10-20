# 0022. One credential model for every outgoing call, and provenance behind a front door

- **Status:** Accepted
- **Date:** 2026-10-20
- **Related:** #287, #402, #312, ADR 0010, ADR 0021

## Context
A Kaleido host calls other Kaleido services through the Kaleido clients. Every hop answers the same
question: "I received a request from alice; what credential goes on my call to service Y?" Before
this decision two hops answered it differently:

| | Router -> service (MCP tool call) | Service -> service (handoff, lookup) |
|---|---|---|
| Decided by | Kaleido: a `ForwardCaller` scope (router only) and a handler added last | the host's own client handler |
| Sent | the caller's inbound token, unchanged, to every service | whatever the host minted (the sample: an on-behalf-of token naming the calling service) |
| Anonymous caller | stays anonymous | fell back to the service's own token |
| Host token exchange | impossible: Kaleido overwrote it | possible |

The router path existed so an AI tool call never runs with the router's own service identity. But it
sent a token issued for the MCP endpoint to every service (MCP's authorization rules forbid passing an
inbound token on), and it made per-service audiences impossible. The service path let a host
exchange tokens, but upgraded an anonymous caller to the service's own identity.

## Decision
1. **One seam for every Kaleido client call, in every host:** `IKaleidoCallCredentialProvider`
   (`Kaleido.Http.Client`). Kaleido asks it once per outgoing request with the destination service,
   the caller (principal and presented credential) and the purpose:
   - `OnBehalfOfCaller` - the call is made while handling an inbound request (an MCP tool call, a
     step handler's handoff or lookup, a delegated query);
   - `ServiceOwn` - a registry fetch or refresh (shared by every caller), a health probe, or any call
     outside an inbound request.
2. **The provider decides** - `Header(value)`, `None`, or, for the service's own calls only,
   `Configured` (keep what the host's client handlers set). It runs after every host handler, so its
   answer is the one sent.
3. **Kaleido enforces one rule for every hop: a call made for a caller never goes out with the host's
   own credentials.** An anonymous caller is sent anonymously without asking the provider;
   `Configured` for a caller's call is refused; a provider exception fails the call (no fallback).
4. **Built-in `ForwardCallerToken()`**: a caller's call sends the caller's token unchanged; the
   service's own calls keep the configured credentials. For deployments where every service accepts
   the caller's token (one audience).
5. **Fail closed for MCP:** `AddMcp()` without a registered provider fails at startup
   (`mcp_call_credentials_not_configured`). A host without the MCP adapter and without a provider keeps
   its own client handlers, as before.
6. The transport records the inbound caller (principal and presented credential) for the outgoing
   clients; the credential is never logged and never on events.
7. **Provenance behind a front door (#402)** - two signals of different strength:
   - **Origin, informational, end to end:** `Origin` / `OriginTool` (`X-Kaleido-Origin` /
     `X-Kaleido-Origin-Tool`). The MCP adapter stamps `mcp` and the tool name on its tool calls;
     every Kaleido client forwards them like the request id; services read them under the same trust
     as the request id. Any caller can send them, so they are an audit hint, never an authorization
     input.
   - **Actor, trusted, per hop:** `CallerActor`, read by the transport from the authenticated
     principal - the `act` claim (its `sub` when it is an object), else `azp`, else `appid` -
     never from a header. Each service reads its own caller's token, so with on-behalf-of tokens the
     actor names the calling service on each hop.
   - Both are recorded on the correlation context, on `ProcessEventContext` and
     `QueryableEventContext`, and as span tags (`kaleido.origin`, `kaleido.origin.tool`,
     `kaleido.caller.actor`). Process state is unchanged.

## Alternatives considered
- **Kaleido steps out** (delete the forwarding; hosts configure standard .NET handlers such as header
  propagation or Microsoft.Identity.Web's on-behalf-of handler). No new API, but Kaleido could no
  longer guarantee that a tool call never runs with the host's service identity. Rejected.
- **A seam for the router hop only**, leaving service-to-service hops to host handlers. Two models
  that disagree on anonymous callers. Rejected for one model.
- **Pass-through by default** when no provider is registered. Keeps a token-broadcasting default for
  MCP hosts. Rejected: the choice is one line, made explicitly.

## Consequences
- Breaking (preview): an MCP host registers `ForwardCallerToken()` or its own provider; with a
  provider, an anonymous caller is no longer upgraded to a service token on downstream calls.
- The PriorAuth services use one provider (an on-behalf-of dev token naming the calling service as
  actor; a service token for their own calls). The router uses `ForwardCallerToken()`: an actor on
  agent calls would satisfy the services' internal-caller policy.
- Token caching is the provider's concern; it is called on every outgoing request.
- A consumer of the event stream can tell which front door and tool started a request (origin) and
  which application acted with the token's authority (actor); only the actor is evidence. The
  PriorAuth sample's dev tokens use the standard `act` claim for the calling service.
