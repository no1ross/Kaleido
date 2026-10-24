using Kaleido.Testing;
using Microsoft.CodeAnalysis;
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
    public interface IDelegatedQuerySource<TQueryContext, TResult> { }
    public interface IDelegatedQuerySource<TQueryContext, TResult, TParameters> { }
}

public sealed record CaptureStep : Kaleido.Processor.IProcessStep;
public sealed record NoParameters : Kaleido.Queryable.IQueryParameters;
public sealed record MemberContext : Kaleido.Queryable.IQueryContext { public string Id { get; init; } }
public sealed class MemberSource : Kaleido.Queryable.IQuerySource<MemberContext> { }";

    private static readonly DiagnosticResult OutputProperty =
        new("KAL2021", DiagnosticSeverity.Warning);

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

    // #454: a delegated source may return a TResult unrelated to its query context; the registry
    // publishes it, so it is described like a view's output (2- and 3-argument interfaces).
    [Fact]
    public async Task ADelegatedSourceResultPropertyWithoutDescription_Reports()
    {
        await RunAsync(@"
public sealed record ShippingRate
{
    public decimal {|#0:Amount|} { get; init; }
}

public sealed record Eta
{
    public int {|#1:Days|} { get; init; }
}

public sealed class RateSource : Kaleido.Queryable.IDelegatedQuerySource<MemberContext, ShippingRate, NoParameters> { }
public sealed class EtaSource : Kaleido.Queryable.IDelegatedQuerySource<MemberContext, Eta> { }
" + KaleidoStubs,
            OutputProperty.WithLocation(0).WithArguments("Amount", "ShippingRate"),
            OutputProperty.WithLocation(1).WithArguments("Days", "Eta"));
    }

    // Its query context is KAL2015's; a described result, one from another assembly, and one shared
    // with a view (reported once) follow the same rules as every other output.
    [Fact]
    public async Task DelegatedSourceResults_CoveredElsewhereDescribedOrShared_FollowTheSameRules()
    {
        await RunAsync(@"
public sealed record Described
{
    [System.ComponentModel.Description(""The rate in EUR."")]
    public decimal Amount { get; init; }
}

public sealed record Shared
{
    public string {|#0:Code|} { get; init; }
}

public sealed class ContextSource : Kaleido.Queryable.IDelegatedQuerySource<MemberContext, MemberContext> { }
public sealed class DescribedSource : Kaleido.Queryable.IDelegatedQuerySource<MemberContext, Described, NoParameters> { }
public sealed class TextSource : Kaleido.Queryable.IDelegatedQuerySource<MemberContext, string> { }
public sealed class SharedSource : Kaleido.Queryable.IDelegatedQuerySource<MemberContext, Shared> { }
public sealed class SharedView : Kaleido.Queryable.IQueryViewSource<MemberSource, MemberContext, Shared, NoParameters> { }
" + KaleidoStubs,
            OutputProperty.WithLocation(0).WithArguments("Code", "Shared"));
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

    // Covered elsewhere or not the consumer's to describe: query contexts (KAL2015), types from
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
