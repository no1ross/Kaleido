# Kaleido.Analyzers

Compile-time checks (`KAL2xxx`) for services built on Kaleido: misconfigured steps, query sources, views and host registration fail the build instead of failing at startup.

The package is installed automatically with `Kaleido`; you do not need to reference it yourself. It contains only the analyzer (`analyzers/dotnet/cs/`) and adds no runtime dependency.

The rule catalog, severities and rationale are in [`docs/ANALYZERS.md`](https://github.com/no1ross/Kaleido/blob/master/docs/ANALYZERS.md#consumer-rules--kal2xxx).
