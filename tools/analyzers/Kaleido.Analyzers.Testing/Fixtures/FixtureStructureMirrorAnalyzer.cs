using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Kaleido.Analyzers.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures;

/// <summary>
/// KAL1003 — the fixture's file location mirrors the SUT's location:
/// src/{Project}/{path}/Sut.cs — tests/{TestProject}/{path}/SutTests.cs.
/// Scoped to unit-test projects via .editorconfig. A SUT in a referenced
/// assembly (every src/ project, for a unit-test project) has no syntax in the
/// compilation: its file is found among the src/ paths tests/Directory.Build.props
/// passes as AdditionalFiles, by its convention name (#405). Generic twins use the
/// arity convention (XOfT.cs / XOfTTests.cs, #404).
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

        var sutPath = SutPath(context, sut);

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
        var expectedFile = TypeBehavior.ConventionName(sut) + "Tests.cs";

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
                    string.IsNullOrEmpty(sutDir) ? expectedFile : sutDir + Path.DirectorySeparatorChar + expectedFile,
                    sutRel));
        }
    }

    private static string? SutPath(SymbolAnalysisContext context, INamedTypeSymbol sut)
    {
        var declared = sut.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree.FilePath;

        if (declared is not null)
        {
            return declared;
        }

        var separator = Path.DirectorySeparatorChar;
        var project = separator + "src" + separator + sut.ContainingAssembly.Name + separator;
        var fileName = TypeBehavior.ConventionName(sut) + ".cs";

        var candidates =
            context.Options.AdditionalFiles
                .Select(f => f.Path.Replace('/', separator))
                .Where(p =>
                    p.IndexOf(project, System.StringComparison.OrdinalIgnoreCase) >= 0 &&
                    string.Equals(Path.GetFileName(p), fileName, System.StringComparison.OrdinalIgnoreCase))
                .ToList();

        if (candidates.Count <= 1)
        {
            return candidates.FirstOrDefault();
        }

        // The same file name in several folders: the one whose folder matches the namespace.
        var assemblyName = sut.ContainingAssembly.Name;
        var ns = sut.ContainingNamespace.ToDisplayString();
        var folder = ns.StartsWith(assemblyName + ".", System.StringComparison.Ordinal)
            ? ns.Substring(assemblyName.Length + 1).Replace('.', separator)
            : string.Empty;

        return candidates.FirstOrDefault(p =>
                string.Equals(
                    Path.GetDirectoryName(DropFirstSegment(RelativeAfterRoot(p, "src") ?? p)),
                    folder,
                    System.StringComparison.OrdinalIgnoreCase))
            ?? candidates[0];
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
