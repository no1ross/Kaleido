# Versioning, compatibility and support

Kaleido is in **pre-1.0 preview**. This page states what holds during the preview and what is
decided at 1.0. Nothing here promises a support period or a date.

## Versions

- Package versions come from [`version.json`](../version.json)
  ([Nerdbank.GitVersioning](https://github.com/dotnet/Nerdbank.GitVersioning)): `1.0.0-preview.{height}`,
  where the height is the number of commits since the version was last changed. All seven
  packages share one version.
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

## Runtime

- The packages target **`net8.0`**: a service runs on .NET 8 or later. `Kaleido.Http` and
  `Kaleido.Http.Mcp` also need ASP.NET Core (`Microsoft.AspNetCore.App`).
- The repository builds with the **.NET 10 SDK** pinned in [`global.json`](../global.json).
- CI builds and tests on Linux with the .NET 8 runtime installed, so the target framework is the
  runtime the tests run on.
- Changing the target framework, or adding one, is a maintainer decision recorded in an issue.

## Decided at 1.0

The 1.0 contract freeze ([#241](https://github.com/no1ross/Kaleido/issues/241)) decides:

- which APIs and contracts are covered by the compatibility promise;
- the deprecation rule for 1.x (how long an `[Obsolete]` member stays before removal);
- the supported runtimes and how long each is supported.

Until then, this page is the policy.
