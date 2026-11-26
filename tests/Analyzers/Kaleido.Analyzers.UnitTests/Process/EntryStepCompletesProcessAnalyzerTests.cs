using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Process.EntryStepCompletesProcessAnalyzer>;

namespace Kaleido.Analyzers.Process.UnitTests;

public sealed class EntryStepCompletesProcessAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Process.EntryStepCompletesProcessAnalyzer>
{
    protected override global::Kaleido.Analyzers.Process.EntryStepCompletesProcessAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
namespace Kaleido.Process
{
    public interface IProcessStep { }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public sealed class DependsOnAttribute<TStep> : System.Attribute where TStep : IProcessStep { }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public sealed class AvailableAfterAttribute<TStep> : System.Attribute where TStep : IProcessStep { }

    [System.AttributeUsage(System.AttributeTargets.Class, AllowMultiple = true)]
    public sealed class AvailableUntilAttribute<TStep> : System.Attribute where TStep : IProcessStep { }
}

namespace Kaleido.Process.Execution
{
    public sealed class ProcessStepHandlerResult
    {
        public static ProcessStepHandlerResult Success() => new();
        public static ProcessStepHandlerResult Complete() => new();
        public static ProcessStepHandlerResult<TProcessStepResult> Complete<TProcessStepResult>(TProcessStepResult response) => new();
    }

    public sealed class ProcessStepHandlerResult<TProcessStepResult> { }

    public interface IProcessStepHandler<in TProcessStep>
    {
        System.Threading.Tasks.Task<ProcessStepHandlerResult> ExecuteAsync(TProcessStep processStep);
    }

    public interface IProcessStepHandler<in TProcessStep, TProcessStepResult>
    {
        System.Threading.Tasks.Task<ProcessStepHandlerResult<TProcessStepResult>> ExecuteAsync(TProcessStep processStep);
    }
}";

    private const string Steps = @"
public sealed class StartStep : Kaleido.Process.IProcessStep { }

[Kaleido.Process.AvailableAfter<StartStep>]
public sealed class CloseCaseStep : Kaleido.Process.IProcessStep { }

[Kaleido.Process.DependsOn<StartStep>]
public sealed class ArchiveStep : Kaleido.Process.IProcessStep { }

[Kaleido.Process.AvailableUntil<StartStep>]
public sealed class OnlyClosedByStep : Kaleido.Process.IProcessStep { }
";

    private static readonly DiagnosticResult EntryStepCompletes =
        new("KAL2022", DiagnosticSeverity.Warning);

    [Fact]
    public async Task CompleteForAnEntryStep_Reports()
    {
        await RunAsync(Steps + @"
public sealed class StartHandler : Kaleido.Process.Execution.IProcessStepHandler<StartStep>
{
    public System.Threading.Tasks.Task<Kaleido.Process.Execution.ProcessStepHandlerResult> ExecuteAsync(StartStep processStep) =>
        System.Threading.Tasks.Task.FromResult({|#0:Kaleido.Process.Execution.ProcessStepHandlerResult.Complete()|});
}
" + KaleidoStubs,
            EntryStepCompletes.WithLocation(0).WithArguments("StartHandler", "StartStep"));
    }

    // AvailableUntil only closes a step: it does not make it run after another.
    [Fact]
    public async Task TypedCompleteForAStepOnlyClosedByAnother_Reports()
    {
        await RunAsync(Steps + @"
public sealed class OnlyClosedByHandler : Kaleido.Process.Execution.IProcessStepHandler<OnlyClosedByStep, string>
{
    public System.Threading.Tasks.Task<Kaleido.Process.Execution.ProcessStepHandlerResult<string>> ExecuteAsync(OnlyClosedByStep processStep) =>
        System.Threading.Tasks.Task.FromResult({|#0:Kaleido.Process.Execution.ProcessStepHandlerResult.Complete(""closed"")|});
}
" + KaleidoStubs,
            EntryStepCompletes.WithLocation(0).WithArguments("OnlyClosedByHandler", "OnlyClosedByStep"));
    }

    [Fact]
    public async Task CompleteForAStepThatRunsAfterOthers_NoDiagnostic()
    {
        await RunAsync(Steps + @"
public sealed class CloseCaseHandler : Kaleido.Process.Execution.IProcessStepHandler<CloseCaseStep>
{
    public System.Threading.Tasks.Task<Kaleido.Process.Execution.ProcessStepHandlerResult> ExecuteAsync(CloseCaseStep processStep) =>
        System.Threading.Tasks.Task.FromResult(Kaleido.Process.Execution.ProcessStepHandlerResult.Complete());
}

public sealed class ArchiveHandler : Kaleido.Process.Execution.IProcessStepHandler<ArchiveStep, string>
{
    public System.Threading.Tasks.Task<Kaleido.Process.Execution.ProcessStepHandlerResult<string>> ExecuteAsync(ArchiveStep processStep) =>
        System.Threading.Tasks.Task.FromResult(Kaleido.Process.Execution.ProcessStepHandlerResult.Complete(""archived""));
}
" + KaleidoStubs);
    }

    [Fact]
    public async Task OtherResultsOrCallsOutsideAHandler_NoDiagnostic()
    {
        await RunAsync(Steps + @"
public sealed class StartHandler : Kaleido.Process.Execution.IProcessStepHandler<StartStep>
{
    public System.Threading.Tasks.Task<Kaleido.Process.Execution.ProcessStepHandlerResult> ExecuteAsync(StartStep processStep) =>
        System.Threading.Tasks.Task.FromResult(Kaleido.Process.Execution.ProcessStepHandlerResult.Success());
}

public static class NotAHandler
{
    public static object Run() => Kaleido.Process.Execution.ProcessStepHandlerResult.Complete();
}
" + KaleidoStubs);
    }
}
