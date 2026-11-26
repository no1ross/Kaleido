using Kaleido.Testing;
using Microsoft.CodeAnalysis;
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

namespace Kaleido.Process
{
    public interface IProcessStep { }

    public sealed class InformationResponseItem { }

    public sealed class InformationRequest { }

    public interface IInformationStep : IProcessStep
    {
        string InformationRequestId { get; init; }
        string ExchangeId { get; init; }
        System.Collections.Generic.IReadOnlyList<InformationResponseItem> Items { get; init; }
    }
}

namespace Kaleido.Process.Execution
{
    public sealed class ProcessStepHandlerResult
    {
        public static ProcessStepHandlerResult Success() => new();
        public static ProcessStepHandlerResult Success<TNext>() where TNext : Kaleido.Process.IProcessStep => new();
        public static ProcessStepHandlerResult RequireInformation<TNext>(Kaleido.Process.InformationRequest request)
            where TNext : Kaleido.Process.IInformationStep => new();
        public static ProcessStepHandlerResult<TProcessStepResult> Success<TProcessStepResult>(TProcessStepResult response) => new();
    }

    public sealed class ProcessStepHandlerResult<TProcessStepResult>
    {
        public ProcessStepHandlerResult<TProcessStepResult> Next<TNext>()
            where TNext : Kaleido.Process.IProcessStep => this;
        public ProcessStepHandlerResult<TProcessStepResult> RequireInformation<TNext>(Kaleido.Process.InformationRequest request)
            where TNext : Kaleido.Process.IInformationStep => this;
    }
}";

    private const string Steps = @"
public sealed record AnswerStep : Kaleido.Process.IInformationStep
{
    public string InformationRequestId { get; init; } = """";
    public string ExchangeId { get; init; } = """";
    public System.Collections.Generic.IReadOnlyList<Kaleido.Process.InformationResponseItem> Items { get; init; } = new Kaleido.Process.InformationResponseItem[0];
}

public sealed record CaptureStep : Kaleido.Process.IProcessStep;
";

    private static readonly DiagnosticResult Shape =
        new("KAL2012", DiagnosticSeverity.Error);

    private static readonly DiagnosticResult SuccessWithInformationStep =
        new("KAL2013", DiagnosticSeverity.Error);

    [Fact]
    public async Task InformationStepWithOnlyItsResponse_NoDiagnostic()
    {
        await RunAsync(Steps + KaleidoStubs);
    }

    [Fact]
    public async Task InformationStepWithAnotherProperty_Reports()
    {
        await RunAsync(@"
public sealed record MixedStep : Kaleido.Process.IInformationStep
{
    public string InformationRequestId { get; init; } = """";
    public string ExchangeId { get; init; } = """";
    public System.Collections.Generic.IReadOnlyList<Kaleido.Process.InformationResponseItem> Items { get; init; } = new Kaleido.Process.InformationResponseItem[0];
    public string {|#0:MemberId|} { get; init; } = """";
}
" + KaleidoStubs,
            Shape.WithLocation(0).WithArguments("MixedStep", "MemberId"));
    }

    [Fact]
    public async Task OrdinaryStepWithProperties_NoDiagnostic()
    {
        await RunAsync(@"
public sealed record CaptureMemberStep(string MemberId) : Kaleido.Process.IProcessStep;
" + KaleidoStubs);
    }

    [Fact]
    public async Task SuccessNamingAnInformationStep_Reports()
    {
        await RunAsync(Steps + @"
public static class Handler
{
    public static object Run() =>
        {|#0:Kaleido.Process.Execution.ProcessStepHandlerResult.Success<AnswerStep>()|};
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
        {|#0:Kaleido.Process.Execution.ProcessStepHandlerResult.Success(""done"").Next<AnswerStep>()|};
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
        Kaleido.Process.Execution.ProcessStepHandlerResult.Success<CaptureStep>();

    public static object Ask() =>
        Kaleido.Process.Execution.ProcessStepHandlerResult.RequireInformation<AnswerStep>(new Kaleido.Process.InformationRequest());

    public static object TypedNext() =>
        Kaleido.Process.Execution.ProcessStepHandlerResult.Success(""done"").Next<CaptureStep>();

    public static object TypedAsk() =>
        Kaleido.Process.Execution.ProcessStepHandlerResult.Success(""done"").RequireInformation<AnswerStep>(new Kaleido.Process.InformationRequest());
}
" + KaleidoStubs);
    }
}
