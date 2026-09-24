using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Fixtures;

/// <summary>
/// KAL1004 — one fixture per subject under test: two fixture classes that
/// resolve to the same SUT type are flagged.
/// Scoped to unit-test projects via .editorconfig.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SingleFixturePerSutAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.SingleFixturePerSut,
            "Only one test fixture per subject under test",
            "Test fixture '{0}' duplicates '{1}' — both map to SUT '{2}'",
            "Kaleido.Tests",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true,
            customTags: WellKnownDiagnosticTags.CompilationEnd);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(startContext =>
        {
            var fixtures = new List<(INamedTypeSymbol Fixture, INamedTypeSymbol Sut)>();

            startContext.RegisterSymbolAction(
                symbolContext =>
                {
                    var type = (INamedTypeSymbol)symbolContext.Symbol;

                    if (!FixtureConventions.IsFixture(type))
                    {
                        return;
                    }

                    var sut =
                        FixtureConventions.ResolveSut(
                            symbolContext.Compilation,
                            type.Name,
                            symbolContext.CancellationToken);

                    if (sut is null)
                    {
                        return;
                    }

                    lock (fixtures)
                    {
                        fixtures.Add((type, sut));
                    }
                },
                SymbolKind.NamedType);

            startContext.RegisterCompilationEndAction(endContext =>
            {
                var grouped =
                    fixtures.GroupBy(f => f.Sut, SymbolEqualityComparer.Default);

                foreach (var group in grouped)
                {
                    var members =
                        group
                            .OrderBy(f => f.Fixture.Locations[0].SourceSpan.Start)
                            .ToList();

                    foreach (var extra in members.Skip(1))
                    {
                        endContext.ReportDiagnostic(
                            Diagnostic.Create(
                                Rule,
                                extra.Fixture.Locations[0],
                                extra.Fixture.Name,
                                members[0].Fixture.Name,
                                members[0].Sut.Name));
                    }
                }
            });
        });
    }
}
