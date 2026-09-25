using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures;

/// <summary>
/// KAL1007 — a fixture inheriting SutFixture&lt;TSut&gt; must be named
/// {TSut.Name}Tests — the name encodes the subject under test exactly.
/// Scoped to unit-test projects via .editorconfig.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixtureNameMatchesSutAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.FixtureNameMatchesSut,
            "Fixture name must match its declared SUT",
            "Test fixture '{0}' declares SUT '{1}' — it must be named '{2}'",
            "Kaleido.Tests",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (!FixtureConventions.IsFixture(type) ||
            FixtureConventions.GetSutFixtureSut(type) is not { } sut)
        {
            return;
        }

        var expected = sut.Name + FixtureConventions.TestsSuffix;

        if (type.Name == expected)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, type.Locations[0], type.Name, sut.Name, expected));
    }
}
