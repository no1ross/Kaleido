using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Design.StaticClassExtensionsAnalyzer>;

namespace Kaleido.Analyzers.Source.Design.UnitTests;

// #459: static is fine for mechanics (extension methods, stateless value mechanics - names, routes,
// URLs, formats); static is not where behavior or state lives.
public sealed class StaticClassExtensionsAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.Design.StaticClassExtensionsAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.Design.StaticClassExtensionsAnalyzer CreateSut() =>
        new();

    private static readonly DiagnosticResult Expected =
        new("KAL0001", DiagnosticSeverity.Warning);

    private const string Domain = @"
public sealed class PriorAuthorization { public bool Complete { get; init; } }
public interface IEligibility { bool IsEligible(string member); }";

    // ── Accepted: mechanics ───────────────────────────────────────────────────

    [Fact]
    public async Task StaticClass_WithExtensionMethod_NoDiagnostic()
    {
        await RunAsync(@"
public static class StringExt
{
    public static string Shout(this string s) => s + ""!"";
}");
    }

    // An extension method may work on any type: it is a helper attached to the type it extends.
    [Fact]
    public async Task StaticClass_WithExtensionMethodOnADomainType_NoDiagnostic()
    {
        await RunAsync(Domain + @"
public static class PriorAuthorizationExtensions
{
    public static bool IsComplete(this PriorAuthorization request) => request.Complete;
}");
    }

    [Fact]
    public async Task StaticClass_ConstantsOnly_NoDiagnostic()
    {
        await RunAsync(@"
public static class ErrorCodes
{
    public const string Foo = ""foo"";
    public static readonly string Bar = ""bar"";
}");
    }

    // The shapes of the route, URL and endpoint-name factories in src (they suppressed KAL0001 before).
    [Fact]
    public async Task StaticClass_RouteUrlAndNameFactories_NoDiagnostic()
    {
        await RunAsync(@"
using System;

internal static class ProcessContractUrls
{
    public const string Execute = ""execute"";

    internal static string ProcessesPrefix(string serviceName) => $""{serviceName}/processes"";

    public static string ProcessState(string serviceName, Guid processId) => $""{ProcessesPrefix(serviceName)}/{processId}"";
}

internal static class QueryableEndpointNames
{
    public static string QueryViewEndpointName(string sourceName, string viewName) => $""KaleidoQueryableViewQuery_{sourceName}_{viewName}"";
}

internal static class RegistryContractUrls
{
    public static string Registry(string? serviceName) => string.IsNullOrWhiteSpace(serviceName) ? ""registry"" : $""{serviceName}/registry"";
}");
    }

    [Fact]
    public async Task StaticClass_WithValueMechanics_OverSimpleTypes_NoDiagnostic()
    {
        await RunAsync(@"
using System;
using System.Collections.Generic;

public enum Unit { Seconds, Minutes }

public static class Formats
{
    public static int Add(int a, int b) => a + b;
    public static string Duration(TimeSpan value, Unit unit) => unit == Unit.Seconds ? value.TotalSeconds.ToString() : value.TotalMinutes.ToString();
    public static string? Join(IEnumerable<string> parts, char separator) => string.Join(separator, parts);
    public static bool TryParse(string text, out Guid id) => Guid.TryParse(text, out id);
    public static Uri Combine(Uri root, string path) => new(root, path);
    public static int? Length(string[]? values, DateTimeOffset? at) => values?.Length;
}");
    }

    [Fact]
    public async Task StaticClass_PrivateHelpers_AreNotJudged_NoDiagnostic()
    {
        await RunAsync(Domain + @"
public static class Names
{
    public static string For(string name) => Clean(new PriorAuthorization(), name);

    private static string Clean(PriorAuthorization request, string name) => name.Trim();
}");
    }

    [Fact]
    public async Task NonStaticClass_NoDiagnostic()
    {
        await RunAsync(Domain + @"
public class Rules
{
    private static int counter;

    public static bool CanSubmit(PriorAuthorization request) => request.Complete && counter++ >= 0;
}");
    }

    // ── Reported: behavior ────────────────────────────────────────────────────

    // Business rules hidden in a static class: they belong in an injectable concrete type.
    [Fact]
    public async Task StaticClass_WithAMethodOnADomainType_Reports()
    {
        await RunAsync(Domain + @"
public static class {|#0:PriorAuthorizationRules|}
{
    public static bool CanSubmit(PriorAuthorization request) => request.Complete;
}",
            Expected.WithLocation(0).WithArguments("PriorAuthorizationRules", "declares 'CanSubmit', which works on more than simple values"));
    }

    [Fact]
    public async Task StaticClass_WithAMethodTakingAService_Reports()
    {
        await RunAsync(Domain + @"
public static class {|#0:Eligibility|}
{
    public static bool Check(IEligibility eligibility, string member) => eligibility.IsEligible(member);
}",
            Expected.WithLocation(0).WithArguments("Eligibility", "declares 'Check', which works on more than simple values"));
    }

    [Fact]
    public async Task StaticClass_WithAMethodTakingTheServiceProvider_Reports()
    {
        await RunAsync(@"
using System;

public static class {|#0:Locator|}
{
    public static object? Find(IServiceProvider services, Type type) => services.GetService(type);
}",
            Expected.WithLocation(0).WithArguments("Locator", "declares 'Find', which works on more than simple values"));
    }

    [Fact]
    public async Task StaticClass_WithAGenericMethod_Reports()
    {
        await RunAsync(@"
public static class {|#0:Copier|}
{
    public static T Copy<T>(T value) => value;
}",
            Expected.WithLocation(0).WithArguments("Copier", "declares 'Copy', which works on more than simple values"));
    }

    // A method that returns nothing acts on something: that is behavior, not a value mechanic.
    [Fact]
    public async Task StaticClass_WithAVoidMethod_Reports()
    {
        await RunAsync(@"
public static class {|#0:Console2|}
{
    public static void Write(string text) => System.Console.WriteLine(text);
}",
            Expected.WithLocation(0).WithArguments("Console2", "declares 'Write', which works on more than simple values"));
    }

    // A read-only sequence of a domain type is not a simple value either.
    [Fact]
    public async Task StaticClass_WithASequenceOfDomainTypes_Reports()
    {
        await RunAsync(@"
using System.Collections.Generic;
using System.Linq;
" + Domain + @"

public static class {|#0:Counts|}
{
    public static int Complete(IReadOnlyList<PriorAuthorization> requests) => requests.Count(x => x.Complete);
}",
            Expected.WithLocation(0).WithArguments("Counts", "declares 'Complete', which works on more than simple values"));
    }

    // ── Reported: state ───────────────────────────────────────────────────────

    [Fact]
    public async Task StaticClass_WithAMutableStaticField_Reports()
    {
        await RunAsync(@"
public static class {|#0:Cache|}
{
    private static int hits;

    public static int Hit(int amount) => hits += amount;
}",
            Expected.WithLocation(0).WithArguments("Cache", "holds mutable state in 'hits'"));
    }

    [Fact]
    public async Task StaticClass_WithASettableStaticProperty_Reports()
    {
        await RunAsync(@"
public static class {|#0:Settings|}
{
    public static string Mode { get; set; } = ""on"";
}",
            Expected.WithLocation(0).WithArguments("Settings", "holds mutable state in 'Mode'"));
    }

    [Fact]
    public async Task StaticClass_WithAStaticEvent_Reports()
    {
        await RunAsync(@"
public static class {|#0:Signals|}
{
    public static event System.EventHandler? Changed;

    public static string Name(string value) => Changed is null ? value : value + ""!"";
}",
            Expected.WithLocation(0).WithArguments("Signals", "holds mutable state in 'Changed'"));
    }
}
