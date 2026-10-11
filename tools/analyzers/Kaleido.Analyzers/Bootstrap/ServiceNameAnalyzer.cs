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
/// KAL2020 — o.ServiceName is longer than <see cref="MaxLength"/> characters. It prefixes every MCP
/// tool name (<c>{service}_{capability}</c>), and MCP limits tool names to 64 characters.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceNameAnalyzer : DiagnosticAnalyzer
{
    public const int MaxLength = 15;

    private static readonly DiagnosticDescriptor LengthRule =
        new(
            DiagnosticIds.ServiceNameLength,
            "ServiceName is too long for MCP tool names",
            "ServiceName '{0}' is {1} characters; keep it at most {2}. It prefixes every MCP tool name ('{0}_…'), which MCP limits to 64 characters.",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            description: "AI agents call Kaleido capabilities as MCP tools named {service}_{capability}; the service name has a 15-character budget and the capability part 40 (KAL2019).");

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
        ImmutableArray.Create(Rule, LengthRule);

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

        if (value.Length > MaxLength)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(LengthRule, literal.GetLocation(), value, value.Length, MaxLength));
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
