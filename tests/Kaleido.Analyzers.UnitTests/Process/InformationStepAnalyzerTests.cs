using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Process.InformationStepAnalyzer>;

namespace Kaleido.Analyzers.Process.UnitTests;

public sealed class InformationStepAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Process.InformationStepAnalyzer>
{
    protected override global::Kaleido.Analyzers.Process.InformationStepAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }

namespace Kaleido.Processor
{
    public interface IProcessStep { }

    public sealed class InformationResponseItem { }

    public sealed class InformationRequest { }

    public interface IInformationStep : IProcessStep
    {
        string InformationRequestId { get; init; }
        System.Collections.Generic.IReadOnlyList<InformationResponseItem> Items { get; init; }
    }
}

namespace Kaleido.Processor.Execution
{
    public sealed class ProcessStepHandlerResult
    {
        public static ProcessStepHandlerResult Success() => new();
        public static ProcessStepHandlerResult Success<TNext>() where TNext : Kaleido.Processor.IProcessStep => new();
        public static ProcessStepHandlerResult RequireInformation<TNext>(Kaleido.Processor.InformationRequest request)
            where TNext : Kaleido.Processor.IInformationStep => new();
    }

    public sealed class ProcessStepHandlerResult<TProcessStepResult>
    {
        public static ProcessStepHandlerResult<TProcessStepResult> Success<TNext>(TProcessStepResult response)
            where TNext : Kaleido.Processor.IProcessStep => new();
    }
}";

    private const string Steps = @"
public sealed record AnswerStep : Kaleido.Processor.IInformationStep
{
    public string InformationRequestId { get; init; } = """";
    public System.Collections.Generic.IReadOnlyList<Kaleido.Processor.InformationResponseItem> Items { get; init; } = new Kaleido.Processor.InformationResponseItem[0];
}

public sealed record CaptureStep : Kaleido.Processor.IProcessStep;
";

    private static readonly DiagnosticResult Shape =
        new("KAL2015", DiagnosticSeverity.Error);

    private static readonly DiagnosticResult SuccessWithInformationStep =
        new("KAL2016", DiagnosticSeverity.Error);

    [Fact]
    public async Task InformationStepWithOnlyItsResponse_NoDiagnostic()
    {
        await RunAsync(Steps + KaleidoStubs);
    }

    [Fact]
    public async Task InformationStepWithAnotherProperty_Reports()
    {
        await RunAsync(@"
public sealed record MixedStep : Kaleido.Processor.IInformationStep
{
    public string InformationRequestId { get; init; } = """";
    public System.Collections.Generic.IReadOnlyList<Kaleido.Processor.InformationResponseItem> Items { get; init; } = new Kaleido.Processor.InformationResponseItem[0];
    public string {|#0:MemberId|} { get; init; } = """";
}
" + KaleidoStubs,
            Shape.WithLocation(0).WithArguments("MixedStep", "MemberId"));
    }

    [Fact]
    public async Task OrdinaryStepWithProperties_NoDiagnostic()
    {
        await RunAsync(@"
public sealed record CaptureMemberStep(string MemberId) : Kaleido.Processor.IProcessStep;
" + KaleidoStubs);
    }

    [Fact]
    public async Task SuccessNamingAnInformationStep_Reports()
    {
        await RunAsync(Steps + @"
public static class Handler
{
    public static object Run() =>
        {|#0:Kaleido.Processor.Execution.ProcessStepHandlerResult.Success<AnswerStep>()|};
}
" + KaleidoStubs,
            SuccessWithInformationStep.WithLocation(0).WithArguments("AnswerStep"));
    }

    [Fact]
    public async Task TypedSuccessNamingAnInformationStep_Reports()
    {
        await RunAsync(Steps + @"
public static class Handler
{
    public static object Run() =>
        {|#0:Kaleido.Processor.Execution.ProcessStepHandlerResult<string>.Success<AnswerStep>(""done"")|};
}
" + KaleidoStubs,
            SuccessWithInformationStep.WithLocation(0).WithArguments("AnswerStep"));
    }

    [Fact]
    public async Task SuccessNamingAnOrdinaryStep_AndRequireInformation_NoDiagnostic()
    {
        await RunAsync(Steps + @"
public static class Handler
{
    public static object Next() =>
        Kaleido.Processor.Execution.ProcessStepHandlerResult.Success<CaptureStep>();

    public static object Ask() =>
        Kaleido.Processor.Execution.ProcessStepHandlerResult.RequireInformation<AnswerStep>(new Kaleido.Processor.InformationRequest());
}
" + KaleidoStubs);
    }
}
