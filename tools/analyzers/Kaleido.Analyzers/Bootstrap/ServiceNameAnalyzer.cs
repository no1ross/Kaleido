using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Bootstrap;

/// <summary>
/// KAL2005 — o.ServiceName must be lowercase with no spaces or separators.
/// ServiceName is used verbatim as the HTTP route prefix (e.g. "intake" → /intake/processes).
/// Uppercase, hyphens, underscores, or spaces cause route mismatches.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceNameAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ServiceNameFormat,
            "ServiceName must be lowercase with no spaces or separators",
            "ServiceName '{0}' contains uppercase letters, spaces, or separators. It is used verbatim as a route prefix and must be lowercase with no spaces or separators (e.g. '{1}').",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "ServiceName is used directly as the HTTP route prefix. It must be lowercase with no spaces, hyphens, or underscores.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(Analyze, SyntaxKind.SimpleAssignmentExpression);
    }

    private static void Analyze(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        if (assignment.Left is not MemberAccessExpressionSyntax memberAccess ||
            memberAccess.Name.Identifier.Text != "ServiceName")
        {
            return;
        }

        if (assignment.Right is not LiteralExpressionSyntax literal ||
            !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            return;
        }

        var value = literal.Token.ValueText;
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        var isInvalid = false;
        foreach (var c in value)
        {
            if (char.IsUpper(c) || c == ' ' || c == '-' || c == '_')
            {
                isInvalid = true;
                break;
            }
        }

        if (!isInvalid)
        {
            return;
        }

        var suggested = value
            .ToLowerInvariant()
            .Replace("-", string.Empty)
            .Replace("_", string.Empty)
            .Replace(" ", string.Empty);

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, literal.GetLocation(), value, suggested));
    }
}
