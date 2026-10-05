using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Process.ProcessStepHandlerAnalyzer>;

namespace Kaleido.Analyzers.Process.UnitTests;

public sealed class ProcessStepHandlerAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Process.ProcessStepHandlerAnalyzer>
{
    protected override global::Kaleido.Analyzers.Process.ProcessStepHandlerAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
namespace Kaleido.Processor
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ProcessStepAttribute : System.Attribute
    {
        public required string Name { get; init; }
        public required string Version { get; init; }
    }
    public interface IProcessStepHandler<TStep> { }
    public interface IProcessStepHandler<TStep, TResult> { }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2008", DiagnosticSeverity.Warning);

    [Fact]
    public async Task StepWithoutHandler_Reports()
    {
        await RunAsync(@"
[Kaleido.Processor.ProcessStep(Name = ""upsert"", Version = ""1"")]
public class {|#0:UpsertStep|} { }
" + KaleidoStubs,
            Expected.WithLocation(0).WithArguments("UpsertStep"));
    }

    [Fact]
    public async Task StepWithHandler_NoDiagnostic()
    {
        await RunAsync(@"
[Kaleido.Processor.ProcessStep(Name = ""upsert"", Version = ""1"")]
public class UpsertStep { }
public class UpsertHandler : Kaleido.Processor.IProcessStepHandler<UpsertStep> { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task StepWithTypedHandler_NoDiagnostic()
    {
        await RunAsync(@"
[Kaleido.Processor.ProcessStep(Name = ""upsert"", Version = ""1"")]
public class UpsertStep { }
public class UpsertHandler : Kaleido.Processor.IProcessStepHandler<UpsertStep, string> { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task MultipleSteps_OneMissingHandler_ReportsOnlyMissing()
    {
        await RunAsync(@"
[Kaleido.Processor.ProcessStep(Name = ""upsert"", Version = ""1"")]
public class UpsertStep { }
[Kaleido.Processor.ProcessStep(Name = ""validate"", Version = ""1"")]
public class {|#0:ValidateStep|} { }
public class UpsertHandler : Kaleido.Processor.IProcessStepHandler<UpsertStep> { }
" + KaleidoStubs,
            Expected.WithLocation(0).WithArguments("ValidateStep"));
    }
}
