using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Layout.CoverageExclusionAnalyzer>;

namespace Kaleido.Analyzers.Source.Layout.UnitTests;

public sealed class CoverageExclusionAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.Layout.CoverageExclusionAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.Layout.CoverageExclusionAnalyzer CreateSut() =>
        new();

    private static readonly DiagnosticResult Expected =
        new("KAL0020", DiagnosticSeverity.Warning);

    [Fact]
    public async Task OnATypeWithBehavior_Reports()
    {
        await RunAsync(@"
using System.Diagnostics.CodeAnalysis;

[{|#0:ExcludeFromCodeCoverage|}]
public sealed record Stamp(string Value)
{
    public bool IsEmpty => string.IsNullOrEmpty(Value);
}",
            Expected.WithLocation(0).WithArguments("Stamp"));
    }

    [Fact]
    public async Task OnADataType_NoDiagnostic()
    {
        await RunAsync(@"
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public sealed record Stamp(string Value)
{
    public int Count { get; init; }
}");
    }

    [Fact]
    public async Task ATypeWithBehavior_WithoutTheAttribute_NoDiagnostic()
    {
        await RunAsync("public sealed class Widget { public int Next() => 1; }");
    }
}
