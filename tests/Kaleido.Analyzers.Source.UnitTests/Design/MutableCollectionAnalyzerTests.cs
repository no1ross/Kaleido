using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Design.MutableCollectionAnalyzer>;

namespace Kaleido.Analyzers.Source.UnitTests;

public sealed class MutableCollectionAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0018", DiagnosticSeverity.Warning);

    // -- No-diagnostic cases --------------------------------------------------

    [Fact]
    public async Task Property_IReadOnlyList_NoDiagnostic()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public IReadOnlyList<string> Items { get; }
}");
    }

    [Fact]
    public async Task Property_IReadOnlyCollection_NoDiagnostic()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public IReadOnlyCollection<string> Items { get; }
}");
    }

    [Fact]
    public async Task Property_IReadOnlyDictionary_NoDiagnostic()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public IReadOnlyDictionary<string, int> Map { get; }
}");
    }

    [Fact]
    public async Task Property_IEnumerable_NoDiagnostic()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public IEnumerable<string> Items { get; }
}");
    }

    [Fact]
    public async Task Property_Array_NoDiagnostic()
    {
        await RunAsync(@"
public class Api
{
    public string[] Items { get; }
}");
    }

    [Fact]
    public async Task PrivateProperty_List_NoDiagnostic()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    private List<string> Items { get; } = [];
}");
    }

    [Fact]
    public async Task PublicProperty_OnPrivateClass_NoDiagnostic()
    {
        await RunAsync(@"
using System.Collections.Generic;
internal class Impl
{
    private sealed class Inner
    {
        public List<string> Items { get; } = [];
    }
}");
    }

    [Fact]
    public async Task Override_MutableReturn_NoDiagnostic()
    {
        // The override itself is exempt — the type is fixed at the base.
        // The base abstract method fires (correctly), so we suppress that with #pragma
        // in the test source and only assert no *additional* diagnostic on the override.
        await RunAsync(@"
using System.Collections.Generic;
public abstract class Base
{
#pragma warning disable KAL0018
    public abstract List<string> GetItems();
#pragma warning restore KAL0018
}
public class Derived : Base
{
    public override List<string> GetItems() => new();
}");
    }

    [Fact]
    public async Task ExplicitInterfaceImpl_MutableReturn_NoDiagnostic()
    {
        // Explicit interface implementations are exempt.
        // The interface declaration itself fires (correctly); suppressed in test source.
        await RunAsync(@"
using System.Collections.Generic;
public interface IApi
{
#pragma warning disable KAL0018
    List<string> GetItems();
#pragma warning restore KAL0018
}
public class Api : IApi
{
    List<string> IApi.GetItems() => new();
}");
    }

    // -- Diagnostic cases -----------------------------------------------------

    [Fact]
    public async Task Property_List_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public {|#0:List<string>|} Items { get; }
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task Property_IList_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public {|#0:IList<string>|} Items { get; }
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.IList<string>"));
    }

    [Fact]
    public async Task Property_Dictionary_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public {|#0:Dictionary<string, int>|} Map { get; }
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.Dictionary<string, int>"));
    }

    [Fact]
    public async Task Property_IDictionary_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public {|#0:IDictionary<string, int>|} Map { get; }
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.IDictionary<string, int>"));
    }

    [Fact]
    public async Task Property_HashSet_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public {|#0:HashSet<string>|} Items { get; }
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.HashSet<string>"));
    }

    [Fact]
    public async Task Property_ISet_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public {|#0:ISet<string>|} Items { get; }
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.ISet<string>"));
    }

    [Fact]
    public async Task Property_ICollection_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public {|#0:ICollection<string>|} Items { get; }
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.ICollection<string>"));
    }

    [Fact]
    public async Task MethodReturn_List_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public {|#0:List<string>|} GetItems() => new();
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task MethodParameter_List_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
public class Api
{
    public void SetItems({|#0:List<string>|} items) { }
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task InternalClass_PublicProperty_List_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
internal class Api
{
    public {|#0:List<string>|} Items { get; }
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.List<string>"));
    }

    [Fact]
    public async Task Interface_Property_List_Reports()
    {
        // Interfaces themselves are also public API surfaces.
        await RunAsync(@"
using System.Collections.Generic;
public interface IApi
{
    {|#0:List<string>|} Items { get; }
}",
            Expected.WithLocation(0)
                .WithArguments("System.Collections.Generic.List<string>"));
    }
}
