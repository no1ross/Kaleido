using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL0016 — every MapGet/MapPost invocation must have a .WithTags() call
/// somewhere in the same fluent chain.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EndpointTagMissingAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.EndpointTagMissing,
            "Endpoint mapping is missing .WithTags()",
            "Endpoint mapping has no .WithTags() call — every endpoint must declare its tags",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.InvocationExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetMethodName(invocation) is not ("MapGet" or "MapPost"))
        {
            return;
        }

        if (ChainContainsWithTags(invocation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool ChainContainsWithTags(InvocationExpressionSyntax root)
    {
        var node = (SyntaxNode)root;

        while (node.Parent is MemberAccessExpressionSyntax parentMember &&
               parentMember.Parent is InvocationExpressionSyntax parentInvocation)
        {
            if (parentMember.Name.Identifier.Text == "WithTags")
            {
                return true;
            }

            node = parentInvocation;
        }

        return false;
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax m => m.Name.Identifier.Text,
            IdentifierNameSyntax id => id.Identifier.Text,
            _ => null
        };
}
