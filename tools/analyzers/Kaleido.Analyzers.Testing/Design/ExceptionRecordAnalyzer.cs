using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Design;

/// <summary>
/// KAL1011 — exception types declared in test projects must not be records.
/// Record value-equality members and synthesized copy semantics are meaningless
/// and harmful on exception types, regardless of whether they are in production
/// or test code.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ExceptionRecordAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.ExceptionRecord,
            "Exception types must not be records",
            "Exception type '{0}' is declared as a record — exception classes must remain classes",
            "Kaleido.Tests",
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

        var baseType = type.BaseType;
        while (baseType is not null)
        {
            if (baseType.ToDisplayString() == "System.Exception")
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(Rule, type.Locations[0], type.Name));
                return;
            }

            baseType = baseType.BaseType;
        }
    }
}
