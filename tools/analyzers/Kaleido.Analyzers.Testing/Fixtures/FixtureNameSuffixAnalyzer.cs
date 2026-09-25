using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures;

/// <summary>
/// KAL1001 G�� test fixtures are named after their subject under test:
/// {SutName}Tests.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixtureNameSuffixAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.FixtureNameSuffix,
            "Test fixture names must end with 'Tests'",
            "Test fixture '{0}' must be named after its subject under test ('{1}Tests')",
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

        if (!FixtureConventions.IsUnitTestAssembly(context.Compilation) ||
            !FixtureConventions.IsFixture(type))
        {
            return;
        }

        if (!type.Name.EndsWith(
                FixtureConventions.TestsSuffix,
                System.StringComparison.Ordinal))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, type.Locations[0], type.Name, type.Name));
        }
    }
}
