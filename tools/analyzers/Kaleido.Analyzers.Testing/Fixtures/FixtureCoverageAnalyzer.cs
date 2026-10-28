using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Kaleido.Analyzers.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures;

/// <summary>
/// KAL1009 — every type with behavior in the source assembly must have its own
/// fixture in the matching unit-test project, named after the type's convention
/// name ({TypeName}Tests, or {TypeName}OfTTests for a generic twin). Behavior is
/// decided by TypeBehavior (#404): records count like classes, and neither a
/// name suffix nor [ExcludeFromCodeCoverage] exempts a type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixtureCoverageAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.FixtureCoverage,
            "Source type with behavior has no unit-test fixture",
            "Type '{0}' in '{1}' has no '{2}' fixture — every type with behavior must have its own unit-test fixture",
            "Kaleido.Tests",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            customTags: [WellKnownDiagnosticTags.CompilationEnd]);

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
            if (!TypeBehavior.HasBehavior(type))
            {
                continue;
            }

            var fixtureName = TypeBehavior.ConventionName(type) + FixtureConventions.TestsSuffix;

            var fixtureExists =
                context.Compilation
                    .GetSymbolsWithName(
                        fixtureName, SymbolFilter.Type, context.CancellationToken)
                    .OfType<INamedTypeSymbol>()
                    .Any(FixtureConventions.IsFixture);

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

}
