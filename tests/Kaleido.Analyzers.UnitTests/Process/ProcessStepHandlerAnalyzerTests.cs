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
    public interface IProcessStep { }
    public interface IProcessStepHandler<TStep> { }
    public interface IProcessStepHandler<TStep, TResult> { }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2008", DiagnosticSeverity.Warning);

    [Fact]
    public async Task StepWithoutHandler_Reports()
    {
        await RunAsync(@"
public class {|#0:UpsertStep|} : Kaleido.Processor.IProcessStep { }
" + KaleidoStubs,
            Expected.WithLocation(0).WithArguments("UpsertStep"));
    }

    [Fact]
    public async Task RecordStepWithoutHandler_Reports()
    {
        await RunAsync(@"
public sealed record {|#0:UpsertStep|} : Kaleido.Processor.IProcessStep;
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }
" + KaleidoStubs,
            Expected.WithLocation(0).WithArguments("UpsertStep"));
    }

    [Fact]
    public async Task StepWithHandler_NoDiagnostic()
    {
        await RunAsync(@"
public class UpsertStep : Kaleido.Processor.IProcessStep { }
public class UpsertHandler : Kaleido.Processor.IProcessStepHandler<UpsertStep> { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task StepWithTypedHandler_NoDiagnostic()
    {
        await RunAsync(@"
public class UpsertStep : Kaleido.Processor.IProcessStep { }
public class UpsertHandler : Kaleido.Processor.IProcessStepHandler<UpsertStep, string> { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task AbstractStep_NoDiagnostic()
    {
        await RunAsync(@"
public abstract class StepBase : Kaleido.Processor.IProcessStep { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task MultipleSteps_OneMissingHandler_ReportsOnlyMissing()
    {
        await RunAsync(@"
public class UpsertStep : Kaleido.Processor.IProcessStep { }
public class {|#0:ValidateStep|} : Kaleido.Processor.IProcessStep { }
public class UpsertHandler : Kaleido.Processor.IProcessStepHandler<UpsertStep> { }
" + KaleidoStubs,
            Expected.WithLocation(0).WithArguments("ValidateStep"));
    }
}
