# Releasing Kaleido

How the eight Kaleido packages (the seven `src/` packages and `Kaleido.Analyzers`) are released. The
release workflow is [`.github/workflows/release.yml`](.github/workflows/release.yml); versions come from
[`version.json`](version.json) ([`docs/VERSIONING.md`](docs/VERSIONING.md)).

## Where packages go

| What | Where |
|---|---|
| `.nupkg` (all eight), every tagged version (preview or stable) | **nuget.org**, published with [trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing) (no stored API key) |
| `.nupkg` (all eight), every run (tags and dry runs) | the repository's GitHub Packages feed: `https://nuget.pkg.github.com/no1ross/index.json` |
| `.snupkg` (symbols) | nuget.org's symbol server (tags), attached to the GitHub Release (tags) and to the workflow run; GitHub Packages does not host symbol packages |
| build provenance | a signed attestation per `.nupkg`, stored with the repository's attestations ([Provenance](#provenance)) |

Consumers install tagged versions from nuget.org with no extra source. A preview built between tags is
only on the GitHub Packages feed: add it as a NuGet source with a token that can read packages.

## Owner setup (once, before the first tag)

The repository cannot configure these; a tag run fails before publishing anything until they exist:
1. **nuget.org account** (or organization) that will own the `Kaleido` and `Kaleido.*` package IDs.
   Optionally reserve the `Kaleido.` ID prefix.
2. **Trusted publishing policy** on nuget.org for repository `no1ross/Kaleido`, workflow file
   `release.yml`, environment `release`.
3. **Repository variable** `NUGET_USER` (Settings -> Secrets and variables -> Actions -> Variables): the
   nuget.org user name or organization of that policy. It is not a secret; no API key is stored.
4. **The `release` environment** with required reviewers and deployment tags `v*` (see below).

## What the workflow guarantees

Every run, from a clean checkout of the tag (or master, for a dry run):
1. restores, builds and tests the solution (`Kaleido.slnx`) in Release;
2. packs the eight packages and verifies them ([`tools/packaging/Test-PackageConsumer.ps1`](tools/packaging/Test-PackageConsumer.ps1)):
   exactly the release set (the script's manifest of ids, one version, one symbols package per
   library: a missing, duplicate, unexpected or mixed-version package fails), contents and metadata
   (assembly, XML docs, README, icon, LICENSE, NOTICE, license, repository and commit), exact
   `[x.y.z]` dependencies between the Kaleido packages ([they move together](docs/VERSIONING.md#versions)),
   analyzer isolation read from a consumer's restored assets, a clean consumer build with the consumer
   analyzer and no repository-only rule, a clean host of every runtime package that starts and
   answers, and deterministic source paths with Source Link in every symbols package (#436). CI also
   runs its self-test (`Test-PackageConsumer.Tests.ps1`), which breaks the set each of those ways;
3. on a tag, refuses to publish unless the tag equals the packed version and the nuget.org publisher
   (`NUGET_USER`) is configured;
4. attests the build provenance of every `.nupkg`;
5. publishes the `.nupkg` files (`--skip-duplicate`) to GitHub Packages and, on a tag, to nuget.org;
   keeps packages and symbols with the run, and on a tag creates the GitHub Release (a prerelease for
   a version with a suffix).

**Least privilege.** The workflow has `contents: read` by default; the release job adds only
`contents: write` (the GitHub Release), `packages: write` (the feed), and `id-token: write` with
`attestations: write` (the OIDC token for nuget.org trusted publishing and the attestation). It
publishes to GitHub Packages with the run's `GITHUB_TOKEN` and to nuget.org with a short-lived key
exchanged from the run's OIDC token - no stored publication secret. The job runs in the **`release` environment**: configure
it in the repository settings with **required reviewers** and **deployment tags `v*`** (plus master for
the dry run), so nothing is published without an approval.

## Dry run

Run the **Release** workflow manually (Actions -> Release -> Run workflow) on master. It publishes the
current preview version (`1.0.0-preview.{height}`) to the feed and uploads packages and symbols to the
run, without a GitHub Release. Check:
- the eight packages appear in the feed with the run's version;
- the run's `release-packages` artifact holds eight `.nupkg` and seven `.snupkg` files;
- a scratch project restores `Kaleido` from the feed and builds.

## Releasing a version

1. **Changelog.** Move the `Unreleased` entries in [`CHANGELOG.md`](CHANGELOG.md) under a heading for
   the version (with the date), and list its breaking changes. For a stable release (1.0 and later),
   in the same pull request:
   - move each package's `PublicAPI.Unshipped.txt` lines to its `PublicAPI.Shipped.txt`, leaving
     only the `#nullable enable` header ([public API baseline](docs/VERSIONING.md#the-public-api-baseline));
   - move the `KAL2xxx` rows to `AnalyzerReleases.Shipped.md` under `## Release <version>`.

   Merge it. After 1.0 has shipped, enable package validation against it (`EnablePackageValidation`
   with `PackageValidationBaselineVersion` 1.0.0) in `src/Directory.Build.props`.
2. **Version.** Set the version in `version.json` (e.g. `"1.0.0"` for a stable release, or keep
   `1.0.0-preview.{height}` for a preview) in a pull request; merge it. A `v*` tag does not change the
   version by itself.
3. **Tag.** Tag the merge commit on master: `git tag v1.0.0 <commit>` and push the tag
   (`git push origin v1.0.0`). For a preview, tag the computed version (`nbgv get-version -v NuGetPackageVersion`).
4. **Approve.** Approve the `release` environment deployment when the run's build, tests and package
   verification are green.
5. **Check.** The packages are on nuget.org and in the GitHub Packages feed, the GitHub Release lists
   the packages and symbols, and the changelog link in the release resolves.

A failed run publishes nothing that was not verified; fix the cause on master and tag again (a new
version, since published versions are immutable).

## Provenance

The packages are built only by this workflow, from a tagged commit, with deterministic builds and Source
Link (each symbol file points at the exact commit). Each `.nupkg` has a signed
[build provenance attestation](https://docs.github.com/actions/security-for-github-actions/using-artifact-attestations)
naming the workflow run and commit that built it; verify a downloaded package with:

```bash
gh attestation verify Kaleido.<version>.nupkg --repo no1ross/Kaleido
```

nuget.org repository-signs every package it accepts. Author signing (a code-signing certificate) is
deliberately not used for now: it needs a certificate and its custody, and the attestation already
ties each package to its build (#432).

Every package carries the `LICENSE` and `NOTICE` files.
