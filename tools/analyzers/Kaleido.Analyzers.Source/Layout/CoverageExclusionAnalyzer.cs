using System.Collections.Immutable;
using System.Linq;
using Kaleido.Analyzers.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Source.Layout;

/// <summary>
/// KAL0020 — <c>[ExcludeFromCodeCoverage]</c> is for types without behavior (data holders). On a type
/// with behavior (TypeBehavior) it hides logic from the coverage gate; that logic is tested in the
/// type's own test class instead (#404).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CoverageExclusionAnalyzer : DiagnosticAnalyzer
{
    private const string ExcludeFromCodeCoverage = "System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute";

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.CoverageExclusionOnBehavior,
            "Do not exclude a type with behavior from code coverage",
            "Type '{0}' has behavior: remove [ExcludeFromCodeCoverage] and test it in its own test class",
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

        var attribute =
            type.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == ExcludeFromCodeCoverage);

        if (attribute is null || !TypeBehavior.HasBehavior(type))
        {
            return;
        }

        var location =
            attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
            ?? type.Locations.FirstOrDefault();

        if (location is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, location, type.ToDisplayString()));
        }
    }
}
