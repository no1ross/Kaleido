using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.InputDescriptionAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class InputDescriptionAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.InputDescriptionAnalyzer>
{
    protected override global::Kaleido.Analyzers.InputDescriptionAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
namespace System.Runtime.CompilerServices { internal static class IsExternalInit { } }

namespace System.ComponentModel.DataAnnotations
{
    [System.AttributeUsage(System.AttributeTargets.All)]
    public sealed class DisplayAttribute : System.Attribute
    {
        public string Name { get; set; }
        public string Prompt { get; set; }
        public string Description { get; set; }
    }
}

namespace Kaleido.Processor
{
    public interface IProcessStep { }

    public interface IInformationStep : IProcessStep
    {
        string InformationRequestId { get; init; }
    }
}

namespace Kaleido.Queryable
{
    public interface IQueryContext { }

    public interface IQueryParameters { }
}";

    private static readonly DiagnosticResult StepProperty =
        new("KAL2017", DiagnosticSeverity.Warning);

    private static readonly DiagnosticResult QueryProperty =
        new("KAL2018", DiagnosticSeverity.Warning);

    [Fact]
    public async Task StepPropertyWithoutDescription_Reports()
    {
        await RunAsync(@"
public sealed record CaptureMemberStep : Kaleido.Processor.IProcessStep
{
    public string {|#0:MemberId|} { get; init; } = """";
}
" + KaleidoStubs,
            StepProperty.WithLocation(0).WithArguments("MemberId", "CaptureMemberStep"));
    }

    [Fact]
    public async Task StepPropertyWithDescriptionOrDisplayDescription_NoDiagnostic()
    {
        await RunAsync(@"
public sealed record CaptureMemberStep : Kaleido.Processor.IProcessStep
{
    [System.ComponentModel.Description(""The member's identifier on their card."")]
    public string MemberId { get; init; } = """";

    [System.ComponentModel.DataAnnotations.Display(Name = ""Date of birth"", Description = ""The member's date of birth."")]
    public string DateOfBirth { get; init; } = """";
}
" + KaleidoStubs);
    }

    [Fact]
    public async Task DisplayWithoutDescription_Reports()
    {
        await RunAsync(@"
public sealed record CaptureMemberStep : Kaleido.Processor.IProcessStep
{
    [System.ComponentModel.DataAnnotations.Display(Name = ""Member ID"", Prompt = ""What is the member ID?"")]
    public string {|#0:MemberId|} { get; init; } = """";
}
" + KaleidoStubs,
            StepProperty.WithLocation(0).WithArguments("MemberId", "CaptureMemberStep"));
    }

    [Fact]
    public async Task EmptyDescription_Reports()
    {
        await RunAsync(@"
public sealed record CaptureMemberStep : Kaleido.Processor.IProcessStep
{
    [System.ComponentModel.Description("" "")]
    public string {|#0:MemberId|} { get; init; } = """";
}
" + KaleidoStubs,
            StepProperty.WithLocation(0).WithArguments("MemberId", "CaptureMemberStep"));
    }

    [Fact]
    public async Task InformationStep_IsNotChecked()
    {
        await RunAsync(@"
public sealed record AnswerStep : Kaleido.Processor.IInformationStep
{
    public string InformationRequestId { get; init; } = """";
}
" + KaleidoStubs);
    }

    [Fact]
    public async Task QueryContextAndParametersPropertiesWithoutDescription_Report()
    {
        await RunAsync(@"
public sealed record MemberQueryContext : Kaleido.Queryable.IQueryContext
{
    public string {|#0:LastName|} { get; init; } = """";
}

public sealed record MemberSearchParameters : Kaleido.Queryable.IQueryParameters
{
    [System.ComponentModel.Description(""Plan to search within."")]
    public string PlanId { get; init; } = """";

    public string {|#1:Region|} { get; init; } = """";
}
" + KaleidoStubs,
            QueryProperty.WithLocation(0).WithArguments("LastName", "MemberQueryContext"),
            QueryProperty.WithLocation(1).WithArguments("Region", "MemberSearchParameters"));
    }

    [Fact]
    public async Task PlainRecord_IsNotChecked()
    {
        await RunAsync(@"
public sealed record MemberView
{
    public string LastName { get; init; } = """";
}
" + KaleidoStubs);
    }
}
