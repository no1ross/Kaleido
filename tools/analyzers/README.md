# Kaleido analyzer projects

This directory contains three Roslyn analyzer projects with different audiences. The canonical rule catalog, severities, exemptions and security rationale live in [`docs/ANALYZERS.md`](../../docs/ANALYZERS.md); keep that guide authoritative rather than duplicating the rules here.

| Project | Rules and audience | Tests |
|---|---|---|
| [`Kaleido.Analyzers`](Kaleido.Analyzers/) | Consumer `KAL2xxx` rules, shipped as the analyzer-only `Kaleido.Analyzers` package that `Kaleido` depends on | [`Kaleido.Analyzers.UnitTests`](../../tests/Kaleido.Analyzers.UnitTests/) |
| [`Kaleido.Analyzers.Source`](Kaleido.Analyzers.Source/) | Repository contributor `KAL0xxx` rules; not shipped to consumers | [`Kaleido.Analyzers.Source.UnitTests`](../../tests/Kaleido.Analyzers.Source.UnitTests/) |
| [`Kaleido.Analyzers.Testing`](Kaleido.Analyzers.Testing/) | Repository test-contributor `KAL1xxx` rules; not shipped to consumers | [`Kaleido.Analyzers.Testing.UnitTests`](../../tests/Kaleido.Analyzers.Testing.UnitTests/) |

`Kaleido.Analyzers` is packed on its own (only `analyzers/dotnet/cs/Kaleido.Analyzers.dll`, no dependencies) and [`src/Kaleido/Kaleido.csproj`](../../src/Kaleido/Kaleido.csproj) references it with analyzer assets only, so `Kaleido.nupkg` depends on it without compiling against or running it. The Source and Testing analyzers are not packable and remain repository-only.

Where each family runs in this repository:

| Family | Loaded by | Applies to |
|---|---|---|
| Consumer `KAL2xxx` | [`samples/Directory.Build.props`](../../samples/Directory.Build.props) | `samples/*` (and services that install `Kaleido`) |
| Source `KAL0xxx` | [`src/Directory.Build.props`](../../src/Directory.Build.props) | `src/*` |
| Test `KAL1xxx` | [`tests/Directory.Build.props`](../../tests/Directory.Build.props) | `tests/*` |

Diagnostic severities and test-project exceptions are configured in [`.editorconfig`](../../.editorconfig).

Logic two projects share is linked from [`Shared/`](Shared/) rather than referenced: `CancellationCatch` (KAL2004, KAL0021) and `TypeBehavior` - which types have behavior and their file/fixture name - used by KAL0022/KAL0023 and KAL1009/KAL1002/KAL1003/KAL1007, so the file rule and the fixture rules never disagree. KAL1003 also reads the `src/**/*.cs` paths `tests/Directory.Build.props` passes as additional files: a unit-test project sees `src/` only as compiled assemblies.

## Rule inventory and release tracking

The `AnalyzerReleases.*.md` files are the authoritative rule inventory: Roslyn release tracking (RS2000–RS2008) checks them against the compiled descriptors, and `tools/` builds with `TreatWarningsAsErrors`, so a descriptor without its row (or a row without its descriptor) fails the build. Today: 20 consumer (`KAL2xxx`), 20 source (`KAL0xxx`) and 12 test (`KAL1xxx`) rules. [`docs/ANALYZERS.md`](../../docs/ANALYZERS.md) documents each of them and lists [retired IDs](../../docs/ANALYZERS.md#retired-ids), which are never reused.

- A new or changed rule goes in its project's `AnalyzerReleases.Unshipped.md`.
- `Kaleido.Analyzers` also has an `AnalyzerReleases.Shipped.md`, empty during the preview. **At 1.0** the `KAL2xxx` rows move from `Unshipped.md` to `Shipped.md` under `## Release 1.0.0`; from then on, changing or removing a consumer rule is recorded there (`### Changed Rules` / `### Removed Rules`) in the release that does it.
- The Source and Testing analyzers never ship, so their rules stay in `Unshipped.md`; a removed repository rule simply loses its row and is listed under retired IDs.

When changing a rule, update its matching analyzer tests and the canonical rule guide. Use the repository's [per-commit build and test gate](../../AGENTS.md#per-commit-quality-gate); the `--` separator in `dotnet test Kaleido.slnx --` is required for Microsoft Testing Platform test discovery.
