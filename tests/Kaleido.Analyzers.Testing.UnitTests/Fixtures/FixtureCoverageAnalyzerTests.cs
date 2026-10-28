using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kaleido.Analyzers.Testing.Fixtures.UnitTests;

/// <summary>
/// KAL1009 runs against whole compilations (assembly-name driven), so these
/// tests build CSharpCompilations directly rather than using the analyzer
/// test harness.
/// </summary>
public sealed class FixtureCoverageAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Testing.Fixtures.FixtureCoverageAnalyzer>
{
    private static readonly string KaleidoAssemblyPath =
        typeof(Kaleido.Exceptions.KaleidoConfigurationException).Assembly.Location;

    protected override FixtureCoverageAnalyzer CreateSut() =>
        new();

    private ImmutableArray<Diagnostic> RunAnalyzer(
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
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(Sut))
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

        // #458: blocking on its own - an Error, whatever the project's warnings-as-errors setting.
        Assert.Contains(
            diagnostics,
            d => d.Id == "KAL1009" && d.Severity == DiagnosticSeverity.Error);
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

    // #404: behavior is judged on the compiled source assembly (metadata), as in the repository.
    private ImmutableArray<Diagnostic> RunAgainst(string sourceAssemblyCode, string testSource = "")
    {
        var source = CSharpCompilation.Create(
            "Sample",
            [CSharpSyntaxTree.ParseText(sourceAssemblyCode)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location), .. RuntimeReferences()],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        using var image = new System.IO.MemoryStream();
        var emitted = source.Emit(image);
        Assert.True(emitted.Success, string.Join(System.Environment.NewLine, emitted.Diagnostics));

        var tests = CSharpCompilation.Create(
            "Sample.UnitTests",
            [CSharpSyntaxTree.ParseText(XunitStub + testSource)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location), MetadataReference.CreateFromImage(image.ToArray()), .. RuntimeReferences()],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return tests
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(Sut))
            .GetAnalyzerDiagnosticsAsync()
            .GetAwaiter()
            .GetResult();
    }

    private static MetadataReference[] RuntimeReferences() =>
        [.. RuntimeAssemblies
            .Select(name => System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(object).Assembly.Location)!, name))
            .Where(System.IO.File.Exists)
            .Select(path => MetadataReference.CreateFromFile(path))];

    private static readonly string[] RuntimeAssemblies = ["System.Runtime.dll", "netstandard.dll"];

    private static readonly string[] BothTwins = ["ResultOfTTests", "ResultTests"];

    private static readonly string[] GenericTwin = ["ResultOfTTests"];

    private const string XunitStub = "namespace Xunit { public class FactAttribute : System.Attribute { } }\n";

    private static string[] MissingFixtures(ImmutableArray<Diagnostic> diagnostics) =>
        [.. diagnostics
            .Where(d => d.Id == "KAL1009")
            .Select(d => d.GetMessage(System.Globalization.CultureInfo.InvariantCulture).Split('\'')[5])
            .Order(System.StringComparer.Ordinal)];

    [Theory]
    [InlineData("public sealed record Stamp(string Value) { public bool IsEmpty => Value.Length == 0; }")]
    [InlineData("public sealed record Stamp { public Stamp Next() => this; }")]
    [InlineData("public sealed record Stamp { public static implicit operator Stamp(int value) => new(); }")]
    [InlineData("public sealed class StampOptions { public int Size { get; set; } public void Validate() { } }")]
    [InlineData("[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage] public class Stamp { public void Work() { } }")]
    public void ATypeWithBehavior_NeedsItsFixture_WhateverItsKindNameOrAttributes(string declaration)
    {
        var name = declaration.Contains("StampOptions", System.StringComparison.Ordinal) ? "StampOptionsTests" : "StampTests";

        Assert.Equal([name], MissingFixtures(RunAgainst("namespace Sample { " + declaration + " }")));
    }

    [Theory]
    [InlineData("public sealed record Stamp(string Value, int Count);")]
    [InlineData("public sealed record Stamp { public string Value { get; init; } = \"\"; }")]
    [InlineData("public class StampException : System.Exception { public string Describe() => Message; }")]
    [InlineData("public sealed class StampAttribute : System.Attribute { public string Describe() => \"\"; }")]
    [InlineData("public static class StampExtensions { public static int Next(this int value) => value; }")]
    [InlineData("public class Stamp { [System.CodeDom.Compiler.GeneratedCode(\"gen\", \"1\")] public void Log() { } }")]
    public void ADataOrExemptType_NeedsNoFixture(string declaration)
    {
        Assert.Empty(MissingFixtures(RunAgainst("namespace Sample { " + declaration + " }")));
    }

    [Fact]
    public void EfCoreModelConfiguration_IsExempt()
    {
        Assert.Empty(MissingFixtures(RunAgainst(@"
namespace Microsoft.EntityFrameworkCore
{
    public interface IEntityTypeConfiguration<TEntity> { void Configure(object builder); }
    public abstract class DbContext { protected virtual void OnModelCreating(object builder) { } }
}
namespace Sample
{
    public sealed class Row { }
    public sealed class RowConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Row> { public void Configure(object builder) { } }
    public sealed class SampleDbContext : Microsoft.EntityFrameworkCore.DbContext { protected override void OnModelCreating(object builder) { } }
}")));
    }

    // Generic twins: each needs its own fixture, the generic one named by arity.
    [Fact]
    public void GenericTwins_EachNeedTheirFixture_NamedByArity()
    {
        const string twins = @"
namespace Sample
{
    public sealed record Result { public static Result Ok() => new(); }
    public sealed record Result<T> { public Result<T> Next() => this; }
}";

        Assert.Equal(BothTwins, MissingFixtures(RunAgainst(twins)));
        Assert.Equal(GenericTwin, MissingFixtures(RunAgainst(twins, "public class ResultTests { [Xunit.Fact] public void M() { } }")));
        Assert.Empty(MissingFixtures(RunAgainst(twins, @"
public class ResultTests { [Xunit.Fact] public void M() { } }
public class ResultOfTTests { [Xunit.Fact] public void M() { } }")));
    }
}
