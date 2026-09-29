using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures;

/// <summary>
/// KAL1012 — a unit-test fixture may not new up a framework collaborator:
/// a testable Kaleido.* type that implements a Kaleido service interface
/// (the mockable seam). Domain data the SUT consumes (types implementing no
/// framework interface, e.g. StepCandidate) is arrangement, not a collaborator.
/// Exempt: the fixture's SUT (handled by KAL1008), non-testable types, types
/// outside Kaleido.* production assemblies, and test-assembly doubles.
/// Scoped to unit-test projects via .editorconfig.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CollaboratorMustBeMockedAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.CollaboratorMustBeMocked,
            "Collaborator types must be mocked, not constructed",
            "'{0}' is a collaborator, not the SUT — mock it instead of new-ing a real instance",
            "Kaleido.Tests",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            Analyze,
            SyntaxKind.ObjectCreationExpression,
            SyntaxKind.ImplicitObjectCreationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        if (context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken)
                .Type is not INamedTypeSymbol created ||
            context.ContainingSymbol?.ContainingType is not INamedTypeSymbol fixture ||
            !FixtureConventions.InheritsSutFixture(fixture))
        {
            return;
        }

        var assemblyName = created.ContainingAssembly?.Name;

        // Only Kaleido production assemblies count — the test assembly itself
        // (test doubles) and BCL/third-party scaffolding (HttpClient,
        // ServiceCollection, Mock<>) are legitimate arrangement.
        if (assemblyName is null ||
            !(assemblyName == "Kaleido" ||
              assemblyName.StartsWith("Kaleido.", System.StringComparison.Ordinal)) ||
            assemblyName.EndsWith("Tests", System.StringComparison.Ordinal) ||
            SymbolEqualityComparer.Default.Equals(
                created.ContainingAssembly, context.Compilation.Assembly) ||
            !FixtureConventions.IsTestable(created) ||
            !ImplementsKaleidoInterface(created))
        {
            return;
        }

        var sut =
            FixtureConventions.GetSutFixtureSut(fixture) ??
            FixtureConventions.ResolveSut(
                context.Compilation,
                fixture.Name,
                context.CancellationToken);

        if (sut is not null &&
            SymbolEqualityComparer.Default.Equals(
                created.OriginalDefinition, sut.OriginalDefinition))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, context.Node.GetLocation(), created.Name));
    }

    /// <summary>
    /// True when the type implements at least one interface declared in a
    /// Kaleido production assembly — the contract seam a test should mock.
    /// </summary>
    private static bool ImplementsKaleidoInterface(INamedTypeSymbol type) =>
        type.AllInterfaces.Any(i =>
            i.ContainingAssembly?.Name is "Kaleido" ||
            i.ContainingAssembly?.Name.StartsWith(
                "Kaleido.", System.StringComparison.Ordinal) == true);
}
