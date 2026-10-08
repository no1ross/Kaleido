using Microsoft.CodeAnalysis;
using Kaleido.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Queryable.QueryableIdentityAnalyzer>;

namespace Kaleido.Analyzers.Queryable.UnitTests;

public sealed class QueryableIdentityAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Queryable.QueryableIdentityAnalyzer>
{
    protected override global::Kaleido.Analyzers.Queryable.QueryableIdentityAnalyzer CreateSut() =>
        new();

    private const string KaleidoStubs = @"
namespace Kaleido.Queryable
{
    public interface IQueryContext { }
    public interface IQueryParameters { }
    public interface ILocalQuerySource<TQueryContext> { }
    public interface IQuerySource<TQueryContext> : ILocalQuerySource<TQueryContext> { }
    public interface IQuerySourceAsync<TQueryContext> : ILocalQuerySource<TQueryContext> { }
    public interface IDelegatedQuerySource<TQueryContext, TResult, TParameters> { }
    public interface IDelegatedQuerySource<TQueryContext, TResult> : IDelegatedQuerySource<TQueryContext, TResult, object> { }
    public interface IQueryViewSource<TSource, TQueryContext, TView, TViewParameters> { }
    public interface IQueryViewSource<TSource, TQueryContext, TView> : IQueryViewSource<TSource, TQueryContext, TView, object> { }
    public interface IQueryViewSourceAsync<TSource, TQueryContext, TView, TViewParameters> { }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class QuerySourceAttribute : System.Attribute
    {
        public string Version { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class QueryViewAttribute : System.Attribute
    {
        public string Version { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
    }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class SortableAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class FilterableAttribute : System.Attribute { }

    [System.AttributeUsage(System.AttributeTargets.Property)]
    public class SearchableAttribute : System.Attribute { }
}

public class MemberQueryContext : Kaleido.Queryable.IQueryContext
{
    [Kaleido.Queryable.Sortable]
    public string LastName { get; set; }
}

public class MemberView { }";

    private static readonly DiagnosticResult AttributeWithoutInterface =
        new("KAL2012", DiagnosticSeverity.Error);

    private static readonly DiagnosticResult InterfaceWithoutAttribute =
        new("KAL2013", DiagnosticSeverity.Error);

    private static readonly DiagnosticResult QueryRuleOutsideContext =
        new("KAL2014", DiagnosticSeverity.Warning);

    [Fact]
    public async Task SourceAttributeWithoutInterface_Reports()
    {
        await RunAsync(@"
[Kaleido.Queryable.QuerySource(Version = ""1"", DisplayName = ""Members"", Description = ""Members."")]
public class {|#0:MemberQuerySource|} { }
" + KaleidoStubs,
            AttributeWithoutInterface.WithLocation(0)
                .WithArguments("MemberQuerySource", "QuerySource", "IQuerySource<T>, IQuerySourceAsync<T> or IDelegatedQuerySource<…>"));
    }

    [Fact]
    public async Task ViewAttributeWithoutInterface_Reports()
    {
        await RunAsync(@"
[Kaleido.Queryable.QueryView(Version = ""1"", DisplayName = ""Member search"", Description = ""Search."")]
public class {|#0:MemberSearch|} { }
" + KaleidoStubs,
            AttributeWithoutInterface.WithLocation(0)
                .WithArguments("MemberSearch", "QueryView", "IQueryViewSource<…> or IQueryViewSourceAsync<…>"));
    }

    [Fact]
    public async Task LocalSourceWithoutAttribute_Reports()
    {
        await RunAsync(@"
public class {|#0:MemberQuerySource|} : Kaleido.Queryable.IQuerySource<MemberQueryContext> { }
" + KaleidoStubs,
            InterfaceWithoutAttribute.WithLocation(0)
                .WithArguments("Query source", "MemberQuerySource", "QuerySource"));
    }

    [Fact]
    public async Task DelegatedSourceWithoutAttribute_Reports()
    {
        await RunAsync(@"
public class {|#0:ProviderSearch|} : Kaleido.Queryable.IDelegatedQuerySource<MemberQueryContext, MemberView> { }
" + KaleidoStubs,
            InterfaceWithoutAttribute.WithLocation(0)
                .WithArguments("Query source", "ProviderSearch", "QuerySource"));
    }

    [Fact]
    public async Task ViewWithoutAttribute_Reports()
    {
        await RunAsync(@"
public class {|#1:MemberQuerySource|} : Kaleido.Queryable.IQuerySource<MemberQueryContext> { }

public class {|#0:MemberSearch|} : Kaleido.Queryable.IQueryViewSource<MemberQuerySource, MemberQueryContext, MemberView> { }
" + KaleidoStubs,
            InterfaceWithoutAttribute.WithLocation(0)
                .WithArguments("Query view", "MemberSearch", "QueryView"),
            InterfaceWithoutAttribute.WithLocation(1)
                .WithArguments("Query source", "MemberQuerySource", "QuerySource"));
    }

    [Fact]
    public async Task SourceAndViewWithAttributes_NoDiagnostic()
    {
        await RunAsync(@"
[Kaleido.Queryable.QuerySource(Version = ""1"", DisplayName = ""Members"", Description = ""Members."")]
public class MemberQuerySource : Kaleido.Queryable.IQuerySource<MemberQueryContext> { }

[Kaleido.Queryable.QueryView(Version = ""1"", DisplayName = ""Member search"", Description = ""Search."")]
public class MemberSearch : Kaleido.Queryable.IQueryViewSource<MemberQuerySource, MemberQueryContext, MemberView> { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task AbstractSourceWithoutAttribute_NoDiagnostic()
    {
        await RunAsync(@"
public abstract class MemberQuerySourceBase : Kaleido.Queryable.IQuerySource<MemberQueryContext> { }
" + KaleidoStubs);
    }

    [Fact]
    public async Task QueryRuleAttributeOutsideQueryContext_Reports()
    {
        await RunAsync(@"
public class MemberSearchResult
{
    [{|#0:Kaleido.Queryable.Sortable|}]
    public string LastName { get; set; }
}
" + KaleidoStubs,
            QueryRuleOutsideContext.WithLocation(0)
                .WithArguments("Sortable", "MemberSearchResult", "LastName"));
    }

    [Fact]
    public async Task QueryRuleAttributeOnQueryContext_NoDiagnostic()
    {
        await RunAsync(KaleidoStubs);
    }
}
