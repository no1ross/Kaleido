using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.ToolNameLengthAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class ToolNameLengthAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.ToolNameLengthAnalyzer>
{
    protected override global::Kaleido.Analyzers.ToolNameLengthAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }

namespace Kaleido.Processor
{
    public interface IProcessStep { }

    public interface IInformationStep : IProcessStep { }
}

namespace Kaleido.Queryable
{
    public interface IQueryContext { }

    public interface IQuerySource<TQueryContext> { }

    public interface IQueryViewSource<TSource, TQueryContext, TView, TViewParameters> { }
}";

    // 48 characters once snake-cased: over the 40-character budget.
    private const string LongStep = "CaptureTheRequestingProviderAndTheirOfficeStep";

    private static readonly DiagnosticResult Expected =
        new("KAL2019", DiagnosticSeverity.Warning);

    [Fact]
    public async Task AStepWithinTheBudget_NoDiagnostic()
    {
        await RunAsync(@"
public sealed record CaptureRequestingProviderStep : Kaleido.Processor.IProcessStep;
" + KaleidoStubs);
    }

    [Fact]
    public async Task AStepOverTheBudget_Reports()
    {
        await RunAsync(@"
public sealed record {|#0:" + LongStep + @"|} : Kaleido.Processor.IProcessStep;
" + KaleidoStubs,
            Expected.WithLocation(0).WithArguments(
                LongStep,
                "capture_the_requesting_provider_and_their_office",
                48,
                40));
    }

    [Fact]
    public async Task AnInformationStep_IsNotATool_NoDiagnostic()
    {
        await RunAsync(@"
public sealed record " + LongStep + @" : Kaleido.Processor.IInformationStep;
" + KaleidoStubs);
    }

    [Fact]
    public async Task ASourceOverTheBudget_Reports_AndItsSuffixIsNotCounted()
    {
        await RunAsync(@"
public sealed class Ctx : Kaleido.Queryable.IQueryContext { }

public sealed class {|#0:RequestingProviderSearchAcrossEveryNetworkAndRegionSource|} : Kaleido.Queryable.IQuerySource<Ctx> { }
" + KaleidoStubs,
            Expected.WithLocation(0).WithArguments(
                "RequestingProviderSearchAcrossEveryNetworkAndRegionSource",
                "requesting_provider_search_across_every_network_and_region",
                58,
                40));
    }

    [Fact]
    public async Task AViewWithinTheBudget_NoDiagnostic()
    {
        await RunAsync(@"
public sealed class Ctx : Kaleido.Queryable.IQueryContext { }
public sealed class Src : Kaleido.Queryable.IQuerySource<Ctx> { }
public sealed record View;
public sealed record Parameters;

public sealed class MemberSearchViewSource : Kaleido.Queryable.IQueryViewSource<Src, Ctx, View, Parameters> { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task AnOrdinaryClass_NoDiagnostic()
    {
        await RunAsync(@"
public sealed class ThisIsAVeryLongHelperClassNameThatIsNotACapabilityAtAll { }
" + KaleidoStubs);
    }

    [Theory]
    [InlineData("CaptureMriInfoStep", "capture_mri_info")]
    [InlineData("ConfirmCtInsteadOfMriStep", "confirm_ct_instead_of_mri")]
    [InlineData("MRIInfoStep", "mri_info")]
    [InlineData("ProductByCategoryQueryViewSource", "product_by_category")]
    [InlineData("PriorAuthQueryContextSource", "prior_auth")]
    [InlineData("Step", "step")]
    public void ToolNamePart_MatchesTheAdaptersNaming(string typeName, string expected)
    {
        Assert.Equal(expected, global::Kaleido.Analyzers.ToolNameLengthAnalyzer.ToolNamePart(typeName));
    }
}
