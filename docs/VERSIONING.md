# Versioning, compatibility and support

Kaleido is in **pre-1.0 preview**. This page states what holds during the preview and what is
decided at 1.0. Nothing here promises a support period or a date.

## Versions

- Package versions come from [`version.json`](../version.json)
  ([Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)): `1.0.0-preview.{height}`,
  where the height is the number of commits since the version was last changed. All eight
  packages (the seven `src/` packages and `Kaleido.Analyzers`) share one version. Only master and
  `v*` tags build that public version; any other branch adds the commit (`-g<commit>`), so a package
  packed from a topic branch is never mistaken for a published one (#526).
- **The packages move together.** Each package depends on its sibling Kaleido packages at exactly
  its own version (`[x.y.z]`, #431): the transports, the client and the MCP adapter use core's
  internal seams (`InternalsVisibleTo`), which the public API baseline does not cover, so a mixed set
  (for example `Kaleido 1.0.1` with `Kaleido.Http 1.0.0`) would fail at run time. NuGet refuses that
  combination at restore instead; upgrade all the Kaleido packages a project references together.
- The release workflow publishes every run to the repository's GitHub Packages feed, and each tagged
  version (preview or stable) also to nuget.org ([`RELEASING.md`](../RELEASING.md)); what changed is
  in [`CHANGELOG.md`](../CHANGELOG.md).
- From 1.0 the packages follow [Semantic Versioning](https://semver.org): a breaking change to the
  public API, the HTTP wire contracts, the error codes or the telemetry names needs a new major
  version.

## During the preview

- **Any preview may contain breaking changes**: public types, wire contracts, error codes,
  configuration keys and telemetry names can change while the public contract is refined.
- A breaking change is named in its pull request and its commit message (a "Breaking changes"
  section), so it can be found before updating.
- There is no deprecation period: an API is changed or removed in the preview that changes it.
- Only the latest preview receives fixes ([security policy](../SECURITY.md)).

Use previews for evaluation and experimentation, not as a compatibility or production-readiness
commitment.

## The public API baseline

Each package records its public API in two files in its project's `.PublicAPI` folder (`Microsoft.CodeAnalysis.PublicApiAnalyzers`, #313):

| File | Holds |
|---|---|
| `PublicAPI.Shipped.txt` | the API of the released versions (empty until 1.0) |
| `PublicAPI.Unshipped.txt` | what changed since the last release (during the preview, the whole API) |

- A public type or member that is added, changed or removed without its line fails the build
  (RS0016 for an unrecorded addition, RS0017 for a recorded member that no longer exists). So every
  public API change is a visible diff of `PublicAPI.Unshipped.txt` in its pull request.
- The IDE's code fix for RS0016 writes the line; `dotnet format analyzers <project> --diagnostics RS0016`
  does it for a whole project.
- Several overloads with optional parameters are rejected (RS0026/RS0027), since adding a parameter
  later would break compiled callers: one overload takes the optional parameters.
- **At each stable release (1.0 and later)** the `Unshipped` lines move to `Shipped`
  ([releasing](../RELEASING.md)). A preview release moves nothing: during the preview `Unshipped`
  holds the whole API and `Shipped` stays empty, so the first move is the 1.0 release.
- **After 1.0 ships**, package validation (`EnablePackageValidation`) compares each package with the
  released 1.0 package, so a binary-breaking change in a 1.x is caught at pack time.

## Runtime

- The packages target **`net8.0`**: a service runs on .NET 8 or later. `Kaleido.Server`,
  `Kaleido.Http.Server`, `Kaleido.Registry.Http` and `Kaleido.Mcp` need ASP.NET Core (`Microsoft.AspNetCore.App`).
- The repository builds with the **.NET 10 SDK** pinned in [`global.json`](../global.json) (#578). Moving to a newer SDK is a deliberate change with its own issue; the packages' target framework does not follow the SDK.
- CI builds and tests on Linux with the .NET 8 runtime installed, so the target framework is the
  runtime the tests run on.
- Changing the target framework, or adding one, is a maintainer decision recorded in an issue.

### Support window

Kaleido supports a runtime while Microsoft does ([ADR 0035](decisions/0035-runtime-support-window-at-1-0.md),
#528). Microsoft's [.NET support policy](https://dotnet.microsoft.com/platform/support/policy/dotnet-core)
ends support for .NET 8 and .NET 9 on **10 November 2026**: after that, run Kaleido services on .NET 10
or later. Kaleido 1.x keeps working on .NET 8, but on a runtime that gets no security patches.

The packages depend on the 8.x lines of Microsoft.Extensions.* (and Microsoft.EntityFrameworkCore.Sqlite
8.0.x for the SQLite provider), which stop getting patches on the same date. **Override them in your
service** to supported, patched versions - a direct PackageReference, or a version in central package
management - and NuGet uses the higher version. Choose a version that still ships a build for your
runtime and for `net8.0`, which Kaleido's assemblies target: some newer majors (EF Core 10, for example)
need a newer runtime. The repository's SDK can now build a `net10.0` target; adding one is a maintainer
decision (#579).

### Deployment modes

| Publish mode | 1.0 |
|---|---|
| Framework-dependent or self-contained (JIT) | Supported |
| Trimmed (`PublishTrimmed`) | **Not supported** |
| NativeAOT (`PublishAot`) | **Not supported** |

Kaleido discovers steps, handlers, sources and views by scanning the configured assemblies, and it
dispatches to them, maps their endpoints, serializes their types and validates their inputs by
reflection (generic methods closed at run time, reflection-based JSON and DataAnnotations, expression
trees for queries). Trimming removes types that are only reached that way, and NativeAOT cannot
create those generic instantiations at run time. The packages are not marked `IsTrimmable` or
`IsAotCompatible`, carry no trimming annotations, and no publish test covers either mode (#449).
Supporting them would take compile-time generation of the registration, dispatch, endpoints and JSON
contracts: a new feature, which a later version could add as an opt-in without breaking the
reflection path.

## Decided at 1.0

The 1.0 contract freeze ([#241](https://github.com/no1ross/Kaleido/issues/241)) decides:

- which APIs and contracts are covered by the compatibility promise;
- the deprecation rule for 1.x (how long an `[Obsolete]` member stays before removal);
- the supported runtimes and how long each is supported ([support window](#support-window), ADR 0035).

At 1.0 the public API baselines (`PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`) and the consumer
analyzer rules (`KAL2xxx`, to `AnalyzerReleases.Shipped.md`) are moved, so later changes to them are
recorded per release ([`tools/analyzers/README.md`](../tools/analyzers/README.md#rule-inventory-and-release-tracking)).
What those baselines hold is reviewed type by type in [ADR 0032](decisions/0032-public-api-at-1-0.md), kept current by every change to the public API until the release.

Until then, this page is the policy.
