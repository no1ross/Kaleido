using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Design;

/// <summary>
/// KAL0004 — exceptions must not be records (AGENTS.md: "Do NOT convert
/// exception classes to records"). Record value-equality members and the
/// synthesized copy semantics are meaningless/harmful on exception types.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExceptionRecordAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ExceptionRecord,
            "Exception types must not be records",
            "Exception type '{0}' is declared as a record — exception classes must remain classes",
            "Kaleido.Design",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
    }

    private static void AnalyzeType(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (!type.IsRecord || type.TypeKind == TypeKind.Struct)
        {
            return;
        }

        if (type.DerivesFrom("System.Exception"))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(Rule, type.Locations[0], type.Name));
        }
    }
}
