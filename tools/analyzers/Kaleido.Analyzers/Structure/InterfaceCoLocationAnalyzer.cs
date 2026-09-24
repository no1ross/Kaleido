using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Layout;

/// <summary>
/// KAL0015 — the interface for a concrete class belongs in the same file as
/// its implementation (IProcessRuntime lives in ProcessRuntime.cs). Provider
/// contracts are exempt by construction: an interface with no same-named
/// implementation in the assembly (IProcessContextStore, IProcessStepHandler)
/// has nothing to co-locate with.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class InterfaceCoLocationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.InterfaceCoLocation,
            "Interface must live in the same file as its implementation",
            "Interface '{0}' should be declared in '{1}' alongside '{2}' — an interface and its concrete class share a file",
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
        var interfaceSymbol = (INamedTypeSymbol)context.Symbol;

        if (interfaceSymbol.TypeKind != TypeKind.Interface ||
            interfaceSymbol.Name.Length < 2 ||
            interfaceSymbol.Name[0] != 'I' ||
            !char.IsUpper(interfaceSymbol.Name[1]) ||
            interfaceSymbol.ContainingType is not null ||
            interfaceSymbol.IsImplicitlyDeclared)
        {
            return;
        }

        var implName = interfaceSymbol.Name.Substring(1);

        var impl =
            context.Compilation
                .GetSymbolsWithName(
                    implName, SymbolFilter.Type, context.CancellationToken)
                .OfType<INamedTypeSymbol>()
                .FirstOrDefault(t =>
                    t.TypeKind == TypeKind.Class &&
                    SymbolEqualityComparer.Default.Equals(
                        t.ContainingAssembly, interfaceSymbol.ContainingAssembly) &&
                    t.AllInterfaces.Any(i =>
                        SymbolEqualityComparer.Default.Equals(
                            i.OriginalDefinition, interfaceSymbol.OriginalDefinition)));

        if (impl is null)
        {
            return;
        }

        var implTrees =
            impl.DeclaringSyntaxReferences
                .Select(r => r.SyntaxTree)
                .ToImmutableHashSet();

        var location =
            interfaceSymbol.Locations
                .FirstOrDefault(l =>
                    l.SourceTree is not null &&
                    !implTrees.Contains(l.SourceTree));

        if (location is null)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule, location, interfaceSymbol.Name, implName + ".cs", implName));
    }
}
