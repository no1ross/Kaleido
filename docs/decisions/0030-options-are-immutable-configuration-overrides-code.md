# 0030. Options are immutable; configuration overrides code; Kaleido never changes a supplied value

- **Status:** Accepted
- **Date:** 2026-10-26
- **Supersedes in part:** ADR 0018 (decisions 1 and 2)
- **Related:** #445, #430, ADR 0018

## Context
ADR 0018 kept the options types mutable and documented "don't change them after startup". Several
were registered as the very object the host configured (`KaleidoHttpOptions`, the event options
inside `KaleidoServiceOptions`), and two builders (`KaleidoServiceOptionsBuilder`,
`KaleidoMcpOptionsBuilder`) existed only so a configure lambda could assign properties. The builders
also *normalized* what the host supplied: a blank MCP server name became `kaleido`, listed tool
names were trimmed and de-duplicated. HTTP and client settings were not validated at all, so a zero
lease or a negative timeout failed at the first request. The precedence was "configuration, then the
configure lambda": code silently won over the environment's configuration.

## Decision
1. **Every options type is an immutable record** (`get; init;`, defaults as property initializers):
   `KaleidoServiceOptions`, `KaleidoEventOptions`, `KaleidoResilienceSettings`, `KaleidoHttpOptions`,
   `KaleidoHttpMapOptions`, `KaleidoClientOptions`/`KaleidoClientEntry`/`KaleidoResilienceOptions`,
   `KaleidoOpenTelemetryOptions`, `KaleidoMcpOptions`. The two builders are removed.
2. **Registration takes an optional options object** built with an object initializer, never an
   `Action`: `AddKaleido(configuration, new() { ... })`, `AddHttp(new() { ... })`,
   `MapKaleidoHttp(new() { ... })`, `AddOpenTelemetry(new() { ... })`, `AddMcp(new() { ... })`.
   Leaving it out means the defaults; an initializer names only what differs from them.
3. **Precedence, lowest to highest: Kaleido's defaults, code (Program.cs), configuration.** Each
   registration lays its configuration section over a *copy* of the object from code (`Kaleido`,
   `Kaleido:Http`, `Kaleido:Http:Map`, `Kaleido:Clients`/`Kaleido:Resilience`, `Kaleido:OpenTelemetry`,
   `Kaleido:Mcp`): a setting the section names replaces the code value, every other setting keeps it.
   Configuration overrides code so each environment can change a setting without a code change.
   Settings only code can give (`Assemblies`, `TypeFilter`, `TrustCorrelationIdentity`) come from code.
   The overlay copies nested records, replaces (never appends to) collections, and never changes the
   host's instance; the plain configuration binder does neither.
4. **Kaleido never changes a value the host gave.** Nothing is trimmed, clamped or substituted. A
   blank, out-of-range, conflicting or unreadable value stops startup with a
   `KaleidoConfigurationException` naming the setting's path, with an area-owned code
   (`invalid_options`, `invalid_event_options`, `invalid_http_options`, `httpclient_invalid_options`,
   `otel_invalid_options`, `mcp_invalid_options`). Defaults apply only to settings neither code nor
   configuration gave.
5. **What is derived from options lives in its own object.** The options keep what the host supplied;
   for example the MCP instructions resolved from `Instructions`/`InstructionsFile`/the packaged text
   and `AdditionalInstructions` are an internal execution object, resolved once by `AddMcp`.

ADR 0018's decision 3 stands: KAL0015 remains the immutability rule for public collections; no
deep-immutability analyzer is added.

## Alternatives considered
- **Keep the builders** (mutable twins bound and configured, then built into a snapshot). Two types
  per options set, and the builder's normalizing silently changed what the host wrote. Rejected.
- **A `Func<TOptions, TOptions>` delegate** (`o => o with { ... }`). Works, but an object initializer
  is plainer, and nothing is lost: nothing is bound before code runs any more.
- **Code overrides configuration** (today's order). The environment could not change a setting a
  developer set in code. Rejected.
- **A final `UseConfiguration()` call** that applies all configuration at the end. Options are used
  when each `Add*` runs (assembly scanning, named clients, the OpenTelemetry resource, the MCP
  instructions), so it would need deferred registration in every package, an order that cannot be
  enforced, and silent loss of configuration when forgotten. Rejected.
- **Microsoft.Extensions.Options** (`IOptions<T>`, `ValidateOnStart`). A larger refactor of every
  consumer of the options, for a reload path Kaleido does not support. Rejected.

## Consequences
- Breaking (preview): configure lambdas become options objects; `KaleidoServiceOptionsBuilder` and
  `KaleidoMcpOptionsBuilder` are gone; configuration now wins over code; blanks and out-of-range
  values that were accepted (or replaced) fail at startup.
- KAL2006 reads the options object passed to `AddKaleido`; KAL2005/KAL2017 read the object initializer.
- `KaleidoOptionsConfigurationExtensions.BindOver` (internal) is the one overlay every package uses.
