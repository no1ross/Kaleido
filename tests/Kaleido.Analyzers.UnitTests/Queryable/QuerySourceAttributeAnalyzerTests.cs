using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Queryable.QuerySourceAttributeAnalyzer>;

namespace Kaleido.Analyzers.Queryable.UnitTests;

public sealed class QuerySourceAttributeAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Queryable.QuerySourceAttributeAnalyzer>
{
    protected override global::Kaleido.Analyzers.Queryable.QuerySourceAttributeAnalyzer CreateSut() =>
        new();

    private const string QuerySourceStub = @"
namespace Kaleido.Queryable
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class QuerySourceAttribute : System.Attribute
    {
        public required string Version { get; init; }
        public required string DisplayName { get; init; }
        public required string Description { get; init; }
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2002", DiagnosticSeverity.Error);

    [Theory]
    [InlineData(@"Version = """", DisplayName = ""Members"", Description = ""Member enrollments.""")]
    [InlineData(@"Version = ""1.0.0"", DisplayName = """", Description = ""Member enrollments.""")]
    [InlineData(@"Version = ""1.0.0"", DisplayName = ""Members"", Description = """"")]
    public async Task EmptyRequiredMetadata_Reports(string arguments)
    {
        await RunAsync(@"
using Kaleido.Queryable;
[{|#0:QuerySource(" + arguments + @")|}]
public class MemberQuerySource { }
" + QuerySourceStub,
            Expected.WithLocation(0).WithArguments("QuerySourceAttribute"));
    }

    [Fact]
    public async Task Valid_NoDiagnostic()
    {
        await RunAsync(@"
using Kaleido.Queryable;
[QuerySource(Version = ""1.0.0"", DisplayName = ""Members"", Description = ""Member enrollments."")]
public class MemberQuerySource { }
" + QuerySourceStub);
    }
}
