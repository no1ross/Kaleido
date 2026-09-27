using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Process.ProcessStepHandlerAnalyzer>;

namespace Kaleido.Analyzers.UnitTests.Process;

public sealed class ProcessStepHandlerAnalyzerTests
{
    private const string KaleidoStubs = @"
namespace Kaleido.Process
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
[Kaleido.Process.ProcessStep(Name = ""upsert"", Version = ""1"")]
public class {|#0:UpsertStep|} { }
" + KaleidoStubs,
            Expected.WithLocation(0).WithArguments("UpsertStep"));
    }

    [Fact]
    public async Task StepWithHandler_NoDiagnostic()
    {
        await RunAsync(@"
[Kaleido.Process.ProcessStep(Name = ""upsert"", Version = ""1"")]
public class UpsertStep { }
public class UpsertHandler : Kaleido.Process.IProcessStepHandler<UpsertStep> { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task StepWithTypedHandler_NoDiagnostic()
    {
        await RunAsync(@"
[Kaleido.Process.ProcessStep(Name = ""upsert"", Version = ""1"")]
public class UpsertStep { }
public class UpsertHandler : Kaleido.Process.IProcessStepHandler<UpsertStep, string> { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task MultipleSteps_OneMissingHandler_ReportsOnlyMissing()
    {
        await RunAsync(@"
[Kaleido.Process.ProcessStep(Name = ""upsert"", Version = ""1"")]
public class UpsertStep { }
[Kaleido.Process.ProcessStep(Name = ""validate"", Version = ""1"")]
public class {|#0:ValidateStep|} { }
public class UpsertHandler : Kaleido.Process.IProcessStepHandler<UpsertStep> { }
" + KaleidoStubs,
            Expected.WithLocation(0).WithArguments("ValidateStep"));
    }
}
