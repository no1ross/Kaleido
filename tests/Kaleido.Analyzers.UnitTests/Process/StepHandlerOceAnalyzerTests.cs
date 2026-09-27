using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Process.StepHandlerOceAnalyzer>;

namespace Kaleido.Analyzers.UnitTests.Process;

public sealed class StepHandlerOceAnalyzerTests
{
    private const string KaleidoStubs = @"
namespace Kaleido.Process
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
public class MyHandler : Kaleido.Process.IProcessStepHandler<MyStep>
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
public class MyHandler : Kaleido.Process.IProcessStepHandler<MyStep>
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
public class MyHandler : Kaleido.Process.IProcessStepHandler<MyStep>
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
