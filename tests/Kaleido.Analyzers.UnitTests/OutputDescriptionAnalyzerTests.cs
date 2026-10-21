using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.OutputDescriptionAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class OutputDescriptionAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.OutputDescriptionAnalyzer>
{
    protected override global::Kaleido.Analyzers.OutputDescriptionAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }

namespace System.ComponentModel.DataAnnotations
{
    [System.AttributeUsage(System.AttributeTargets.All)]
    public sealed class DisplayAttribute : System.Attribute
    {
        public string Description { get; set; }
    }
}

namespace Kaleido.Processor
{
    public interface IProcessStep { }
    public interface IProcessStepHandler<TStep> { }
    public interface IProcessStepHandler<TStep, TResult> { }
}

namespace Kaleido.Queryable
{
    public interface IQueryContext { }
    public interface IQueryParameters { }
    public interface IQuerySource<TQueryContext> { }
    public interface IQueryViewSource<TSource, TQueryContext, TView, TViewParameters> { }
    public interface IQueryViewSourceAsync<TSource, TQueryContext, TView, TViewParameters> { }
}

public sealed record CaptureStep : Kaleido.Processor.IProcessStep;
public sealed record NoParameters : Kaleido.Queryable.IQueryParameters;
public sealed record MemberContext : Kaleido.Queryable.IQueryContext { public string Id { get; init; } }
public sealed class MemberSource : Kaleido.Queryable.IQuerySource<MemberContext> { }";

    private static readonly DiagnosticResult OutputProperty =
        new("KAL2024", DiagnosticSeverity.Warning);

    [Fact]
    public async Task AStepResultPropertyWithoutDescription_Reports()
    {
        await RunAsync(@"
public sealed record CaptureResult
{
    public string {|#0:ResolvedCode|} { get; init; }
}

public sealed class CaptureHandler : Kaleido.Processor.IProcessStepHandler<CaptureStep, CaptureResult> { }
" + KaleidoStubs,
            OutputProperty.WithLocation(0).WithArguments("ResolvedCode", "CaptureResult"));
    }

    [Fact]
    public async Task AViewOutputPropertyWithoutDescription_Reports_ForSyncAndAsyncViews()
    {
        await RunAsync(@"
public sealed record MemberCard
{
    public string {|#0:DisplayName|} { get; init; }
}

public sealed record MemberRow
{
    public string {|#1:Plan|} { get; init; }
}

public sealed class MemberCardView : Kaleido.Queryable.IQueryViewSource<MemberSource, MemberContext, MemberCard, NoParameters> { }
public sealed class MemberRowView : Kaleido.Queryable.IQueryViewSourceAsync<MemberSource, MemberContext, MemberRow, NoParameters> { }
" + KaleidoStubs,
            OutputProperty.WithLocation(0).WithArguments("DisplayName", "MemberCard"),
            OutputProperty.WithLocation(1).WithArguments("Plan", "MemberRow"));
    }

    [Fact]
    public async Task DescribedOutputs_NoDiagnostic()
    {
        await RunAsync(@"
public sealed record CaptureResult
{
    [System.ComponentModel.Description(""The procedure code to request."")]
    public string ResolvedCode { get; init; }

    [System.ComponentModel.DataAnnotations.Display(Description = ""When it was resolved."")]
    public System.DateTimeOffset ResolvedOn { get; init; }

    public static string Unrelated { get; } = """";

    internal string Internal { get; init; }
}

public sealed class CaptureHandler : Kaleido.Processor.IProcessStepHandler<CaptureStep, CaptureResult> { }
" + KaleidoStubs);
    }

    // An output used by two handlers is reported once.
    [Fact]
    public async Task AnOutputUsedTwice_IsReportedOnce()
    {
        await RunAsync(@"
public sealed record SharedResult
{
    public string {|#0:Code|} { get; init; }
}

public sealed class FirstHandler : Kaleido.Processor.IProcessStepHandler<CaptureStep, SharedResult> { }
public sealed class SecondHandler : Kaleido.Processor.IProcessStepHandler<CaptureStep, SharedResult> { }
" + KaleidoStubs,
            OutputProperty.WithLocation(0).WithArguments("Code", "SharedResult"));
    }

    // Covered elsewhere or not the consumer's to describe: query contexts (KAL2018), types from
    // other assemblies (string), handlers without a result, abstract handlers.
    [Fact]
    public async Task OutputsNotDescribedHere_NoDiagnostic()
    {
        await RunAsync(@"
public sealed class ContextView : Kaleido.Queryable.IQueryViewSource<MemberSource, MemberContext, MemberContext, NoParameters> { }
public sealed class TextHandler : Kaleido.Processor.IProcessStepHandler<CaptureStep, string> { }
public sealed class PlainHandler : Kaleido.Processor.IProcessStepHandler<CaptureStep> { }

public sealed record HiddenResult { public string Code { get; init; } }
public abstract class BaseHandler : Kaleido.Processor.IProcessStepHandler<CaptureStep, HiddenResult> { }
" + KaleidoStubs);
    }
}
