using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures;

/// <summary>
/// KAL1006 G�� every unit-test fixture declares its subject under test by
/// inheriting SutFixture&lt;TSut&gt;. Scoped to unit-test projects via
/// .editorconfig.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixtureMustInheritSutFixtureAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.FixtureMustInheritSutFixture,
            "Unit-test fixtures must inherit SutFixture<TSut>",
            "Test fixture '{0}' must inherit SutFixture<{1}> G�� every unit test declares its subject under test",
            "Kaleido.Tests",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: false);

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

        if (type.IsAbstract || !FixtureConventions.IsFixture(type))
        {
            return;
        }

        if (FixtureConventions.InheritsSutFixture(type))
        {
            return;
        }

        var suggestedSut =
            type.Name.EndsWith(FixtureConventions.TestsSuffix, System.StringComparison.Ordinal)
                ? type.Name.Substring(0, type.Name.Length - FixtureConventions.TestsSuffix.Length)
                : "TSut";

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, type.Locations[0], type.Name, suggestedSut));
    }
}
