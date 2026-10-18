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
| Test `KAL1xxx` | [`tests/Directory.Build.props`](../../tests/Directory.Build.props) | `tests/*` | Diagnostic severities and test-project exceptions are configured in [`.editorconfig`](../../.editorconfig). Each analyzer project tracks new diagnostics in its `AnalyzerReleases.Unshipped.md`.

When changing a rule, update its matching analyzer tests and the canonical rule guide. Use the repository's [per-commit build and test gate](../../AGENTS.md#per-commit-quality-gate); the `--` separator in `dotnet test Kaleido.slnx --` is required for Microsoft Testing Platform test discovery.
