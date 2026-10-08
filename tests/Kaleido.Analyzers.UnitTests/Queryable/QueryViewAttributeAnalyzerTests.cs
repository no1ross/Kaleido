using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Queryable.QueryViewAttributeAnalyzer>;

namespace Kaleido.Analyzers.Queryable.UnitTests;

public sealed class QueryViewAttributeAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Queryable.QueryViewAttributeAnalyzer>
{
    protected override global::Kaleido.Analyzers.Queryable.QueryViewAttributeAnalyzer CreateSut() =>
        new();

    private const string QueryViewStub = @"
namespace Kaleido.Queryable
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class QueryViewAttribute : System.Attribute
    {
        public required string Version { get; init; }
        public required string DisplayName { get; init; }
        public required string Description { get; init; }
        public string DefaultSortField { get; init; }
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2003", DiagnosticSeverity.Error);

    [Theory]
    [InlineData(@"Version = """", DisplayName = ""Member search"", Description = ""Searchable members.""")]
    [InlineData(@"Version = ""1.0.0"", DisplayName = """", Description = ""Searchable members.""")]
    [InlineData(@"Version = ""1.0.0"", DisplayName = ""Member search"", Description = """"")]
    public async Task EmptyRequiredMetadata_Reports(string arguments)
    {
        await RunAsync(@"
using Kaleido.Queryable;
[{|#0:QueryView(" + arguments + @")|}]
public class MemberSearchView { }
" + QueryViewStub,
            Expected.WithLocation(0).WithArguments("QueryViewAttribute"));
    }

    [Fact]
    public async Task Valid_NoDiagnostic()
    {
        await RunAsync(@"
using Kaleido.Queryable;
[QueryView(Version = ""1.0.0"", DisplayName = ""Member search"", Description = ""Searchable members."", DefaultSortField = ""LastName"")]
public class MemberSearchView { }
" + QueryViewStub);
    }
}
