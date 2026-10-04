# Kaleido analyzer projects

This directory contains three Roslyn analyzer projects with different audiences. The canonical rule catalog, severities, exemptions and security rationale live in [`docs/ANALYZERS.md`](../../docs/ANALYZERS.md); keep that guide authoritative rather than duplicating the rules here.

| Project | Rules and audience | Tests |
|---|---|---|
| [`Kaleido.Analyzers`](Kaleido.Analyzers/) | Consumer `KAL2xxx` rules, bundled into `Kaleido.nupkg` under `analyzers/dotnet/cs/` | [`Kaleido.Analyzers.UnitTests`](../../tests/Kaleido.Analyzers.UnitTests/) |
| [`Kaleido.Analyzers.Source`](Kaleido.Analyzers.Source/) | Repository contributor `KAL0xxx` rules; not shipped to consumers | [`Kaleido.Analyzers.Source.UnitTests`](../../tests/Kaleido.Analyzers.Source.UnitTests/) |
| [`Kaleido.Analyzers.Testing`](Kaleido.Analyzers.Testing/) | Repository test-contributor `KAL1xxx` rules; not shipped to consumers | [`Kaleido.Analyzers.Testing.UnitTests`](../../tests/Kaleido.Analyzers.Testing.UnitTests/) |

All three projects are non-packable on their own. [`src/Kaleido/Kaleido.csproj`](../../src/Kaleido/Kaleido.csproj) references the consumer analyzer during builds and packs its DLL into the main Kaleido package; the Source and Testing analyzers remain repository-only. Diagnostic severities and test-project exceptions are configured in [`.editorconfig`](../../.editorconfig). Each analyzer project tracks new diagnostics in its `AnalyzerReleases.Unshipped.md`.

When changing a rule, update its matching analyzer tests and the canonical rule guide. Use the repository's [per-commit build and test gate](../../AGENTS.md#per-commit-quality-gate); the `--` separator in `dotnet test Kaleido.slnx --` is required for Microsoft Testing Platform test discovery.
