using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Design.ObservabilityCancellationAnalyzer>;

namespace Kaleido.Analyzers.Source.Design.UnitTests;

public sealed class ObservabilityCancellationAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.Design.ObservabilityCancellationAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.Design.ObservabilityCancellationAnalyzer CreateSut() =>
        new();

    private const string Stubs = @"
namespace Kaleido.Processor.Observability
{
    public interface IExecutionObservation
    {
        void Failed(System.Exception exception);
        void Canceled();
    }
}

public interface ILog
{
    void Warn(System.Exception exception);
}";

    private static readonly DiagnosticResult Expected =
        new("KAL0018", DiagnosticSeverity.Warning);

    [Fact]
    public async Task CatchException_CallsObservation_Reports()
    {
        await RunAsync(@"
public class C
{
    public void M(Kaleido.Processor.Observability.IExecutionObservation observation)
    {
        try { }
        {|#0:catch|} (System.Exception ex) { observation.Failed(ex); throw; }
    }
}" + Stubs,
            Expected.WithLocation(0).WithArguments("observation.Failed"));
    }

    [Fact]
    public async Task BareCatch_CallsObservation_Reports()
    {
        await RunAsync(@"
public class C
{
    public void M(Kaleido.Processor.Observability.IExecutionObservation observation)
    {
        try { }
        {|#0:catch|} { observation.Canceled(); throw; }
    }
}" + Stubs,
            Expected.WithLocation(0).WithArguments("observation.Canceled"));
    }

    [Fact]
    public async Task CatchException_FilteredOnCancellation_NoDiagnostic()
    {
        await RunAsync(@"
public class C
{
    public void M(Kaleido.Processor.Observability.IExecutionObservation observation)
    {
        try { }
        catch (System.Exception ex) when (ex is not System.OperationCanceledException) { observation.Failed(ex); throw; }
    }
}" + Stubs);
    }

    [Fact]
    public async Task CatchException_AfterCancellationClause_NoDiagnostic()
    {
        await RunAsync(@"
public class C
{
    public void M(Kaleido.Processor.Observability.IExecutionObservation observation)
    {
        try { }
        catch (System.OperationCanceledException) { observation.Canceled(); throw; }
        catch (System.Exception ex) { observation.Failed(ex); throw; }
    }
}" + Stubs);
    }

    private static string Method(string catches) => @"
using OCE = System.OperationCanceledException;
public class MyCanceledException : System.OperationCanceledException { }
public class C
{
    private bool retry;

    public void M(Kaleido.Processor.Observability.IExecutionObservation observation)
    {
        System.Exception other = null;
        try { }
        " + catches + @"
    }

    private static bool Log(string value) => true;
}" + Stubs;

    // Filters that let an OperationCanceledException be recorded as a failure.
    [Theory]
    [InlineData("ex is not System.OperationCanceledException || retry")]
    [InlineData("Log(nameof(System.OperationCanceledException))")]
    [InlineData("Log(typeof(System.OperationCanceledException).Name)")]
    [InlineData("ex.Message != \"OperationCanceledException\"")]
    [InlineData("ex is not System.Threading.Tasks.TaskCanceledException")]
    [InlineData("ex is not MyCanceledException")]
    [InlineData("other is not System.OperationCanceledException")]
    [InlineData("ex.GetType() != typeof(System.OperationCanceledException)")]
    [InlineData("ex is System.Exception")]
    public async Task Filter_ThatDoesNotExcludeCancellation_Reports(string filter)
    {
        await RunAsync(
            Method("{|#0:catch|} (System.Exception ex) when (" + filter + ") { observation.Failed(ex); throw; }"),
            Expected.WithLocation(0).WithArguments("observation.Failed"));
    }

    // Filters that exclude cancellation, or narrow the catch to cancellations only.
    [Theory]
    [InlineData("ex is not OCE")]
    [InlineData("!(ex is System.OperationCanceledException)")]
    [InlineData("ex is not System.OperationCanceledException && retry")]
    [InlineData("ex is not (System.OperationCanceledException or System.ArgumentException)")]
    [InlineData("ex is not System.SystemException")]
    [InlineData("ex is System.ArgumentException")]
    [InlineData("ex is System.OperationCanceledException")]
    [InlineData("ex is MyCanceledException")]
    public async Task Filter_ThatExcludesOrOnlyCatchesCancellation_NoDiagnostic(string filter)
    {
        await RunAsync(Method("catch (System.Exception ex) when (" + filter + ") { observation.Canceled(); throw; }"));
    }

    [Fact]
    public async Task CatchException_AfterFilteredCancellationClause_Reports()
    {
        await RunAsync(
            Method("catch (System.OperationCanceledException) when (retry) { observation.Canceled(); throw; }\n        {|#0:catch|} (System.Exception ex) { observation.Failed(ex); throw; }"),
            Expected.WithLocation(0).WithArguments("observation.Failed"));
    }

    // Cancellation is the caller's token firing (#517): a clause filtered on a token being cancelled
    // handles the cancellations; the catch-all then sees the other OperationCanceledExceptions (a
    // downstream timeout) as the failures they are.
    [Fact]
    public async Task CatchException_AfterAClauseFilteredOnTheTokenBeingCancelled_NoDiagnostic()
    {
        await RunAsync(@"
public class C
{
    public void M(Kaleido.Processor.Observability.IExecutionObservation observation, System.Threading.CancellationToken cancellationToken)
    {
        try { }
        catch (System.OperationCanceledException) when (cancellationToken.IsCancellationRequested) { observation.Canceled(); throw; }
        catch (System.Exception ex) { observation.Failed(ex); throw; }
    }
}" + Stubs);
    }

    // Only a CancellationToken's own flag counts: any other filter may let a cancellation through.
    [Theory]
    [InlineData("!cancellationToken.IsCancellationRequested")]
    [InlineData("source.IsCancellationRequested")]
    [InlineData("cancellationToken.CanBeCanceled")]
    public async Task CatchException_AfterAClauseFilteredOnSomethingElse_Reports(string filter)
    {
        await RunAsync(@"
public class C
{
    public void M(Kaleido.Processor.Observability.IExecutionObservation observation, System.Threading.CancellationToken cancellationToken, System.Threading.CancellationTokenSource source)
    {
        try { }
        catch (System.OperationCanceledException) when (" + filter + @") { observation.Canceled(); throw; }
        {|#0:catch|} (System.Exception ex) { observation.Failed(ex); throw; }
    }
}" + Stubs,
            Expected.WithLocation(0).WithArguments("observation.Failed"));
    }

    [Fact]
    public async Task CatchException_AfterAliasedCancellationClause_NoDiagnostic()
    {
        await RunAsync(Method("catch (OCE) { observation.Canceled(); throw; }\n        catch (System.Exception ex) { observation.Failed(ex); throw; }"));
    }

    [Fact]
    public async Task GeneratedCode_NoDiagnostic()
    {
        await RunAsync("// <auto-generated/>\n" + Method("catch (System.Exception ex) { observation.Failed(ex); throw; }"));
    }

    [Fact]
    public async Task CatchException_NoObservabilityCall_NoDiagnostic()
    {
        await RunAsync(@"
public class C
{
    public void M(ILog log)
    {
        try { }
        catch (System.Exception ex) { log.Warn(ex); }
    }
}" + Stubs);
    }

    // #465: a type is an observation by symbol - Kaleido's '*.Observability' namespace and the name
    // suffix - not because its name contains the word somewhere.
    [Theory]
    [InlineData("namespace Weather { public interface IWeatherObservation { void Failed(System.Exception exception); } }", "Weather.IWeatherObservation")]
    [InlineData("namespace Kaleido.Processor.Observability { public interface IObservationLog { void Failed(System.Exception exception); } }", "Kaleido.Processor.Observability.IObservationLog")]
    [InlineData("namespace Kaleido.Processor { public interface IStepObservation { void Failed(System.Exception exception); } }", "Kaleido.Processor.IStepObservation")]
    public async Task CatchException_CallsATypeThatIsNotAKaleidoObservation_NoDiagnostic(string declaration, string type)
    {
        await RunAsync(declaration + @"
public class C
{
    public void M(" + type + @" observation)
    {
        try { }
        catch (System.Exception ex) { observation.Failed(ex); throw; }
    }
}");
    }

    [Fact]
    public async Task CatchException_CallsAKaleidoObservability_Reports()
    {
        await RunAsync(@"
namespace Kaleido.Http.Mcp.Observability { public interface IMcpObservability { void RegistryRead(string result); } }
public class C
{
    public void M(Kaleido.Http.Mcp.Observability.IMcpObservability observability)
    {
        try { }
        {|#0:catch|} (System.Exception) { observability.RegistryRead(""failed""); throw; }
    }
}",
            Expected.WithLocation(0).WithArguments("observability.RegistryRead"));
    }

    [Fact]
    public async Task CatchSpecificException_CallsObservation_NoDiagnostic()
    {
        await RunAsync(@"
public class C
{
    public void M(Kaleido.Processor.Observability.IExecutionObservation observation)
    {
        try { }
        catch (System.InvalidOperationException ex) { observation.Failed(ex); throw; }
    }
}" + Stubs);
    }
}
