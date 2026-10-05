using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures;

/// <summary>
/// KAL1013 — a unit-test fixture lives in its SUT's namespace with <c>.UnitTests</c>
/// appended: a fixture for <c>Kaleido.Queryable.Query.QueryContextEngine</c> is declared in
/// <c>Kaleido.Queryable.Query.UnitTests</c>. The SUT is the <c>SutFixture&lt;TSut&gt;</c> type
/// argument, or else the type named by the fixture's <c>{Sut}Tests</c> prefix.
/// Unit-test assemblies only; folders still mirror <c>src/</c> (KAL1003).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixtureNamespaceAnalyzer : DiagnosticAnalyzer
{
    private const string UnitTestsSuffix = "UnitTests";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.FixtureNamespace,
            "Fixture namespace must be the SUT namespace plus .UnitTests",
            "Test fixture '{0}' tests '{1}' — declare it in namespace '{2}'",
            "Kaleido.Tests",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (!FixtureConventions.IsUnitTestAssembly(start.Compilation))
            {
                return;
            }

            start.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
        });
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (!FixtureConventions.IsFixture(type))
        {
            return;
        }

        var sut =
            FixtureConventions.GetSutFixtureSut(type)
            ?? FixtureConventions.ResolveSut(context.Compilation, type.Name, context.CancellationToken);

        if (sut is null)
        {
            return;
        }

        var expected =
            sut.ContainingNamespace.IsGlobalNamespace
                ? UnitTestsSuffix
                : $"{sut.ContainingNamespace.ToDisplayString()}.{UnitTestsSuffix}";

        if (type.ContainingNamespace.ToDisplayString() == expected)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, type.Locations[0], type.Name, sut.ToDisplayString(), expected));
    }
}
