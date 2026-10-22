# 0018. Options are startup inputs; no defensive copies and no deep-immutability analyzer

- **Status:** Accepted
- **Date:** 2026-10-19
- **Related:** #294, #305, #284

## Context
The pre-1.0 review raised two related questions:

- **#294 (CLN-010):** one source asked that later changes to configure-time objects never reach the
  running framework. An audit found most options already end up as snapshots
  (`KaleidoServiceOptions`, `KaleidoMcpOptions`, the per-client resilience settings). Three places
  keep the object the host configured: `AddHttp` registers the `KaleidoHttpOptions` passed to its
  callback; the registry endpoint reads `KaleidoHttpMapOptions.RegistryCacheTtl` per request; and
  the OpenTelemetry wiring keeps `KaleidoOpenTelemetryOptions` for delegates the SDK runs later.
- **#305 (CLN-018):** one source treated deep immutability of public objects as a requirement and
  asked for a stronger analyzer than KAL0015 (no mutable collections in the public API); another
  saw no justification for one.

Only code that keeps a reference to an options object it configured, and changes it after startup,
is affected. That is the host's own code; there is no trust boundary between it and the framework.

## Decision
1. **Options are startup inputs.** Every Kaleido options object is configured during composition
   (configuration or the configure callback of `AddKaleido`, `AddHttp`, `MapKaleidoHttp`,
   `AddHttpClients`, `AddMcp`, `AddOpenTelemetry`). Changing it afterwards is unsupported and its
   effect undefined. Kaleido does not copy options defensively. The rule is documented in
   `docs/PATTERNS.md`.
2. **The builders stay for what they do.** `KaleidoServiceOptionsBuilder` and the MCP options builder
   validate and normalize (service name, assemblies, roles, tool names) when they build the options;
   that is why they exist, not to defend against mutation.
3. **Deep immutability is not a requirement.** KAL0015 (no mutable collections in the public API)
   stays the rule. No transitive-immutability specification or analyzer is added.

## Alternatives considered
- **Hand-written copies (`Copy()`/`MemberwiseClone`) for each options type.** They defend the host
  against its own code, must be remembered for every new property, and a shallow copy silently stops
  being a copy when a collection is added. Rejected.
- **Move to the Microsoft.Extensions.Options pattern** (`AddOptions<T>().Bind().ValidateOnStart()`,
  `IOptions<T>`). Idiomatic and gives startup validation and a reload path, but it is a refactor of
  five packages and does not make anything immutable either. Not needed now; revisit if validation
  or reload is asked for.
- **A deep-immutability analyzer.** Needs a versioned specification and a classifier with known
  approximation limits, for a guarantee no consumer has asked for. Rejected.

## Consequences
- No source change: the three places above stay as they are, covered by the documented rule.
- #294 and #305 are resolved by this record; neither blocks the release (#284).
- A future need for validated or reloadable options is a new decision (the Options pattern above).
