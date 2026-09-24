using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.Http;

/// <summary>
/// KAL0017 — every MapGet/MapPost invocation that has a .WithTags(...) call in the
/// same fluent chain must include "Kaleido" as one of the string literal arguments.
/// MapGet/MapPost calls without any .WithTags(...) are reported by KAL0016 instead.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class EndpointKaleidoTagAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.EndpointKaleidoTag,
            "Endpoint mapping is missing the \"Kaleido\" tag",
            "Endpoint mapping is missing the \"Kaleido\" tag — add \"Kaleido\" to .WithTags() so the endpoint is discoverable in the registry",
            "Kaleido.Design",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (GetMethodName(invocation) is not ("MapGet" or "MapPost"))
        {
            return;
        }

        InvocationExpressionSyntax? withTags = null;

        foreach (var node in FluentChain(invocation))
        {
            if (GetMethodName(node) == "WithTags")
            {
                withTags = node;
                break;
            }
        }

        if (withTags is null)
        {
            return; // KAL0016 handles the missing .WithTags() case
        }

        foreach (var argument in withTags.ArgumentList.Arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax literal &&
                literal.IsKind(SyntaxKind.StringLiteralExpression) &&
                literal.Token.ValueText == "Kaleido")
            {
                return;
            }
        }

        // Report at the method name so the location is the .WithTags identifier,
        // not the full outer invocation expression.
        var withTagsName = ((MemberAccessExpressionSyntax)withTags.Expression).Name;
        context.ReportDiagnostic(Diagnostic.Create(Rule, withTagsName.GetLocation()));
    }

    private static IEnumerable<InvocationExpressionSyntax> FluentChain(
        InvocationExpressionSyntax start)
    {
        var node = start;

        while (node.Parent is MemberAccessExpressionSyntax &&
               node.Parent.Parent is InvocationExpressionSyntax parent)
        {
            yield return parent;
            node = parent;
        }
    }

    private static string? GetMethodName(InvocationExpressionSyntax invocation) =>
        invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null
        };
}
