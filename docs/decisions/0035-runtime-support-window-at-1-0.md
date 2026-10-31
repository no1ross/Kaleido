# 0035. 1.0 targets net8.0, with a stated support window and override guidance

- **Status:** Accepted
- **Date:** 2026-10-31
- **Related:** #528, #424, #533, #508

## Context
The packages target `net8.0` only. Their dependencies are on the 8.x lines: `Microsoft.Extensions.*`
8.x, `Microsoft.EntityFrameworkCore.Sqlite` 8.0.x (the SQLite provider), and the matching health-check
and resilience packages. The repository builds with the .NET 9 SDK, the organization's standard SDK
(#424).

Microsoft's [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
ends support for .NET 8 (LTS) and .NET 9 (STS, now supported for 24 months) on **10 November 2026**.
After that date those runtimes, and the 8.x package lines Kaleido depends on, get no more security
patches. 1.0 would ship on or near that date.

A `net8.0` library runs on .NET 10, but a consumer restores the dependency versions the package asks
for (8.x) unless it overrides them. Dependabot ignores semver-major NuGet updates, so nothing moves
those lines on its own.

## Decision
Option (b) of #528 (owner decision, 2026-10-30):
- **1.0 targets `net8.0` only.** A service runs on .NET 8 or later; run it on a supported runtime
  (.NET 10 after 10 November 2026).
- **The support window is stated** in [VERSIONING](../VERSIONING.md#runtime) and
  [SUPPORT](../../SUPPORT.md): Kaleido supports a runtime while Microsoft does. Kaleido 1.x keeps
  working on .NET 8, but a service on .NET 8 after its end of support runs on an unpatched runtime.
- **Consumers override the 8.x dependencies** to supported, patched versions in their own project
  (a direct `PackageReference`, or central package management). NuGet takes the higher version, and a
  newer version of these packages that still ships a `net8.0` build works with Kaleido's `net8.0`
  assemblies. Check the target frameworks of the version you choose: some newer majors (EF Core 10,
  for example) need a newer runtime.
- **The Dependabot policy is unchanged:** it still ignores semver-major NuGet versions. Moving
  Kaleido's own dependency lines, or its target framework, is a maintainer decision with its own issue,
  as before.
- **Multi-targeting waits for the SDK.** The organization's .NET 9 SDK cannot build `net10.0`; adding
  that target (option (a)) is revisited when the SDK changes (#424).

## Alternatives considered
- **(a) Multi-target `net8.0` and `net10.0`,** with dependency versions per target. This gives patched
  dependencies by default on .NET 10, but the .NET 9 SDK cannot build `net10.0`, and it doubles the
  package-consumer gate and the test matrix. Deferred until the SDK changes.
- **(c) Move to `net10.0` only.** Also blocked by the SDK, and it drops every consumer still on .NET 8
  at 1.0.

## Consequences
- 1.0 makes no promise about a runtime after Microsoft's end of support; the documents say so.
- Consumers running on .NET 10 decide their own dependency versions, with the guidance above.
- A later 1.x may add `net10.0` (an additive change) once the SDK allows it, through a new decision.
