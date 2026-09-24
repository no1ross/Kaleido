using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL1002 — the {Sut}Tests prefix must resolve to a real type (the subject
/// under test). Catches fixtures named after scenarios instead of the SUT.
/// Scoped to unit-test projects via .editorconfig.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixtureSutResolutionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.FixtureSutResolution,
            "Test fixture name prefix must resolve to a subject under test",
            "Test fixture '{0}' does not map to a type named '{1}' — fixtures are named {{SutName}}Tests",
            "Kaleido.Tests",
            DiagnosticSeverity.Warning,
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

        if (!FixtureConventions.IsFixture(type))
        {
            return;
        }

        var prefix =
            type.Name.EndsWith(
                FixtureConventions.TestsSuffix,
                System.StringComparison.Ordinal)
                ? type.Name.Substring(0, type.Name.Length - FixtureConventions.TestsSuffix.Length)
                : type.Name;

        if (prefix.Length == 0)
        {
            return;
        }

        if (FixtureConventions.ResolveSut(
                context.Compilation,
                type.Name,
                context.CancellationToken) is null)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, type.Locations[0], type.Name, prefix));
        }
    }
}
