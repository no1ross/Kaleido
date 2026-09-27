using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Queryable.QueryContextAttributeAnalyzer>;

namespace Kaleido.Analyzers.UnitTests.Queryable;

public sealed class QueryContextAttributeAnalyzerTests
{
    private const string QueryContextStub = @"
namespace Kaleido.Queryable
{
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class QueryContextAttribute : System.Attribute
    {
        public required string Name { get; init; }
        public required string Version { get; init; }
    }
}";

    private static readonly DiagnosticResult Expected =
        new("KAL2002", DiagnosticSeverity.Error);

    [Fact]
    public async Task Name_Empty_Reports()
    {
        await RunAsync(@"
using Kaleido.Queryable;
[{|#0:QueryContext(Name = """", Version = ""1.0.0"")|}]
public class MyContext { }
" + QueryContextStub,
            Expected.WithLocation(0).WithArguments("QueryContextAttribute"));
    }

    [Fact]
    public async Task Version_Empty_Reports()
    {
        await RunAsync(@"
using Kaleido.Queryable;
[{|#0:QueryContext(Name = ""Foo"", Version = """")|}]
public class MyContext { }
" + QueryContextStub,
            Expected.WithLocation(0).WithArguments("QueryContextAttribute"));
    }

    [Fact]
    public async Task Both_Empty_Reports()
    {
        await RunAsync(@"
using Kaleido.Queryable;
[{|#0:QueryContext(Name = """", Version = """")|}]
public class MyContext { }
" + QueryContextStub,
            Expected.WithLocation(0).WithArguments("QueryContextAttribute"));
    }

    [Fact]
    public async Task Valid_NoDiagnostic()
    {
        await RunAsync(@"
using Kaleido.Queryable;
[QueryContext(Name = ""Foo"", Version = ""1.0.0"")]
public class MyContext { }
" + QueryContextStub);
    }
}
