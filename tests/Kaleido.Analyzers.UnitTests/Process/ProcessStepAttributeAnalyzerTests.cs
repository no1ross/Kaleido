using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Process.ProcessStepAttributeAnalyzer>;

namespace Kaleido.Analyzers.Process.UnitTests;

public sealed class ProcessStepAttributeAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Process.ProcessStepAttributeAnalyzer>
{
    protected override global::Kaleido.Analyzers.Process.ProcessStepAttributeAnalyzer CreateSut() =>
        new();

    private const string ProcessStepStub = @"
namespace Kaleido.Processor
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ProcessStepAttribute : System.Attribute
    {
        public required string Version { get; init; }
        public required string DisplayName { get; init; }
        public required string Description { get; init; }
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2001", DiagnosticSeverity.Error);

    [Fact]
    public async Task Version_Empty_Reports()
    {
        await RunAsync(@"
using Kaleido.Processor;
[{|#0:ProcessStep(Version = """", DisplayName = ""Foo"", Description = ""Does foo."")|}]
public class MyStep { }
" + ProcessStepStub,
            Expected.WithLocation(0).WithArguments("ProcessStepAttribute"));
    }

    [Fact]
    public async Task DisplayName_Empty_Reports()
    {
        await RunAsync(@"
using Kaleido.Processor;
[{|#0:ProcessStep(Version = ""1.0.0"", DisplayName = """", Description = ""Does foo."")|}]
public class MyStep { }
" + ProcessStepStub,
            Expected.WithLocation(0).WithArguments("ProcessStepAttribute"));
    }

    [Fact]
    public async Task Description_WhitespaceOnly_Reports()
    {
        await RunAsync(@"
using Kaleido.Processor;
[{|#0:ProcessStep(Version = ""1.0.0"", DisplayName = ""Foo"", Description = ""   "")|}]
public class MyStep { }
" + ProcessStepStub,
            Expected.WithLocation(0).WithArguments("ProcessStepAttribute"));
    }

    [Fact]
    public async Task Valid_NoDiagnostic()
    {
        await RunAsync(@"
using Kaleido.Processor;
[ProcessStep(Version = ""1.0.0"", DisplayName = ""Foo"", Description = ""Does foo."")]
public class MyStep { }
" + ProcessStepStub);
    }
}
