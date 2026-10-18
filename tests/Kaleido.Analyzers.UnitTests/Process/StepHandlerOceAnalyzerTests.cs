using Kaleido.Testing;
using Microsoft.CodeAnalysis;
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

    private static string Handler(string catches, string members = "") => @"
using OCE = System.OperationCanceledException;
public class MyStep { }
public class MyCanceledException : System.OperationCanceledException { }
public class MyHandler : Kaleido.Processor.IProcessStepHandler<MyStep>
{
    private bool retry;
" + members + @"
    public System.Threading.Tasks.Task<int> ExecuteAsync(
        MyStep step, object context,
        System.Threading.CancellationToken cancellationToken = default)
    {
        System.Exception other = null;
        try { }
        " + catches + @"
        return System.Threading.Tasks.Task.FromResult(0);
    }

    private static bool Log(string value) => true;
}
" + KaleidoStubs;

    // Filters that let an OperationCanceledException into the catch-all as a general failure.
    [Theory]
    [InlineData("ex is not System.OperationCanceledException || retry")]
    [InlineData("Log(nameof(System.OperationCanceledException))")]
    [InlineData("ex.Message != \"OperationCanceledException\"")]
    [InlineData("ex is not System.Threading.Tasks.TaskCanceledException")]
    [InlineData("ex is not MyCanceledException")]
    [InlineData("other is not System.OperationCanceledException")]
    [InlineData("ex.GetType() != typeof(System.OperationCanceledException)")]
    [InlineData("!(ex is System.OperationCanceledException) || retry")]
    [InlineData("ex is System.Exception")]
    [InlineData("true")]
    public async Task Filter_ThatDoesNotExcludeCancellation_Reports(string filter)
    {
        await RunAsync(
            Handler("{|#0:catch (System.Exception ex) when (" + filter + ") { }|}"),
            Expected.WithLocation(0).WithArguments("MyHandler"));
    }

    // Filters that exclude cancellation, or narrow the catch to cancellations only.
    [Theory]
    [InlineData("ex is not System.OperationCanceledException")]
    [InlineData("ex is not OCE")]
    [InlineData("!(ex is System.OperationCanceledException)")]
    [InlineData("ex is not System.OperationCanceledException && retry")]
    [InlineData("retry && ex is not System.OperationCanceledException")]
    [InlineData("ex is not System.OperationCanceledException and not System.ArgumentException")]
    [InlineData("ex is not (System.OperationCanceledException or System.ArgumentException)")]
    [InlineData("ex is not System.SystemException")]
    [InlineData("ex is System.ArgumentException")]
    [InlineData("ex is System.ArgumentException { ParamName: \"x\" }")]
    [InlineData("ex is System.OperationCanceledException")]
    [InlineData("ex is System.Threading.Tasks.TaskCanceledException")]
    [InlineData("ex is MyCanceledException")]
    [InlineData("false")]
    public async Task Filter_ThatExcludesOrOnlyCatchesCancellation_NoDiagnostic(string filter)
    {
        await RunAsync(Handler("catch (System.Exception ex) when (" + filter + ") { }"));
    }

    [Fact]
    public async Task ExceptionCatch_WithAliasedPrecedingOceCatch_NoDiagnostic()
    {
        await RunAsync(Handler("catch (OCE) { throw; }\n        catch (System.Exception) { }"));
    }

    [Fact]
    public async Task ExceptionCatch_AfterFilteredOceCatch_Reports()
    {
        await RunAsync(
            Handler("catch (System.OperationCanceledException) when (retry) { throw; }\n        {|#0:catch (System.Exception) { }|}"),
            Expected.WithLocation(0).WithArguments("MyHandler"));
    }

    [Fact]
    public async Task ExceptionCatch_AfterDerivedCancellationCatch_Reports()
    {
        await RunAsync(
            Handler("catch (System.Threading.Tasks.TaskCanceledException) { throw; }\n        {|#0:catch (System.Exception) { }|}"),
            Expected.WithLocation(0).WithArguments("MyHandler"));
    }

    [Fact]
    public async Task GeneratedCode_NoDiagnostic()
    {
        await RunAsync("// <auto-generated/>\n" + Handler("catch (System.Exception) { }"));
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
