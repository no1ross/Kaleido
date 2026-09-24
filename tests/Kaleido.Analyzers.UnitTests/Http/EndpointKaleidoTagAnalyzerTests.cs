using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Http.EndpointKaleidoTagAnalyzer>;

namespace Kaleido.Analyzers.UnitTests;

public sealed class EndpointKaleidoTagAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0017", DiagnosticSeverity.Warning);

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
}";

    [Fact]
    public async Task MapGet_WithKaleidoTagAmongOthers_NoDiagnostic()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapGet(""/processes"", () => { }).WithTags(""Processes"", ""Kaleido"");
    }
}" + BuilderStub);
    }

    [Fact]
    public async Task MapPost_WithOnlyKaleidoTag_NoDiagnostic()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapPost(""/processes"", () => { }).WithTags(""Kaleido"");
    }
}" + BuilderStub);
    }

    [Fact]
    public async Task MapGet_WithKaleidoTagFirst_NoDiagnostic()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapGet(""/processes"", () => { }).WithTags(""Kaleido"", ""Extra"");
    }
}" + BuilderStub);
    }

    [Fact]
    public async Task MapGet_WithTagsMissingKaleido_Reports()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapGet(""/processes"", () => { }).{|#0:WithTags|}(""Processes"");
    }
}" + BuilderStub,
            Expected.WithLocation(0));
    }

    [Fact]
    public async Task MapPost_WithTagsMissingKaleido_Reports()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapPost(""/processes"", () => { }).{|#0:WithTags|}(""Something"", ""Other"");
    }
}" + BuilderStub,
            Expected.WithLocation(0));
    }

    [Fact]
    public async Task MapGet_WithoutWithTags_NoDiagnosticFromKal0017()
    {
        // When WithTags is absent entirely, KAL0016 fires — KAL0017 stays silent.
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapGet(""/processes"", () => { }).WithName(""GetProcesses"");
    }
}" + BuilderStub);
    }

    [Fact]
    public async Task MapGet_WithNameBeforeWithTagsMissingKaleido_Reports()
    {
        await RunAsync(@"
public class Sample
{
    public void Register(App app)
    {
        app.MapGet(""/processes"", () => { }).WithName(""GetProcesses"").{|#0:WithTags|}(""Processes"");
    }
}" + BuilderStub,
            Expected.WithLocation(0));
    }
}
