# 0052. Use vetted dependency lines that support net8.0

- **Status:** Accepted
- **Date:** 2026-11-25
- **Related:** #630, #585, #579; ADR 0035

## Context

ADR 0035 keeps Kaleido's libraries on `net8.0` and originally leaves their Microsoft dependencies on 8.x, expecting consumers to override them after .NET 8's end of support. The build now uses the .NET 10 SDK, but changing the libraries' target framework remains the separate #579 decision. The maintainer chose to retain `net8.0` while modernizing compatible package lines.

NuGet's .NET Standard fallback alone is not a support promise. Microsoft.Extensions 11.0.0 packages restore on `net8.0`, but their transitive build targets warn that this target is unsupported and untested. SourceLink 11.0.100 pulls System.IO.Hashing 11 with the same warning. ASP.NET Core and EF Core 11.x require `net11.0`; Microsoft.Data.Sqlite 11 requires `net10.0`. Suppressing target-framework warnings would conceal unsupported dependencies and violate the zero-warning build gate.

## Decision

1. Keep `net8.0` as the shipped target and require a supported runtime for deployments. Pin Microsoft.Extensions packages with supported `net8.0` assets to the vetted 10.0.15 runtime line and the independent dotnet/extensions packages (HybridCache, Compliance, HTTP resilience and test-time clock) to 10.11.0. Keep ASP.NET Core, EF Core, their health-check/test integrations and Microsoft.Data.Sqlite on the patched 8.0.34 line. Analyzer-test Workspaces 4.11 remains separate from the shipped analyzer's Roslyn 4.8 minimum compiler.
2. Require a published age of at least seven days for new direct pins. The available dotnet/extensions 10.11.1 and 11.0.1 releases and Meziantou 3.0.342 are too recent at this checkpoint; 10.11.0, Microsoft.Extensions 10.0.15 and Roslynator 5.0.1 have passed that age. Keep the existing Moq security pin and Dependabot cooldown/major-version ignore unchanged. Never use a floating version range or bypass the target-framework support warnings.
3. Defer OpenTelemetry 1.21 while its .NET Standard fallback pulls Microsoft.Extensions 11 into `net8.0` hosts; retain the coherent 1.20.1 set. Defer ModelContextProtocol.AspNetCore 2.4 because its `ContentBlock.Converter` fails .NET 8 tool-call results; retain 2.3. Defer SourceLink 11.0.100 because its transitive System.IO.Hashing 11 warns on `net8.0`; retain 10.0.404. Reassess these versions when they offer a supported graph or #579 changes the target framework.
4. Prove the dependency graph in a clean `net8.0` package consumer, all project tests and the merged shipped-source coverage gate. Tests of background scheduling wait until their fake timers have been registered before advancing time. Tests with a SQLite authority stop its host before deleting its database; neither changes production scheduling or persistence behavior.

## Alternatives considered

- Move all direct and transitive Microsoft packages to 11.x while keeping `net8.0`: NuGet restore succeeds for some .NET Standard fallbacks, but build-time support warnings and net11-only ASP.NET Core/EF packages make the resulting support claim false.
- Suppress `SuppressTfmSupportBuildWarnings` or lower the warning gate: this hides an unsupported runtime combination rather than fixing it.
- Retarget or multi-target `net10.0`/`net11.0`: changes the consumer/runtime promise and package matrix; #579 owns that decision, not this dependency update.
- Leave all dependencies on 8.x: misses vetted, supported net8-compatible updates without making the runtime itself supported again.

## Consequences

This supersedes only ADR 0035's blanket 8.x dependency/consumer-override guidance. Its `net8.0` target choice, runtime support window and the separate #579 target-framework decision remain. A consumer still runs Kaleido on a Microsoft-supported runtime and may choose newer versions only when those versions support its target and pass its own compatibility checks. This choice does not make an end-of-life .NET 8 runtime secure.
