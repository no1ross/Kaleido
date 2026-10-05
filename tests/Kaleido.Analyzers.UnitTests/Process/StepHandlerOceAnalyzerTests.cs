using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Process.StepHandlerOceAnalyzer>;

namespace Kaleido.Analyzers.Process.UnitTests;

public sealed class StepHandlerOceAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Process.StepHandlerOceAnalyzer>
{
    protected override global::Kaleido.Analyzers.Process.StepHandlerOceAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
namespace Kaleido.Processor
{
    public interface IProcessStepHandler<TStep>
    {
        System.Threading.Tasks.Task<int> ExecuteAsync(
            TStep step,
            object context,
            System.Threading.CancellationToken cancellationToken = default);
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2004", DiagnosticSeverity.Warning);

    [Fact]
    public async Task BareExceptionCatch_Reports()
    {
        await RunAsync(@"
public class MyStep { }
public class MyHandler : Kaleido.Processor.IProcessStepHandler<MyStep>
{
    public System.Threading.Tasks.Task<int> ExecuteAsync(
        MyStep step, object context,
        System.Threading.CancellationToken cancellationToken = default)
    {
        try { }
        {|#0:catch (System.Exception) { }|}
        return System.Threading.Tasks.Task.FromResult(0);
    }
}
" + KaleidoStubs,
            Expected.WithLocation(0).WithArguments("MyHandler"));
    }

    [Fact]
    public async Task ExceptionCatch_WithOceFilter_NoDiagnostic()
    {
        await RunAsync(@"
public class MyStep { }
public class MyHandler : Kaleido.Processor.IProcessStepHandler<MyStep>
{
    public System.Threading.Tasks.Task<int> ExecuteAsync(
        MyStep step, object context,
        System.Threading.CancellationToken cancellationToken = default)
    {
        try { }
        catch (System.Exception ex) when (ex is not System.OperationCanceledException) { }
        return System.Threading.Tasks.Task.FromResult(0);
    }
}
" + KaleidoStubs);
    }

    [Fact]
    public async Task ExceptionCatch_WithPrecedingOceCatch_NoDiagnostic()
    {
        await RunAsync(@"
public class MyStep { }
public class MyHandler : Kaleido.Processor.IProcessStepHandler<MyStep>
{
    public System.Threading.Tasks.Task<int> ExecuteAsync(
        MyStep step, object context,
        System.Threading.CancellationToken cancellationToken = default)
    {
        try { }
        catch (System.OperationCanceledException) { throw; }
        catch (System.Exception) { }
        return System.Threading.Tasks.Task.FromResult(0);
    }
}
" + KaleidoStubs);
    }

    [Fact]
    public async Task NonHandlerClass_BareExceptionCatch_NoDiagnostic()
    {
        await RunAsync(@"
public class SomeService
{
    public void DoWork()
    {
        try { }
        catch (System.Exception) { }
    }
}
" + KaleidoStubs);
    }
}
