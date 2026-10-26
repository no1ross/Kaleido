using Kaleido.Testing;
using Microsoft.CodeAnalysis;
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
        public required string DisplayName { get; init; }
        public required string Description { get; init; }
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2001", DiagnosticSeverity.Error);

    [Fact]
    public async Task DisplayName_Empty_Reports()
    {
        await RunAsync(@"
using Kaleido.Processor;
[{|#0:ProcessStep(DisplayName = """", Description = ""Does foo."")|}]
public class MyStep { }
" + ProcessStepStub,
            Expected.WithLocation(0).WithArguments("ProcessStepAttribute"));
    }

    [Fact]
    public async Task Description_WhitespaceOnly_Reports()
    {
        await RunAsync(@"
using Kaleido.Processor;
[{|#0:ProcessStep(DisplayName = ""Foo"", Description = ""   "")|}]
public class MyStep { }
" + ProcessStepStub,
            Expected.WithLocation(0).WithArguments("ProcessStepAttribute"));
    }

    [Fact]
    public async Task Valid_NoDiagnostic()
    {
        await RunAsync(@"
using Kaleido.Processor;
[ProcessStep(DisplayName = ""Foo"", Description = ""Does foo."")]
public class MyStep { }
" + ProcessStepStub);
    }

    // Attribute arguments are constants: a concatenation, a const or nameof(...) is as good as a literal.
    [Fact]
    public async Task ConstantExpressions_NoDiagnostic()
    {
        await RunAsync(@"
using Kaleido.Processor;
public static class Texts { public const string Name = ""Foo""; }
[ProcessStep(DisplayName = Texts.Name, Description = ""Does foo. "" + nameof(MyStep))]
public class MyStep { }
" + ProcessStepStub);
    }

    [Fact]
    public async Task ConstantExpressionThatIsEmpty_Reports()
    {
        await RunAsync(@"
using Kaleido.Processor;
public static class Texts { public const string Empty = """"; }
[{|#0:ProcessStep(DisplayName = ""Foo"", Description = Texts.Empty + """")|}]
public class MyStep { }
" + ProcessStepStub,
            Expected.WithLocation(0).WithArguments("ProcessStepAttribute"));
    }
}
