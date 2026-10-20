using System.Collections.Immutable;
using System.Text;
using Kaleido.Analyzers.Process;
using Kaleido.Analyzers.Queryable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers;

/// <summary>
/// KAL2019 — a process step, query source or query view whose type name makes an MCP tool name
/// longer than the budget. Tools are named <c>{service}_{capability}</c> in snake_case, where the
/// capability part is the type name without its <c>Step</c>, <c>QueryContextSource</c>,
/// <c>QueryViewSource</c>, <c>ViewSource</c> or <c>Source</c> suffix; MCP limits tool names to 64
/// characters, so the capability part may use at most <see cref="MaxCapabilityLength"/> (the service
/// name has 15, KAL2020). Must stay in step with <c>ToolNameBuilder</c> in Kaleido.Http.Mcp.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ToolNameLengthAnalyzer : DiagnosticAnalyzer
{
    public const int MaxCapabilityLength = 40;

    private const string InformationStepInterfaceFullName = "Kaleido.Processor.IInformationStep";

    private static readonly string[] Suffixes =
        ["QueryContextSource", "QueryViewSource", "ViewSource", "Source", "Step"];

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ToolNameLength,
            "Capability type name is too long for an MCP tool name",
            "'{0}' becomes the MCP tool name part '{1}' ({2} characters); keep it at most {3} so '{{service}}_{1}' fits MCP's 64-character limit. Shorten the type name.",
            "Kaleido.Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            helpLinkUri: HelpLinks.For(DiagnosticIds.ToolNameLength),
            description: "AI agents call Kaleido capabilities as MCP tools named {service}_{capability}. MCP limits tool names to 64 characters; longer names are cut and given a hash, which agents read badly.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    /// <summary>The capability part of the tool name for a type name (same rule as ToolNameBuilder).</summary>
    public static string ToolNamePart(string typeName) =>
        ToSnakeCase(TrimSuffix(typeName));

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (type.TypeKind != TypeKind.Class || type.IsAbstract || !IsTool(type))
        {
            return;
        }

        var part = ToolNamePart(type.Name);

        if (part.Length <= MaxCapabilityLength)
        {
            return;
        }

        var location = type.Locations.Length > 0 ? type.Locations[0] : Location.None;

        context.ReportDiagnostic(
            Diagnostic.Create(Rule, location, type.Name, part, part.Length, MaxCapabilityLength));
    }

    // Information steps are never tools (they are answered through information requests).
    private static bool IsTool(INamedTypeSymbol type) =>
        (ProcessStepSymbols.ImplementsProcessStep(type) &&
         !ProcessStepSymbols.ImplementsProcessStep(type, InformationStepInterfaceFullName)) ||
        QueryableSymbols.IsQuerySource(type) ||
        QueryableSymbols.IsQueryView(type);

    private static string TrimSuffix(string typeName)
    {
        foreach (var suffix in Suffixes)
        {
            if (typeName.Length > suffix.Length &&
                typeName.EndsWith(suffix, System.StringComparison.Ordinal))
            {
                return typeName.Substring(0, typeName.Length - suffix.Length);
            }
        }

        return typeName;
    }

    private static string ToSnakeCase(string value)
    {
        var builder = new StringBuilder(value.Length + 8);

        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];

            if (current is >= 'A' and <= 'Z')
            {
                var previous = i > 0 ? value[i - 1] : '\0';
                var next = i + 1 < value.Length ? value[i + 1] : '\0';

                if (i > 0 &&
                    (IsLower(previous) || IsDigit(previous) || (IsUpper(previous) && IsLower(next))))
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }
            else if (IsLower(current) || IsDigit(current))
            {
                builder.Append(current);
            }
            else
            {
                builder.Append('_');
            }
        }

        var snake = builder.ToString();

        while (snake.Contains("__"))
        {
            snake = snake.Replace("__", "_");
        }

        return snake.Trim('_');
    }

    private static bool IsLower(char c) => c is >= 'a' and <= 'z';

    private static bool IsUpper(char c) => c is >= 'A' and <= 'Z';

    private static bool IsDigit(char c) => c is >= '0' and <= '9';
}
