using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Process.ProcessStepSuffixAnalyzer>;

namespace Kaleido.Analyzers.UnitTests.Process;

public sealed class ProcessStepSuffixAnalyzerTests
{
    private const string ProcessStepStub = @"
namespace Kaleido.Process
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ProcessStepAttribute : System.Attribute
    {
        public required string Name { get; init; }
        public required string Version { get; init; }
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2007", DiagnosticSeverity.Warning);

    [Fact]
    public async Task NoStepSuffix_Reports()
    {
        await RunAsync(@"
[Kaleido.Process.ProcessStep(Name = ""capture-requested"", Version = ""1.0"")]
public class {|#0:CaptureRequested|} { }
" + ProcessStepStub,
            Expected.WithLocation(0).WithArguments("CaptureRequested"));
    }

    [Fact]
    public async Task WithStepSuffix_NoDiagnostic()
    {
        await RunAsync(@"
[Kaleido.Process.ProcessStep(Name = ""capture-requested"", Version = ""1.0"")]
public class CaptureRequestedStep { }
" + ProcessStepStub);
    }

    [Fact]
    public async Task NoAttribute_NoDiagnostic()
    {
        await RunAsync(@"
public class CaptureRequested { }");
    }

    [Fact]
    public async Task StepSuffixCaseSensitive_Reports()
    {
        // "step" (lowercase) is not the same as "Step"
        await RunAsync(@"
[Kaleido.Process.ProcessStep(Name = ""capture-requested"", Version = ""1.0"")]
public class {|#0:CaptureRequestedstep|} { }
" + ProcessStepStub,
            Expected.WithLocation(0).WithArguments("CaptureRequestedstep"));
    }
}
