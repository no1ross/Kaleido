# 0051. MCP uses the host's Kaleido service identity

- **Status:** Accepted
- **Date:** 2026-11-20
- **Related:** #585, #625, #463; ADR 0030, ADR 0043, ADR 0045

## Context

`AddKaleido()` already validates one `ServiceName` and one opaque `ServiceVersion` for a host. The older MCP adapter separately accepted `Kaleido:Mcp:ServerName` and `ServerVersion`; the latter defaulted to the entry assembly's informational version. An MCP host could therefore report a name and version unrelated to the same host's Kaleido identity. Registry publication has a distinct `BuildVersion` for the deployed application, so the service version need not acquire a second release/build meaning.

## Decision

1. The MCP `initialize` response reports `builder.ServiceOptions.ServiceName` and `ServiceVersion` as its server name and version. The latter is the exact opaque label validated by `AddKaleido()`, not the host assembly's build version or Kaleido's package version. Remove `KaleidoMcpOptions.ServerName` and `ServerVersion` and the assembly-version resolver. Explicit obsolete `Kaleido:Mcp:ServerName`/`ServerVersion` settings fail startup with `mcp_invalid_options` and point to `Kaleido:ServiceName`/`ServiceVersion` instead of being silently ignored.
2. `KaleidoMcpOptions` follows the shared `IKaleidoOptions` / `builder.BindOptions()` startup contract. Configuration overlays a copy of the host's code options; validation and a snapshot of the `ListedTools` collection run before registration. MCP-specific instructions, elicitation, listing and confirmation settings remain in `Kaleido:Mcp`.
3. Registry `BuildVersion` remains a separate build identity derived from the participant's entry assembly when it registers. MCP introduces no redundant build-version setting or protocol metadata field. A future need to expose build identity must name and define it separately, not reinterpret `ServiceVersion`.

## Alternatives considered

- Keep separate MCP branding and release-version overrides: that gives one host two meanings for the same identity and can drift silently during configuration changes.
- Report the Registry `BuildVersion` as MCP's protocol version: an application build can change without changing its service contract or its version-pinned Process state.
- Silently ignore the old `Kaleido:Mcp` identity keys: a deployment could appear to use its former configuration while reporting something else.
- Add an MCP-only build-version setting now: the current protocol initialization needs only the host's name and version, and the Registry already records builds where they matter.

## Consequences

This is a preview MCP API/configuration change. Reader-only and capability-hosted MCP endpoints report their `AddKaleido()` identity consistently. The Registry's service-version routing and independent build-version publications do not change, and MCP's caller-filtered discovery, exchanged credentials and Process continuation rules remain separate concerns.
