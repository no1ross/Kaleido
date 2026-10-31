using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Design.OneTryTwoEffectsAnalyzer>;

namespace Kaleido.Analyzers.Source.Design.UnitTests;

public sealed class OneTryTwoEffectsAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.Design.OneTryTwoEffectsAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.Design.OneTryTwoEffectsAnalyzer CreateSut() =>
        new();

    private static readonly DiagnosticResult Expected =
        new("KAL0022", DiagnosticSeverity.Warning);

    // Dependencies with effects and reads, an observer, and a concrete helper.
    private static string Worker(string body, string members = "") => @"
using System;
using System.Threading;
using System.Threading.Tasks;

public interface IHandler { Task<int> ExecuteAsync(); }
public interface IStore { Task SaveAsync(); Task<int> LoadAsync(); Task CompleteAsync(int id); }
public interface IClient { Task SendAsync(); Task<int> GetStateAsync(); }
public interface IStepObservation { void Failed(Exception exception); }
public sealed class Serializer { public Task WriteAsync() => Task.CompletedTask; }

public class Worker
{
    private readonly IHandler handler = null;
    private readonly IStore store = null;
    private readonly IStore other = null;
    private readonly IStepObservation observation = null;
    private readonly Serializer serializer = new Serializer();

    private IClient Client(string service) => null;

    public async Task RunAsync(bool flag, int kind, CancellationToken cancellationToken)
    {
" + body + @"
    }
" + members + @"
}";

    // The defect of #537: the handler ran, then the save failed, and the catch recorded a failed step.
    [Fact]
    public async Task AnEffectThenAnotherEffect_WithACatchThatHandles_Reports()
    {
        await RunAsync(
            Worker(@"
        {|#0:try|}
        {
            await handler.ExecuteAsync();
            await store.SaveAsync();
        }
        catch (Exception) { await other.SaveAsync(); }"),
            Expected.WithLocation(0).WithArguments("handler.ExecuteAsync", "store.SaveAsync"));
    }

    // The same through the type's own helpers, as the executor had it.
    [Fact]
    public async Task ThroughTheTypesOwnMethods_Reports()
    {
        await RunAsync(
            Worker(@"
        {|#0:try|}
        {
            await InvokeAsync();
            await SaveStepAsync();
        }
        catch (Exception) { }",
                @"
    private Task<int> InvokeAsync() => handler.ExecuteAsync();

    private async Task SaveStepAsync() => await Inner();

    private Task Inner() => store.SaveAsync();"),
            Expected.WithLocation(0).WithArguments("handler.ExecuteAsync", "store.SaveAsync"));
    }

    // The MCP shape: a step ran, then reading where it continues failed - a read after an effect counts,
    // and a factory called for another target is another dependency.
    [Fact]
    public async Task AnEffectThenAReadOnAnotherTarget_Reports()
    {
        await RunAsync(
            Worker(@"
        {|#0:try|}
        {
            await Client(""intake"").SendAsync();
            await Client(""radiology"").GetStateAsync();
        }
        catch (InvalidOperationException) { return; }"),
            Expected.WithLocation(0).WithArguments(@"Client(""intake"").SendAsync", @"Client(""radiology"").GetStateAsync"));
    }

    // The remedy: the later operation's failure is caught where it happens.
    [Fact]
    public async Task TheLaterOperationCaughtWhereItHappens_NoDiagnostic()
    {
        await RunAsync(Worker(@"
        try
        {
            await handler.ExecuteAsync();

            try
            {
                await store.SaveAsync();
            }
            catch (Exception) { }
        }
        catch (Exception) { }"));
    }

    // A catch that only observes and rethrows decides nothing; nor does a catch of the caller's
    // cancellation (#517); nor a try with only a finally.
    [Fact]
    public async Task CatchesThatDoNotHandleTheFailure_NoDiagnostic()
    {
        await RunAsync(Worker(@"
        try
        {
            await handler.ExecuteAsync();
            await store.SaveAsync();
        }
        catch (Exception exception)
        {
            observation.Failed(exception);
            throw;
        }

        try
        {
            await handler.ExecuteAsync();
            await store.SaveAsync();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }

        try
        {
            await handler.ExecuteAsync();
            await store.SaveAsync();
        }
        finally { }"));
    }

    // Reads before an effect (nothing has happened when they fail), the same dependency twice (one
    // dependency, one failure), and synchronous or concrete calls (not operations on a dependency).
    [Fact]
    public async Task NoEffectBeforeAnotherDependency_NoDiagnostic()
    {
        await RunAsync(Worker(@"
        try
        {
            await store.LoadAsync();
            await Client(""intake"").GetStateAsync();
            await handler.ExecuteAsync();
        }
        catch (Exception) { }

        try
        {
            await store.SaveAsync();
            await store.CompleteAsync(1);
        }
        catch (Exception) { }

        try
        {
            await handler.ExecuteAsync();
            await serializer.WriteAsync();
            observation.Failed(null);
        }
        catch (Exception) { }"));
    }

    // Alternatives never both run: if/else, a switch expression, ?: and an early return.
    [Fact]
    public async Task OperationsInAlternativeBranches_NoDiagnostic()
    {
        await RunAsync(Worker(@"
        try
        {
            if (flag)
            {
                await handler.ExecuteAsync();
            }
            else
            {
                await store.SaveAsync();
            }
        }
        catch (Exception) { }

        try
        {
            var _ = kind switch
            {
                0 => await handler.ExecuteAsync(),
                _ => await store.LoadAsync(),
            };
        }
        catch (Exception) { }

        try
        {
            await (flag ? handler.ExecuteAsync() : store.LoadAsync());
        }
        catch (Exception) { }

        try
        {
            if (flag)
            {
                await handler.ExecuteAsync();
                return;
            }

            await store.SaveAsync();
        }
        catch (Exception) { }"));
    }

    // An effect before the branch still precedes each arm.
    [Fact]
    public async Task AnEffectBeforeABranch_Reports()
    {
        await RunAsync(
            Worker(@"
        {|#0:try|}
        {
            await handler.ExecuteAsync();

            if (flag)
            {
                await store.SaveAsync();
            }
        }
        catch (Exception) { }"),
            Expected.WithLocation(0).WithArguments("handler.ExecuteAsync", "store.SaveAsync"));
    }

    // Methods calling each other are followed once each, and the walk ends.
    [Fact]
    public async Task MethodsCallingEachOther_AreFollowedOnce()
    {
        await RunAsync(
            Worker(@"
        {|#0:try|}
        {
            await Ping(3);
        }
        catch (Exception) { }",
                @"
    private async Task Ping(int n) { await handler.ExecuteAsync(); if (n > 0) { await Pong(n - 1); } }

    private async Task Pong(int n) { if (n > 0) { await Ping(n - 1); } await store.SaveAsync(); }"),
            Expected.WithLocation(0).WithArguments("handler.ExecuteAsync", "store.SaveAsync"));
    }
}
