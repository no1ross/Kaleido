using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Http.EndpointTagMissingAnalyzer>;

namespace Kaleido.Analyzers.Source.UnitTests;

public sealed class EndpointTagMissingAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0016", DiagnosticSeverity.Warning);

    // Minimal stubs — the analyzer is syntax-only so no real ASP.NET Core
    // reference assemblies are required.
    private const string BuilderStub = @"
public class RouteHandlerBuilder
{
    public RouteHandlerBuilder WithTags(params string[] tags) => this;
    public RouteHandlerBuilder WithName(string name) => this;
}
public class App
{
    public RouteHandlerBuilder MapGet(string pattern, System.Delegate handler) => new();
    public RouteHandlerBuilder MapPost(string pattern, System.Delegate handler) => new();
    public RouteHandlerBuilder MapPut(string pattern, System.Delegate handler) => new();
}";

    [Fact]
    public async Task MapGet_WithWithTags_NoDiagnostic()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapGet(""/items"", () => { }).WithTags(""Items"");
    }
}" + BuilderStub);
    }

    [Fact]
    public async Task MapPost_WithWithTags_NoDiagnostic()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapPost(""/items"", () => { }).WithTags(""Items"");
    }
}" + BuilderStub);
    }

    [Fact]
    public async Task MapGet_WithNameThenWithTags_NoDiagnostic()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapGet(""/items"", () => { }).WithName(""GetItems"").WithTags(""Items"");
    }
}" + BuilderStub);
    }

    [Fact]
    public async Task MapGet_WithoutWithTags_Reports()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        {|#0:app.MapGet(""/items"", () => { })|};
    }
}" + BuilderStub,
            Expected.WithLocation(0));
    }

    [Fact]
    public async Task MapPost_WithoutWithTags_Reports()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        {|#0:app.MapPost(""/items"", () => { })|};
    }
}" + BuilderStub,
            Expected.WithLocation(0));
    }

    [Fact]
    public async Task MapGet_WithTagsHasNoKaleidoArg_NoDiagnosticFromKal0016()
    {
        // KAL0016 only checks for presence of WithTags, not its content.
        // KAL0017 handles the missing "Kaleido" argument separately.
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapGet(""/items"", () => { }).WithTags(""SomeOtherTag"");
    }
}" + BuilderStub);
    }

    [Fact]
    public async Task MapPut_WithoutWithTags_NoDiagnostic()
    {
        // Only MapGet/MapPost are in scope for KAL0016.
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapPut(""/items"", () => { });
    }
}" + BuilderStub);
    }
}
