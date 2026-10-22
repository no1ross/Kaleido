using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Layout.TypeFileNameAnalyzer>;

namespace Kaleido.Analyzers.Source.Layout.UnitTests;

public sealed class TypeFileNameAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.Layout.TypeFileNameAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.Layout.TypeFileNameAnalyzer CreateSut() =>
        new();

    private static readonly DiagnosticResult Expected =
        new("KAL0019", DiagnosticSeverity.Warning);

    private static async Task RunFilesAsync(DiagnosticResult[] expected, params (string Name, string Code)[] files)
    {
        var test = Create("");
        test.TestState.Sources.Clear();

        foreach (var file in files)
        {
            test.TestState.Sources.Add(file);
        }

        test.ExpectedDiagnostics.AddRange(expected);
        await test.RunAsync();
    }

    [Fact]
    public async Task ATypeWithBehavior_InAFileNamedAfterIt_NoDiagnostic()
    {
        await RunFilesAsync([], ("Widget.cs", "public class Widget { public int Next() => 1; }"));
    }

    [Fact]
    public async Task ATypeWithBehavior_InAnotherFile_Reports()
    {
        await RunFilesAsync(
            [Expected.WithLocation(0).WithArguments("Widget", "Widget.cs", "Widget", "Gadget.cs")],
            ("Gadget.cs", "public class {|#0:Widget|} { public int Next() => 1; }"));
    }

    // Two types with behavior in one file: the one not named after the file is reported.
    [Fact]
    public async Task TwoTypesWithBehavior_InOneFile_ReportsTheSecond()
    {
        await RunFilesAsync(
            [Expected.WithLocation(0).WithArguments("Helper", "Helper.cs", "Helper", "Widget.cs")],
            ("Widget.cs", @"
public class Widget { public int Next() => 1; }
public sealed record {|#0:Helper|}(int Value) { public int Twice() => Value * 2; }"));
    }

    [Fact]
    public async Task DataRecordsAndInterfaces_MayShareAFile()
    {
        await RunFilesAsync([], ("Widget.cs", @"
public interface IWidget { int Next(); }
public class Widget : IWidget { public int Next() => 1; }
public sealed record WidgetOptions(int Size) { public string Name { get; init; } = """"; }
public sealed class WidgetState { public int Count { get; set; } }"));
    }

    [Theory]
    [InlineData("public sealed record Stamp { public bool IsEmpty => true; }")]
    [InlineData("public sealed record Stamp { public static implicit operator Stamp(int value) => new(); }")]
    [InlineData("public sealed record Stamp(object Value) : System.IComparable { int System.IComparable.CompareTo(object? other) => 0; }")]
    public async Task ComputedPropertiesOperatorsAndExplicitImplementations_AreBehavior(string declaration)
    {
        var test = Create("");
        test.TestState.Sources.Clear();
        test.TestState.Sources.Add(("Widget.cs", declaration.Replace("record Stamp", "record {|#0:Stamp|}", System.StringComparison.Ordinal)));
        test.ExpectedDiagnostics.Add(Expected.WithLocation(0).WithArguments("Stamp", "Stamp.cs", "Stamp", "Widget.cs"));
        await test.RunAsync();
    }

    [Fact]
    public async Task APartialType_MayBeSplitIntoNamedParts()
    {
        await RunFilesAsync([],
            ("Widget.cs", "public partial class Widget { public int Next() => 1; }"),
            ("Widget.Logging.cs", "public partial class Widget { private void Log() { } }"));
    }

    // Generic twins are named by arity: the lowest arity plain, a one-parameter twin XOfT.
    [Fact]
    public async Task GenericTwins_LiveInFilesNamedByArity()
    {
        await RunFilesAsync([],
            ("Result.cs", "public sealed record Result { public static Result Ok() => new(); }"),
            ("ResultOfT.cs", "public sealed record Result<T> { public Result<T> Next() => this; }"));
    }

    [Fact]
    public async Task AGenericTwin_InThePlainNamedFile_Reports()
    {
        await RunFilesAsync(
            [Expected.WithLocation(0).WithArguments("Result<T>", "ResultOfT.cs", "ResultOfT", "Result.cs")],
            ("Result.cs", @"
public sealed record Result { public static Result Ok() => new(); }
public sealed record {|#0:Result|}<T> { public Result<T> Next() => this; }"));
    }

    [Fact]
    public async Task AGenericTypeWithoutATwin_KeepsItsPlainName()
    {
        await RunFilesAsync([], ("Page.cs", "public sealed record Page<T>(T Item) { public bool IsEmpty => Item is null; }"));
    }

    [Theory]
    [InlineData("public class WidgetException : System.Exception { public string Describe() => Message; }")]
    [InlineData("public sealed class WidgetAttribute : System.Attribute { public string Describe() => \"\"; }")]
    [InlineData("public abstract class WidgetBase { public int Next() => 1; }")]
    [InlineData("public static class WidgetExtensions { public static int Next(this int value) => value; }")]
    [InlineData("public class Holder { private sealed class Nested { public int Next() => 1; } }")]
    public async Task ExemptTypes_MayLiveAnywhere(string declaration)
    {
        await RunFilesAsync([], ("Anything.cs", declaration));
    }
}
