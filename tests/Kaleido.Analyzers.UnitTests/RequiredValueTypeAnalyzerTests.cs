using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.RequiredValueTypeAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class RequiredValueTypeAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.RequiredValueTypeAnalyzer>
{
    protected override global::Kaleido.Analyzers.RequiredValueTypeAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
    internal sealed class RequiredMemberAttribute : System.Attribute { }
    internal sealed class CompilerFeatureRequiredAttribute : System.Attribute { public CompilerFeatureRequiredAttribute(string featureName) { } }
}

namespace System.Diagnostics.CodeAnalysis
{
    [System.AttributeUsage(System.AttributeTargets.Constructor)]
    internal sealed class SetsRequiredMembersAttribute : System.Attribute { }
}

namespace System.ComponentModel.DataAnnotations
{
    [System.AttributeUsage(System.AttributeTargets.All)]
    public sealed class RequiredAttribute : System.Attribute { }
}

namespace Kaleido.Processor
{
    public interface IProcessStep { }
}

namespace Kaleido.Queryable
{
    public interface IQueryParameters { }
}

public enum ProcedureCodeSystem { Unknown, Cpt }";

    private static readonly DiagnosticResult StepInput =
        new("KAL2021", DiagnosticSeverity.Warning);

    private static readonly DiagnosticResult QueryParameter =
        new("KAL2022", DiagnosticSeverity.Warning);

    [Fact]
    public async Task RequiredValueTypeStepInputs_Report()
    {
        await RunAsync(@"
public sealed record CaptureMemberStep : Kaleido.Processor.IProcessStep
{
    [System.ComponentModel.DataAnnotations.Required]
    public System.Guid {|#0:MemberId|} { get; init; }

    [System.ComponentModel.DataAnnotations.Required]
    public ProcedureCodeSystem {|#1:CodeSystem|} { get; init; }
}
" + KaleidoStubs,
            StepInput.WithLocation(0).WithArguments("MemberId", "CaptureMemberStep", "Guid"),
            StepInput.WithLocation(1).WithArguments("CodeSystem", "CaptureMemberStep", "ProcedureCodeSystem"));
    }

    [Fact]
    public async Task RequiredValueTypeQueryParameter_Reports()
    {
        await RunAsync(@"
public sealed class ProviderSearchParameters : Kaleido.Queryable.IQueryParameters
{
    [System.ComponentModel.DataAnnotations.Required]
    public System.Guid {|#0:ProcessId|} { get; init; }
}
" + KaleidoStubs,
            QueryParameter.WithLocation(0).WithArguments("ProcessId", "ProviderSearchParameters", "Guid"));
    }

    [Fact]
    public async Task NullableReferenceAndOptionalInputs_NoDiagnostic()
    {
        await RunAsync(@"
public sealed record CaptureMemberStep : Kaleido.Processor.IProcessStep
{
    [System.ComponentModel.DataAnnotations.Required]
    public System.Guid? MemberId { get; init; }

    [System.ComponentModel.DataAnnotations.Required]
    public required System.Guid MemberEnrollmentId { get; init; }

    [System.ComponentModel.DataAnnotations.Required]
    public string CodeValue { get; init; } = """";

    public System.DateTime DateOfService { get; init; }
}

public sealed class ProviderSearchParameters : Kaleido.Queryable.IQueryParameters
{
    [System.ComponentModel.DataAnnotations.Required]
    public System.Guid? ProcessId { get; init; }
}
" + KaleidoStubs);
    }

    [Fact]
    public async Task OtherTypes_NoDiagnostic()
    {
        await RunAsync(@"
public sealed record NotAStep
{
    [System.ComponentModel.DataAnnotations.Required]
    public System.Guid Id { get; init; }
}
" + KaleidoStubs);
    }
}
