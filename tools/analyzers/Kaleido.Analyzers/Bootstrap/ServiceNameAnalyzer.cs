using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Bootstrap;

/// <summary>
/// KAL2005 — a constant <c>ServiceName</c> must match the runtime rule <c>[a-z0-9][a-z0-9_-]*</c>
/// (<c>KaleidoServiceOptions.ValidateServiceName</c>): it is the HTTP route prefix and the MCP tool-name
/// prefix. Both are tested against the shared table in <c>tests/Shared/ServiceNameCases.cs</c>.
/// KAL2017 — the constant is longer than <see cref="MaxLength"/> characters. It prefixes every MCP
/// tool name (<c>{service}_{capability}</c>), and MCP limits tool names to 64 characters.
/// Only assignments to Kaleido's <c>ServiceName</c> properties with a compile-time constant value are
/// checked (literals, <c>const</c> fields and locals, constant interpolations, <c>nameof</c>); a value
/// read from configuration is checked by <c>AddKaleido</c> at startup.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ServiceNameAnalyzer : DiagnosticAnalyzer
{
    public const int MaxLength = 15;

    private static readonly ImmutableHashSet<string> OptionTypes =
        ImmutableHashSet.Create("Kaleido.KaleidoServiceOptions");

    private static readonly DiagnosticDescriptor LengthRule =
        new(
            DiagnosticIds.ServiceNameLength,
            "ServiceName is too long for MCP tool names",
            "ServiceName '{0}' is {1} characters; keep it at most {2}. It prefixes every MCP tool name ('{0}_…'), which MCP limits to 64 characters.",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinks.For(DiagnosticIds.ServiceNameLength),
            description: "AI agents call Kaleido capabilities as MCP tools named {service}_{capability}; the service name has a 15-character budget and the capability part 40 (KAL2016).");

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ServiceNameFormat,
            "ServiceName must be lowercase letters, digits, '-' and '_'",
            "ServiceName '{0}' is not a valid service name: it must start with a lowercase letter or digit and contain only lowercase letters (a-z), digits, '-' and '_' (e.g. '{1}').",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinks.For(DiagnosticIds.ServiceNameFormat),
            description: "ServiceName is the HTTP route prefix and the MCP tool-name prefix; AddKaleido rejects any value outside [a-z0-9][a-z0-9_-]* at startup with invalid_service_name.");

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

        var name = assignment.Left switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.Text,
            IdentifierNameSyntax identifier => identifier.Identifier.Text,
            _ => null,
        };

        if (name != "ServiceName" ||
            context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol is not IPropertySymbol property ||
            !OptionTypes.Contains(property.ContainingType.ToDisplayString()))
        {
            return;
        }

        var constant = context.SemanticModel.GetConstantValue(assignment.Right, context.CancellationToken);
        if (!constant.HasValue || constant.Value is not string value || value.Length == 0)
        {
            return;
        }

        var location = assignment.Right.GetLocation();

        if (value.Length > MaxLength)
        {
            context.ReportDiagnostic(Diagnostic.Create(LengthRule, location, value, value.Length, MaxLength));
        }

        if (IsValid(value))
        {
            return;
        }

        var suggested = new string(value.ToLowerInvariant().Where(IsAllowed).ToArray()).TrimStart('-', '_');
        context.ReportDiagnostic(
            Diagnostic.Create(Rule, location, value, suggested.Length > 0 ? suggested : "myservice"));
    }

    private static bool IsValid(string value) =>
        IsLowercaseLetterOrDigit(value[0]) && value.All(IsAllowed);

    private static bool IsAllowed(char c) => IsLowercaseLetterOrDigit(c) || c == '-' || c == '_';

    private static bool IsLowercaseLetterOrDigit(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
}
