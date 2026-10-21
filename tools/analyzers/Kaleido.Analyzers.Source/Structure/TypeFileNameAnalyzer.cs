using System.Collections.Immutable;
using System.IO;
using Kaleido.Analyzers.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.Layout;

/// <summary>
/// KAL0022 — a type with behavior (TypeBehavior) is declared in a file named after it:
/// <c>ProcessorContext</c> in <c>ProcessorContext.cs</c>, a generic twin by its arity
/// (<c>ProcessStepHandlerResult&lt;T&gt;</c> in <c>ProcessStepHandlerResultOfT.cs</c>), the parts of a
/// partial type in <c>{Name}.cs</c> or <c>{Name}.{Part}.cs</c>. So two types with behavior never share a
/// file, and a file shows which unit-test class it needs (<c>{Name}Tests</c>, #404). Data-only records,
/// interfaces, exceptions and attributes may still share a file.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TypeFileNameAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.TypeFileName,
            "A type with behavior must be declared in a file named after it",
            "Type '{0}' has behavior and must be declared in '{1}' (or a '{2}.*.cs' part), not '{3}' — a type with behavior has its own file and its own test class",
            "Kaleido.Layout",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSymbolAction(Analyze, SymbolKind.NamedType);
    }

    private static void Analyze(SymbolAnalysisContext context)
    {
        var type = (INamedTypeSymbol)context.Symbol;

        if (!TypeBehavior.HasBehavior(type))
        {
            return;
        }

        var name = TypeBehavior.ConventionName(type);

        foreach (var location in type.Locations)
        {
            if (location.SourceTree?.FilePath is not { Length: > 0 } path ||
                TypeBehavior.IsGeneratedFilePath(path))
            {
                continue;
            }

            var file = Path.GetFileNameWithoutExtension(path);

            if (string.Equals(file, name, System.StringComparison.Ordinal) ||
                file.StartsWith(name + ".", System.StringComparison.Ordinal))
            {
                continue;
            }

            context.ReportDiagnostic(
                Diagnostic.Create(Rule, location, type.ToDisplayString(), name + ".cs", name, Path.GetFileName(path)));
        }
    }
}
