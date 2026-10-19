using Kaleido.UnitTests;
using NetArchTest.Rules;
using TestResult = NetArchTest.Rules.TestResult;

namespace System.Runtime.InteropServices.UnitTests;

/// <summary>
/// Enforces the project/namespace boundaries documented in src/AGENTS.md.
/// These rules turn "keep transport out of core" and capability isolation
/// invariants into executable checks instead of prose.
/// </summary>
public sealed class ArchitectureTests
    : SutFixture
{
    private static readonly System.Reflection.Assembly KaleidoAssembly =
        typeof(IKaleidoBuilder).Assembly;

    private static string Failures(TestResult result) =>
        string.Join(Environment.NewLine, result.FailingTypeNames ?? []);

    [Fact]
    public void Core_MustNotDependOnAspNetCore()
    {
        var result =
            Types.InAssembly(KaleidoAssembly)
                .ShouldNot()
                .HaveDependencyOn("Microsoft.AspNetCore")
                .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Core_MustNotDependOnHttpClient()
    {
        var result =
            Types.InAssembly(KaleidoAssembly)
                .ShouldNot()
                .HaveDependencyOn("System.Net.Http")
                .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Core_MustNotDependOnEntityFramework()
    {
        var result =
            Types.InAssembly(KaleidoAssembly)
                .ShouldNot()
                .HaveDependencyOn("Microsoft.EntityFrameworkCore")
                .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    [Fact]
    public void Core_MustNotDependOnTransportNamespaces()
    {
        var result =
            Types.InAssembly(KaleidoAssembly)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "Kaleido.Http",
                    "Kaleido.Http.Abstractions",
                    "Kaleido.Http.Client")
                .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    /// <summary>
    /// HTTP contract types (responses, the error body and its codes) live in
    /// Kaleido.Http.Abstractions, never in transport-free core (#298).
    /// </summary>
    [Fact]
    public void Core_MustNotContainHttpContractTypes()
    {
        var result =
            Types.InAssembly(KaleidoAssembly)
                .ShouldNot()
                .HaveNameEndingWith("Response")
                .Or()
                .HaveName("KaleidoError")
                .Or()
                .HaveName("KaleidoErrorCodes")
                .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    /// <summary>
    /// The Process runtime never parses input: a transport reads its wire format into the step type
    /// before core sees it (#344). Core's one sanctioned JSON use is the event outbox (Kaleido.Eventing).
    /// </summary>
    [Fact]
    public void Process_MustNotDependOnJson()
    {
        var result =
            Types.InAssembly(KaleidoAssembly)
                .That()
                .ResideInNamespace("Kaleido.Processor")
                .ShouldNot()
                .HaveDependencyOn("System.Text.Json")
                .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    /// <summary>
    /// Process is step-centric; Queryable is context/view-centric. They share
    /// only primitives (telemetry constants) via the Queryable → Process
    /// direction — Process must never reach back into Queryable.
    /// </summary>
    [Fact]
    public void Process_MustNotDependOnQueryable()
    {
        var result =
            Types.InAssembly(KaleidoAssembly)
                .That()
                .ResideInNamespace("Kaleido.Processor")
                .ShouldNot()
                .HaveDependencyOn("Kaleido.Queryable")
                .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }

    /// <summary>
    /// IKaleidoContextStore is the provider seam — core ships the default
    /// in-memory store and must never reference provider implementations.
    /// </summary>
    [Fact]
    public void Core_MustNotDependOnProviderImplementations()
    {
        var result =
            Types.InAssembly(KaleidoAssembly)
                .ShouldNot()
                .HaveDependencyOnAny(
                    "Microsoft.Data.Sqlite",
                    "System.Data.SQLite")
                .GetResult();

        Assert.True(result.IsSuccessful, Failures(result));
    }
}
