using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures;

/// <summary>
/// KAL1003 G�� the fixture's file location mirrors the SUT's location:
/// src/{Project}/{path}/Sut.cs G�� tests/{TestProject}/{path}/SutTests.cs.
/// Scoped to unit-test projects via .editorconfig.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FixtureStructureMirrorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule =
        new(
            DiagnosticIds.FixtureStructureMirror,
            "Test fixture location must mirror the subject under test",
            "Test fixture '{0}' must live at '{1}' (mirroring SUT path '{2}')",
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

        if (!FixtureConventions.IsUnitTestAssembly(context.Compilation) ||
            !FixtureConventions.IsFixture(type))
        {
            return;
        }

        var sut =
            FixtureConventions.ResolveSut(
                context.Compilation,
                type.Name,
                context.CancellationToken);

        if (sut is null)
        {
            return;
        }

        var fixturePath = type.Locations[0].SourceTree?.FilePath;

        var sutPath =
            sut.DeclaringSyntaxReferences
                .FirstOrDefault()
                ?.SyntaxTree.FilePath;

        if (fixturePath is null || sutPath is null)
        {
            return;
        }

        var fixtureRel = RelativeAfterRoot(fixturePath, "tests");
        var sutRel = RelativeAfterRoot(sutPath, "src");

        if (fixtureRel is null || sutRel is null)
        {
            return;
        }

        // Strip {Project}/ then compare directory + file name
        var fixtureDir = Path.GetDirectoryName(DropFirstSegment(fixtureRel));
        var sutDir = Path.GetDirectoryName(DropFirstSegment(sutRel));
        var expectedFile = sut.Name + "Tests.cs";

        if (!string.Equals(fixtureDir, sutDir, System.StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(
                Path.GetFileName(fixturePath),
                expectedFile,
                System.StringComparison.OrdinalIgnoreCase))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Rule,
                    type.Locations[0],
                    type.Name,
                    sutDir is null ? expectedFile : Path.Combine(sutDir, expectedFile),
                    sutRel));
        }
    }

    /// <summary>Returns the path after the first {root}/{anything}/ segment pair.</summary>
    private static string? RelativeAfterRoot(string path, string rootName)
    {
        var normalized = path.Replace('/', Path.DirectorySeparatorChar);
        var marker =
            Path.DirectorySeparatorChar + rootName + Path.DirectorySeparatorChar;
        var index =
            normalized.LastIndexOf(marker, System.StringComparison.OrdinalIgnoreCase);

        return index < 0 ? null : normalized.Substring(index + marker.Length);
    }

    private static string DropFirstSegment(string relativePath)
    {
        var index = relativePath.IndexOf(Path.DirectorySeparatorChar);
        return index < 0 ? relativePath : relativePath.Substring(index + 1);
    }
}
