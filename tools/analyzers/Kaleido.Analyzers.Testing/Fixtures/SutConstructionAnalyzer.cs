using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures;

/// <summary>
/// KAL1008 G�� the SUT may only be constructed inside CreateSut(). new-ing the
/// fixture's TSut inside test methods creates per-test construction drift;
/// all arrangement belongs in the single CreateSut() seam.
/// Scoped to unit-test projects via .editorconfig.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SutConstructionAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.SutConstruction,
            "SUT may only be constructed inside CreateSut()",
            "'{0}' may only be constructed inside CreateSut() G�� use CreateSut() or the Sut property",
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
            context.ContainingSymbol?.ContainingType is not INamedTypeSymbol fixture)
        {
            return;
        }

        var sut =
            FixtureConventions.GetSutFixtureSut(fixture) ??
            (FixtureConventions.InheritsSutFixture(fixture)
                ? FixtureConventions.ResolveSut(
                    context.Compilation,
                    fixture.Name,
                    context.CancellationToken)
                : null);

        if (sut is null ||
            !SymbolEqualityComparer.Default.Equals(
                created.OriginalDefinition, sut.OriginalDefinition))
        {
            return;
        }

        // allowed inside CreateSut (or a local function/lambda within it)
        for (var node = context.Node.Parent; node is not null; node = node.Parent)
        {
            if (node is MethodDeclarationSyntax method &&
                method.Identifier.ValueText == "CreateSut")
            {
                return;
            }

            if (node is TypeDeclarationSyntax)
            {
                break;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, context.Node.GetLocation(), sut.Name));
    }
}
