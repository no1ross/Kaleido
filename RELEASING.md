# Releasing Kaleido

How the eight Kaleido packages (the seven `src/` packages and `Kaleido.Analyzers`) are released. The
release workflow is [`.github/workflows/release.yml`](.github/workflows/release.yml); versions come from
[`version.json`](version.json) ([`docs/VERSIONING.md`](docs/VERSIONING.md)).

## Where packages go

| What | Where |
|---|---|
| `.nupkg` (all eight) | the repository's GitHub Packages feed: `https://nuget.pkg.github.com/no1ross/index.json` |
| `.snupkg` (symbols) | attached to the GitHub Release (and to the workflow run); GitHub Packages does not host symbol packages |
| stable packages on nuget.org | not yet: decided before 1.0.0 (see the [changelog](CHANGELOG.md)) |

Consumers of the feed add it as a NuGet source with a token that can read packages.

## What the workflow guarantees

Every run, from a clean checkout of the tag (or master, for a dry run):
1. restores, builds and tests the CI solution (`Kaleido.CI.slnf`) in Release;
2. packs the eight packages and verifies them ([`tools/packaging/Test-PackageConsumer.ps1`](tools/packaging/Test-PackageConsumer.ps1)):
   contents (assembly, XML docs, README, symbols), a clean consumer build with the consumer analyzer and
   no repository-only rule, and deterministic source paths with Source Link in every symbols package;
3. on a tag, refuses to publish unless the tag equals the packed version;
4. publishes the `.nupkg` files (`--skip-duplicate`), keeps packages and symbols with the run, and on a
   tag creates the GitHub Release (a prerelease for a version with a suffix).

**Least privilege.** The workflow has `contents: read` by default; the release job adds only
`contents: write` (the GitHub Release) and `packages: write` (the feed), and publishes with the run's
`GITHUB_TOKEN` - no stored publication secret. The job runs in the **`release` environment**: configure
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
   the version (with the date), and list its breaking changes. Merge it.
2. **Version.** Set the version in `version.json` (e.g. `"1.0.0"` for a stable release, or keep
   `1.0.0-preview.{height}` for a preview) in a pull request; merge it. A `v*` tag does not change the
   version by itself.
3. **Tag.** Tag the merge commit on master: `git tag v1.0.0 <commit>` and push the tag
   (`git push origin v1.0.0`). For a preview, tag the computed version (`nbgv get-version -v NuGetPackageVersion`).
4. **Approve.** Approve the `release` environment deployment when the run's build, tests and package
   verification are green.
5. **Check.** The packages are in the feed, the GitHub Release lists the packages and symbols, and the
   changelog link in the release resolves.

A failed run publishes nothing that was not verified; fix the cause on master and tag again (a new
version, since published versions are immutable).

## Provenance

The packages are built only by this workflow, from a tagged commit, with deterministic builds and Source
Link (each symbol file points at the exact commit). Signed build attestations and package signing are
considered for the stable nuget.org release.
