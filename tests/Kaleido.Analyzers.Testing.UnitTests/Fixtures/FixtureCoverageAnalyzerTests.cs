using System.Collections.Immutable;
using Kaleido.Analyzers.Testing.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace Kaleido.Analyzers.Testing.UnitTests;

/// <summary>
/// KAL1009 runs against whole compilations (assembly-name driven), so these
/// tests build CSharpCompilations directly rather than using the analyzer
/// test harness.
/// </summary>
public sealed class FixtureCoverageAnalyzerTests
{
    private static readonly string KaleidoAssemblyPath =
        typeof(Kaleido.Exceptions.KaleidoConfigurationException).Assembly.Location;

    private static ImmutableArray<Diagnostic> RunAnalyzer(
        string testSource,
        string testAssemblyName = "Kaleido.UnitTests")
    {
        var compilation =
            CSharpCompilation.Create(
                testAssemblyName,
                [CSharpSyntaxTree.ParseText(testSource)],
                [
                    MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                    MetadataReference.CreateFromFile(KaleidoAssemblyPath),
                ],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return compilation
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new FixtureCoverageAnalyzer()))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();
    }

    [Fact]
    public void MissingFixture_ForTestableType_Reports()
    {
        var diagnostics = RunAnalyzer(@"
namespace Kaleido.UnitTests
{
    public class EmptyTests
    {
        public void M() { }
    }
}");

        Assert.Contains(
            diagnostics,
            d => d.Id == "KAL1009");
    }

    [Fact]
    public void ExistingFixture_ForTestableType_NotReportedForThatType()
    {
        var diagnostics = RunAnalyzer(@"
namespace Xunit
{
    public class FactAttribute : System.Attribute { }
}
namespace Kaleido.UnitTests
{
    public class DataTypeMapperTests
    {
        [Xunit.Fact]
        public void M_S_E() { }
    }
}");

        Assert.DoesNotContain(
            diagnostics,
            d => d.Id == "KAL1009" &&
                 d.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Contains("DataTypeMapperTests"));
    }

    [Fact]
    public void NonUnitTestAssembly_NoDiagnostics()
    {
        var diagnostics = RunAnalyzer(@"
namespace Whatever
{
    public class EmptyTests
    {
        public void M() { }
    }
}",
            testAssemblyName: "Something.Else");

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void TypeWithExcludeFromCodeCoverage_NoDiagnostic()
    {
        // [ExcludeFromCodeCoverage] is the explicit opt-out — the rule must not flag
        // types that carry this attribute even when they are otherwise testable.
        var diagnostics = RunAnalyzer(@"
namespace System.Diagnostics.CodeAnalysis
{
    public sealed class ExcludeFromCodeCoverageAttribute : System.Attribute { }
}
namespace Kaleido.UnitTests
{
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class InfrastructureHelper
    {
        public void DoWork() { }
    }
}");

        Assert.DoesNotContain(
            diagnostics,
            d => d.Id == "KAL1009" &&
                 d.GetMessage(System.Globalization.CultureInfo.InvariantCulture)
                  .Contains("InfrastructureHelper"));
    }
}
