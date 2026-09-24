# Kaleido.Analyzers contributor guide

This document is the authoritative reference for contributors adding or modifying analyzers in `tools/analyzers/Kaleido.Analyzers`.

## Overview

`Kaleido.Analyzers` is a Roslyn analyzer package that enforces Kaleido's design and test conventions at compile time. All rules produce build diagnostics — warnings or errors configured via `.editorconfig`. There are no runtime side-effects.

## Project layout

```
tools/analyzers/
  Kaleido.Analyzers/               ← analyzer implementations
    Coverage/                      ← KAL1010 — endpoint functional coverage
    DependencyInjection/           ← KAL0006–KAL0014 — DI convention rules
    Design/                        ← KAL0001–KAL0004 — general design rules
    Fixtures/                      ← KAL1001–KAL1009 — test fixture conventions
    Http/                          ← KAL0016–KAL0017 — HTTP endpoint tagging rules
    Structure/                     ← KAL0015 — structural co-location rules
    DiagnosticIds.cs               ← all KAL* ID constants (single source of truth)
    SymbolExtensions.cs            ← shared Roslyn helpers
  Kaleido.Analyzers.Tests/         ← one test class per analyzer
```

## Diagnostic ID ranges

| Range | Category | Owner |
|---|---|---|
| KAL0001–KAL0004 | Design | `Design/` |
| KAL0005–KAL0014 | DependencyInjection | `DependencyInjection/` |
| KAL0015 | Structure | `Structure/` |
| KAL0016–KAL0017 | Http | `Http/` |
| KAL1001–KAL1009 | Test fixtures | `Fixtures/` |

The split between `KAL0xxx` (source design rules) and `KAL1xxx` (test convention rules) is intentional and must be preserved.

## Adding a new rule

1. **Pick the next available ID** in the appropriate range from `DiagnosticIds.cs`. Add the constant there.
2. **Add the release entry** to `AnalyzerReleases.Unshipped.md` in the format the SDK expects.
3. **Create the analyzer** in the matching subfolder. Copy the structure of a nearby analyzer.
4. **Write tests** in `Kaleido.Analyzers.Tests/` with a matching subfolder and class name.
5. **Update `.editorconfig`** — add a `dotnet_diagnostic.KALxxxx.severity` entry for every project scope where the rule applies.
6. **Update `docs/ANALYZERS.md`** — add a row to the appropriate table.

## Analyzer structure conventions

Every analyzer follows the same shape:

```csharp
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticIds.MyRule,
        "Short title",
        "Message with '{0}' placeholders",
        "Kaleido.<Category>",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        // Register actions here
    }
}
```

- Use `EnableConcurrentExecution()` and `ConfigureGeneratedCodeAnalysis(None)` in every `Initialize`.
- Use `RegisterCompilationAction` (not `RegisterCompilationStartAction`) for rules that need to inspect the full compilation. Add `WellKnownDiagnosticTags.CompilationEnd` to the descriptor's `customTags` when doing so.
- Guard rules that should only fire in specific project types (e.g. `*.FunctionalTests`, `*.UnitTests`) by checking `context.Compilation.AssemblyName` early.
- Prefer `SemanticModel`-free syntax-only analysis where the rule permits it — it is faster and simpler.

## Test conventions

Tests use raw `CSharpCompilation` (not the Roslyn testing harness) for rules that operate on whole compilations, and `CSharpAnalyzerTest<TAnalyzer, TVerifier>` from `Microsoft.CodeAnalysis.CSharp.Analyzer.Testing` for syntax-level rules.

When testing rules that distinguish between source trees (e.g. KAL1010 which separates endpoint-declaration trees from test-coverage trees), pass **two separate `SyntaxTree` instances** to `CSharpCompilation.Create` — one representing the production code, one the test code. This mirrors the real multi-file compilation that the analyzer sees in production.

## `.editorconfig` configuration

Rules are scoped by glob. Add new rules to every relevant glob block:

- `[*.cs]` — rules that apply everywhere
- `[src/**/*.cs]` — source-only rules
- `[tests/**/*.cs]` — test-only rules
- `[tests/**/*FunctionalTests*/**/*.cs]` — functional-test–only rules (e.g. KAL1010)
- `[tests/**/*UnitTests*/**/*.cs]` — unit-test–only rules (e.g. KAL1001–KAL1009)

## HTTP endpoint rules (KAL0016–KAL0017)

These run against `src/Kaleido.Http` (the project that maps endpoints).

- **KAL0016** — every `MapGet`/`MapPost` call must chain `.WithTags(...)`.
- **KAL0017** — every `.WithTags(...)` chain must include `"Kaleido"` as one of the tag arguments.

The `"Kaleido"` tag is used at runtime for OpenAPI grouping and endpoint discovery. Endpoint names must be declared as `const string` fields in a `*EndpointNames` class in `Kaleido.Http.Abstractions` — do not use inline literals for `WithName()` values.
