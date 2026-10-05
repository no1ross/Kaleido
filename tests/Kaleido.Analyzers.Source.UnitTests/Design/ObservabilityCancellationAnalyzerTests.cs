using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Design.ObservabilityCancellationAnalyzer>;

namespace Kaleido.Analyzers.Source.Design.UnitTests;

public sealed class ObservabilityCancellationAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.Design.ObservabilityCancellationAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.Design.ObservabilityCancellationAnalyzer CreateSut() =>
        new();

    private const string Stubs = @"
public interface IExecutionObservation
{
    void Failed(System.Exception exception);
    void Canceled();
}

public interface ILog
{
    void Warn(System.Exception exception);
}";

    private static readonly DiagnosticResult Expected =
        new("KAL0021", DiagnosticSeverity.Warning);

    [Fact]
    public async Task CatchException_CallsObservation_Reports()
    {
        await RunAsync(@"
public class C
{
    public void M(IExecutionObservation observation)
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
    public void M(IExecutionObservation observation)
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
    public void M(IExecutionObservation observation)
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
    public void M(IExecutionObservation observation)
    {
        try { }
        catch (System.OperationCanceledException) { observation.Canceled(); throw; }
        catch (System.Exception ex) { observation.Failed(ex); throw; }
    }
}" + Stubs);
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

    [Fact]
    public async Task CatchSpecificException_CallsObservation_NoDiagnostic()
    {
        await RunAsync(@"
public class C
{
    public void M(IExecutionObservation observation)
    {
        try { }
        catch (System.InvalidOperationException ex) { observation.Failed(ex); throw; }
    }
}" + Stubs);
    }
}
