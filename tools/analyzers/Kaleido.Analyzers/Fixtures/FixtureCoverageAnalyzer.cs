using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Fixtures;

/// <summary>
/// KAL1009 — every testable type in the source assembly must have a
/// {TypeName}Tests fixture in the matching unit-test project. Testable means:
/// public or internal class, non-static, non-abstract, with behavior (at
/// least one ordinary method). DTOs, records, exceptions, attributes, and
/// static extension classes are exempt.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixtureCoverageAnalyzer : DiagnosticAnalyzer
{
    private static readonly string[] DtoSuffixes =
        ["Options", "Settings", "Request", "Response", "Metadata", "Details", "Parameters", "Envelope", "Descriptor", "Summary", "Item", "Message", "Event"];

    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.FixtureCoverage,
            "Testable source type has no unit-test fixture",
            "Type '{0}' in '{1}' has no '{2}' fixture — every testable type must have a unit-test fixture",
            "Kaleido.Tests",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationAction(Analyze);
    }

    private static void Analyze(CompilationAnalysisContext context)
    {
        var assemblyName = context.Compilation.AssemblyName;

        if (assemblyName is null ||
            !assemblyName.EndsWith(".UnitTests", System.StringComparison.Ordinal))
        {
            return;
        }

        var sourceAssemblyName =
            assemblyName.Substring(0, assemblyName.Length - ".UnitTests".Length);

        IAssemblySymbol? sourceAssembly = null;

        foreach (var reference in context.Compilation.References)
        {
            if (context.Compilation.GetAssemblyOrModuleSymbol(reference)
                    is IAssemblySymbol candidate &&
                candidate.Name == sourceAssemblyName)
            {
                sourceAssembly = candidate;
                break;
            }
        }

        if (sourceAssembly is null)
        {
            return;
        }

        var missing = new List<(INamedTypeSymbol Type, string FixtureName)>();

        foreach (var type in FixtureConventions.EnumerateTypes(
                     sourceAssembly.GlobalNamespace))
        {
            if (!IsTestable(type))
            {
                continue;
            }

            var fixtureName = type.Name + FixtureConventions.TestsSuffix;

            var fixtureExists =
                context.Compilation
                    .GetSymbolsWithName(
                        fixtureName, SymbolFilter.Type, context.CancellationToken)
                    .OfType<INamedTypeSymbol>()
                    .Any(t => FixtureConventions.IsFixture(t));

            if (!fixtureExists)
            {
                missing.Add((type, fixtureName));
            }
        }

        foreach (var (type, fixtureName) in missing)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    Location.None,
                    type.ToDisplayString(),
                    sourceAssemblyName,
                    fixtureName));
        }
    }

    private static bool IsTestable(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class ||
            type.IsAbstract ||
            type.IsStatic ||
            type.IsImplicitlyDeclared ||
            type.IsRecord ||
            type.ContainingType is not null ||
            type.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Internal))
        {
            return false;
        }

        if (InheritsFrom(type, "System.Exception") ||
            InheritsFrom(type, "System.Attribute"))
        {
            return false;
        }

        if (DtoSuffixes.Any(s => type.Name.EndsWith(s, System.StringComparison.Ordinal)))
        {
            return false;
        }

        // has behavior: at least one ordinary non-implicit method
        // (registered services always qualify — services have methods)
        return type.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(m =>
                m.MethodKind == MethodKind.Ordinary &&
                !m.IsImplicitlyDeclared);
    }

    private static bool InheritsFrom(INamedTypeSymbol type, string baseTypeName)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == baseTypeName)
            {
                return true;
            }
        }

        return false;
    }
}
