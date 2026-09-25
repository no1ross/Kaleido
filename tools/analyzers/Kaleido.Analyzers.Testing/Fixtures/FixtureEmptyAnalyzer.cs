using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures;

/// <summary>
/// KAL1010 — a class named {Type}Tests exists but contains no [Fact] or
/// [Theory] test methods. An empty fixture stub does not satisfy the
/// coverage requirement — add at least one test. Scoped to unit-test
/// projects via .editorconfig.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixtureEmptyAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.FixtureEmpty,
            "Fixture has no test methods",
            "Fixture '{0}' has no [Fact] or [Theory] test methods — add at least one test or remove the empty stub",
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

        // Must be a non-abstract, non-static class ending with 'Tests'
        if (type.TypeKind != TypeKind.Class ||
            type.IsAbstract ||
            type.IsStatic ||
            !type.Name.EndsWith(FixtureConventions.TestsSuffix, System.StringComparison.Ordinal))
        {
            return;
        }

        // Must have at least one ordinary method (i.e. is a real fixture candidate, not
        // a pure base class or helper) — but none of those methods carry [Fact]/[Theory].
        var methods = type.GetMembers().OfType<IMethodSymbol>()
            .Where(m =>
                m.MethodKind == MethodKind.Ordinary &&
                !m.IsImplicitlyDeclared)
            .ToArray();

        // A class with no ordinary methods at all is not a fixture stub — skip it.
        // A class that already has [Fact]/[Theory] is fine — skip it.
        if (methods.Length == 0 ||
            methods.Any(m =>
                m.GetAttributes().Any(a =>
                    a.AttributeClass?.ToDisplayString() is
                        "Xunit.FactAttribute" or "Xunit.TheoryAttribute")))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, type.Locations[0], type.Name));
    }
}
