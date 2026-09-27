using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Bootstrap;

/// <summary>
/// KAL2009 — AddKaleido() options lambdas must set Assemblies explicitly.
/// The GetCallingAssembly() fallback is JIT-nondeterministic and should not
/// be relied upon in production code.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AddKaleidoAssembliesAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.AddKaleidoMissingAssemblies,
            "AddKaleido() options lambda does not set Assemblies",
            "AddKaleido() options lambda does not set Assemblies — the GetCallingAssembly() fallback is JIT-nondeterministic. Set o.Assemblies explicitly.",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "Setting Assemblies explicitly ensures deterministic step and view discovery regardless of JIT inlining.");

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

        if (GetMethodName(invocation) != "AddKaleido")
        {
            return;
        }

        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count < 2)
        {
            return;
        }

        var secondArg = arguments[1].Expression;
        CSharpSyntaxNode? lambdaBody = secondArg switch
        {
            SimpleLambdaExpressionSyntax simple => (CSharpSyntaxNode)simple.Body,
            ParenthesizedLambdaExpressionSyntax parenthesized => (CSharpSyntaxNode)parenthesized.Body,
            _ => null
        };

        if (lambdaBody is null)
        {
            return;
        }

        if (LambdaBodyReferencesAssemblies(lambdaBody))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, invocation.GetLocation()));
    }

    private static bool LambdaBodyReferencesAssemblies(CSharpSyntaxNode body)
    {
        foreach (var node in body.DescendantNodesAndSelf())
        {
            if (node is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText == "Assemblies")
            {
                return true;
            }

            if (node is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "Assemblies")
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax m => m.Name.Identifier.ValueText,
            IdentifierNameSyntax id => id.Identifier.ValueText,
            _ => null
        };
}
