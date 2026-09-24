using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Design.BclExceptionBanAnalyzer>;

namespace Kaleido.Analyzers.Source.UnitTests;

public sealed class BclExceptionBanAnalyzerTests
{
    private const string KaleidoStub = @"
namespace Kaleido.Exceptions
{
    public class KaleidoValidationException : System.Exception
    {
        public KaleidoValidationException(string code, string message) : base(message) { }
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL0002", DiagnosticSeverity.Warning);

    [Fact]
    public async Task Throw_InvalidOperationException_Reports()
    {
        await RunAsync(@"
public class C
{
    public void M()
    {
        throw {|#0:new System.InvalidOperationException(""bad"")|};
    }
}" + KaleidoStub,
            Expected.WithLocation(0).WithArguments("System.InvalidOperationException"));
    }

    [Fact]
    public async Task Throw_ArgumentNullException_NoDiagnostic()
    {
        await RunAsync(@"
public class C
{
    public void M()
    {
        throw new System.ArgumentNullException(""x"");
    }
}");
    }

    [Fact]
    public async Task Throw_FormatException_NoDiagnostic()
    {
        await RunAsync(@"
public class C
{
    public void M()
    {
        throw new System.FormatException(""bad"");
    }
}");
    }

    [Fact]
    public async Task Throw_KaleidoException_NoDiagnostic()
    {
        await RunAsync(@"
public class C
{
    public void M()
    {
        throw new Kaleido.Exceptions.KaleidoValidationException(""code"", ""msg"");
    }
}" + KaleidoStub);
    }
}
