# Versioning, compatibility and support

Kaleido is in **pre-1.0 preview**. This page states what holds during the preview and what is
decided at 1.0. Nothing here promises a support period or a date.

## Versions

- Package versions come from [`version.json`](../version.json)
  ([Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)): `1.0.0-preview.{height}`,
  where the height is the number of commits since the version was last changed. All eight
  packages (the seven `src/` packages and `Kaleido.Analyzers`) share one version.
- Previews and releases are published by the release workflow to the repository's GitHub Packages
  feed ([`RELEASING.md`](../RELEASING.md)); what changed is in [`CHANGELOG.md`](../CHANGELOG.md).
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
- **At each release** the `Unshipped` lines move to `Shipped` ([releasing](../RELEASING.md)).
- **After 1.0 ships**, package validation (`EnablePackageValidation`) compares each package with the
  released 1.0 package, so a binary-breaking change in a 1.x is caught at pack time.

## Runtime

- The packages target **`net8.0`**: a service runs on .NET 8 or later. `Kaleido.Http` and
  `Kaleido.Http.Mcp` also need ASP.NET Core (`Microsoft.AspNetCore.App`).
- The repository builds with the **.NET 9 SDK** pinned in [`global.json`](../global.json), for now: it moves back to the .NET 10 SDK when that is available again (#424).
- CI builds and tests on Linux with the .NET 8 runtime installed, so the target framework is the
  runtime the tests run on.
- Changing the target framework, or adding one, is a maintainer decision recorded in an issue.

## Decided at 1.0

The 1.0 contract freeze ([#241](https://github.com/no1ross/Kaleido/issues/241)) decides:

- which APIs and contracts are covered by the compatibility promise;
- the deprecation rule for 1.x (how long an `[Obsolete]` member stays before removal);
- the supported runtimes and how long each is supported.

At 1.0 the public API baselines (`PublicAPI.Unshipped.txt` to `PublicAPI.Shipped.txt`) and the consumer
analyzer rules (`KAL2xxx`, to `AnalyzerReleases.Shipped.md`) are moved, so later changes to them are
recorded per release ([`tools/analyzers/README.md`](../tools/analyzers/README.md#rule-inventory-and-release-tracking)).

Until then, this page is the policy.
