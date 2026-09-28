using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Queryable.QueryViewAttributeAnalyzer>;

namespace Kaleido.Analyzers.UnitTests.Queryable;

public sealed class QueryViewAttributeAnalyzerTests
{
    private const string QueryViewStub = @"
namespace Kaleido.Queryable
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class QueryViewAttribute : System.Attribute
    {
        public required string Name { get; init; }
        public required string Version { get; init; }
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2003", DiagnosticSeverity.Error);

    [Fact]
    public async Task Name_Empty_Reports()
    {
        await RunAsync(@"
using Kaleido.Queryable;
[{|#0:QueryView(Name = """", Version = ""1.0.0"")|}]
public class MyView { }
" + QueryViewStub,
            Expected.WithLocation(0).WithArguments("QueryViewAttribute"));
    }

    [Fact]
    public async Task Version_Empty_Reports()
    {
        await RunAsync(@"
using Kaleido.Queryable;
[{|#0:QueryView(Name = ""Foo"", Version = """")|}]
public class MyView { }
" + QueryViewStub,
            Expected.WithLocation(0).WithArguments("QueryViewAttribute"));
    }

    [Fact]
    public async Task Both_Empty_Reports()
    {
        await RunAsync(@"
using Kaleido.Queryable;
[{|#0:QueryView(Name = """", Version = """")|}]
public class MyView { }
" + QueryViewStub,
            Expected.WithLocation(0).WithArguments("QueryViewAttribute"));
    }

    [Fact]
    public async Task Valid_NoDiagnostic()
    {
        await RunAsync(@"
using Kaleido.Queryable;
[QueryView(Name = ""Foo"", Version = ""1.0.0"")]
public class MyView { }
" + QueryViewStub);
    }
}
