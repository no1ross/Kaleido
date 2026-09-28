using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace Kaleido.Testing;

/// <summary>Compiler-diagnostic behavior for <see cref="AnalyzerTest{TAnalyzer}"/>.</summary>
internal enum CompilerDiagnostics
{
    /// <summary>Compiler errors in the test source fail the test.</summary>
    Errors,

    /// <summary>Compiler diagnostics are ignored (analyzer still runs).</summary>
    None
}

/// <summary>Expected analyzer diagnostic: id + severity + markup location + message args.</summary>
internal sealed record DiagnosticResult(
    string Id,
    DiagnosticSeverity Severity)
{
    public int? LocationIndex { get; private init; }

    public object[] Arguments { get; private init; } = [];

    public DiagnosticResult WithLocation(int markupKey) =>
        this with { LocationIndex = markupKey };

    public DiagnosticResult WithArguments(params object[] arguments) =>
        this with { Arguments = arguments };
}

/// <summary>Additional sources compiled alongside <see cref="AnalyzerTest{TAnalyzer}.TestCode"/>.</summary>
internal sealed class TestStateCollection
{
    public List<(string FileName, string Source)> Sources { get; } = new();
}

/// <summary>
/// Minimal analyzer-test harness replacing the deprecated Microsoft.CodeAnalysis.Testing
/// packages. Compiles sources (with {|#0:...|} markup), runs a single analyzer, and
/// verifies expected diagnostics by id, span, severity, and formatted message.
/// </summary>
internal sealed class AnalyzerTest<TAnalyzer>
    where TAnalyzer : DiagnosticAnalyzer, new()
{
    public string? TestCode { get; set; }

    public string AssemblyName { get; set; } = "TestProject";

    public CompilerDiagnostics CompilerDiagnostics { get; set; } = CompilerDiagnostics.Errors;

    public TestStateCollection TestState { get; } = new();

    public List<DiagnosticResult> ExpectedDiagnostics { get; } = new();

    public async Task RunAsync()
    {
        var sources =
            TestState
                .Sources
                .Select(x => (x.FileName, x.Source))
                .ToList();

        if (TestCode is not null)
        {
            sources.Insert(0, ("Test0.cs", TestCode));
        }

        var trees =
            new List<SyntaxTree>();

        var markup =
            new Dictionary<int, (string Path, TextSpan Span)>();

        foreach (var (fileName, rawSource) in sources)
        {
            var source =
                StripMarkup(
                    rawSource,
                    fileName,
                    markup);

            trees.Add(
                CSharpSyntaxTree.ParseText(
                    source,
                    path: fileName));
        }

        // Net80 reference-pack only (Basic.Reference.Assemblies) — matching the
        // old harness's ReferenceAssemblies.Net.Net80. Using the test process's
        // TPA would pull in real framework deps that collide with in-source stubs.
        var references =
            Basic.Reference.Assemblies.ReferenceAssemblies.Net80;

        // Enable all of the analyzer's descriptors — isEnabledByDefault:false rules
        // (e.g. KAL1006) must still be exercised by their own unit tests.
        var analyzer =
            new TAnalyzer();

        var compilationOptions =
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                specificDiagnosticOptions:
                    analyzer
                        .SupportedDiagnostics
                        .Select(x =>
                            new System.Collections.Generic.KeyValuePair
                                <string, ReportDiagnostic>(
                                x.Id,
                                x.DefaultSeverity switch
                                {
                                    DiagnosticSeverity.Error => ReportDiagnostic.Error,
                                    DiagnosticSeverity.Warning => ReportDiagnostic.Warn,
                                    DiagnosticSeverity.Info => ReportDiagnostic.Info,
                                    _ => ReportDiagnostic.Hidden
                                })));

        var compilation =
            CSharpCompilation.Create(
                AssemblyName,
                trees,
                references,
                compilationOptions);

        if (CompilerDiagnostics == CompilerDiagnostics.Errors)
        {
            var compilerErrors =
                compilation
                    .GetDiagnostics()
                    .Where(x => x.Severity == DiagnosticSeverity.Error)
                    .ToList();

            Assert.True(
                compilerErrors.Count == 0,
                string.Join(
                    "\n",
                    compilerErrors.Select(x =>
                        $"{x.Id}: {x.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}")));
        }

        var actual =
            await compilation
                .WithAnalyzers(
                    [new TAnalyzer()])
                .GetAnalyzerDiagnosticsAsync();

        foreach (var expected in ExpectedDiagnostics)
        {
            var matches =
                actual
                    .Where(x =>
                        x.Id == expected.Id &&
                        (expected.LocationIndex is null ||
                         LocationMatches(
                             x,
                             markup[expected.LocationIndex.Value])))
                    .ToList();

            Assert.NotEmpty(
                matches);

            if (expected.Arguments.Length > 0)
            {
                var match =
                    matches[0];

                var expectedMessage =
                    string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        match.Descriptor.MessageFormat.ToString(
                            System.Globalization.CultureInfo.InvariantCulture),
                        expected.Arguments);

                Assert.Contains(
                    matches,
                    x => x.GetMessage(
                            System.Globalization.CultureInfo.InvariantCulture) ==
                        expectedMessage);
            }
        }

        Assert.Equal(
            ExpectedDiagnostics.Count,
            actual.Length);
    }

    private static bool LocationMatches(
        Diagnostic diagnostic,
        (string Path, TextSpan Span) markup)
    {
        var span =
            diagnostic.Location.GetLineSpan();

        return
            span.Path == markup.Path &&
            diagnostic.Location.SourceSpan == markup.Span;
    }

    private static string StripMarkup(
        string source,
        string fileName,
        Dictionary<int, (string Path, TextSpan Span)> markup)
    {
        var builder =
            new System.Text.StringBuilder();

        var position =
            0;

        while (true)
        {
            var open =
                source.IndexOf(
                    "{|#",
                    position,
                    StringComparison.Ordinal);

            if (open < 0)
            {
                builder.Append(source[position..]);
                return builder.ToString();
            }

            var colon =
                source.IndexOf(
                    ':',
                    open + 3);

            var key =
                int.Parse(
                    source[(open + 3)..colon],
                    System.Globalization.CultureInfo.InvariantCulture);

            var close =
                source.IndexOf(
                    "|}",
                    colon,
                    StringComparison.Ordinal);

            builder.Append(source[position..open]);

            var spanStart =
                builder.Length;

            builder.Append(source[(colon + 1)..close]);

            markup[key] =
                (fileName,
                 new TextSpan(
                     spanStart,
                     close - colon - 1));

            position =
                close + 2;
        }
    }
}
