using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using static Kaleido.Analyzers.Tests.AnalyzerTest<
    Kaleido.Analyzers.AsyncCancellationTokenAnalyzer>;

namespace Kaleido.Analyzers.Tests;

public sealed class AsyncCancellationTokenAnalyzerTests
{
    private static readonly DiagnosticResult Expected =
        new("KAL0019", DiagnosticSeverity.Warning);

    // Minimal stubs — the HttpContext name must match the full CLR name the
    // analyzer checks, so we declare it in the right namespace.
    private const string HttpContextStub = @"
namespace Microsoft.AspNetCore.Http
{
    public class HttpContext { }
}";

    // ── No-diagnostic cases ──────────────────────────────────────────────────

    [Fact]
    public async Task AsyncMethod_WithCancellationToken_NoDiagnostic()
    {
        await RunAsync(@"
using System.Threading;
using System.Threading.Tasks;
public class Service
{
    public async Task DoWorkAsync(CancellationToken cancellationToken) => await Task.CompletedTask;
}");
    }

    [Fact]
    public async Task AsyncMethod_WithCancellationTokenDefault_NoDiagnostic()
    {
        await RunAsync(@"
using System.Threading;
using System.Threading.Tasks;
public class Service
{
    public async Task<int> GetValueAsync(string key, CancellationToken cancellationToken = default) => await Task.FromResult(0);
}");
    }

    [Fact]
    public async Task Override_WithoutCancellationToken_NoDiagnostic()
    {
        // The override itself is exempt — signature fixed at the base.
        // The base abstract method fires (correctly); suppressed in test source.
        await RunAsync(@"
using System.Threading.Tasks;
public abstract class Base
{
#pragma warning disable KAL0019
    public abstract Task DoWorkAsync();
#pragma warning restore KAL0019
}
public class Derived : Base
{
    public override async Task DoWorkAsync() => await Task.CompletedTask;
}");
    }

    [Fact]
    public async Task ExplicitInterfaceImpl_WithoutCancellationToken_NoDiagnostic()
    {
        // Explicit interface implementations are exempt.
        // The interface method declaration fires (correctly); suppressed in test source.
        await RunAsync(@"
using System.Threading.Tasks;
public interface IService
{
#pragma warning disable KAL0019
    Task DoWorkAsync();
#pragma warning restore KAL0019
}
public class Service : IService
{
    async Task IService.DoWorkAsync() => await Task.CompletedTask;
}");
    }

    [Fact]
    public async Task PrivateAsync_WithoutCancellationToken_NoDiagnostic()
    {
        await RunAsync(@"
using System.Threading.Tasks;
public class Service
{
    private async Task DoWorkAsync() => await Task.CompletedTask;
}");
    }

    [Fact]
    public async Task NonTask_ReturnType_NoDiagnostic()
    {
        // Synchronous public methods are not in scope.
        await RunAsync(@"
public class Service
{
    public void DoWork() { }
    public int GetValue() => 0;
}");
    }

    [Fact]
    public async Task InvokeAsync_SingleHttpContext_NoDiagnostic()
    {
        // ASP.NET Core middleware convention is exempt.
        await RunAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
public class MyMiddleware
{
    public async Task InvokeAsync(HttpContext context) => await Task.CompletedTask;
}" + HttpContextStub);
    }

    // ── Diagnostic cases ─────────────────────────────────────────────────────

    [Fact]
    public async Task PublicAsync_Task_NoCancellationToken_Reports()
    {
        await RunAsync(@"
using System.Threading.Tasks;
public class Service
{
    public async Task {|#0:DoWorkAsync|}() => await Task.CompletedTask;
}",
            Expected.WithLocation(0).WithArguments("DoWorkAsync"));
    }

    [Fact]
    public async Task PublicAsync_TaskOfT_NoCancellationToken_Reports()
    {
        await RunAsync(@"
using System.Threading.Tasks;
public class Service
{
    public async Task<int> {|#0:GetValueAsync|}() => await Task.FromResult(0);
}",
            Expected.WithLocation(0).WithArguments("GetValueAsync"));
    }

    [Fact]
    public async Task InternalAsync_NoCancellationToken_Reports()
    {
        await RunAsync(@"
using System.Threading.Tasks;
public class Service
{
    internal async Task {|#0:DoWorkAsync|}() => await Task.CompletedTask;
}",
            Expected.WithLocation(0).WithArguments("DoWorkAsync"));
    }

    [Fact]
    public async Task InvokeAsync_WithExtraParam_NoCancellationToken_Reports()
    {
        // InvokeAsync is only exempt when its sole parameter is HttpContext.
        await RunAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
public class MyMiddleware
{
    public async Task {|#0:InvokeAsync|}(HttpContext context, string extra) => await Task.CompletedTask;
}" + HttpContextStub,
            Expected.WithLocation(0).WithArguments("InvokeAsync"));
    }

    [Fact]
    public async Task Interface_AsyncMethod_NoCancellationToken_Reports()
    {
        await RunAsync(@"
using System.Threading.Tasks;
public interface IService
{
    Task {|#0:DoWorkAsync|}();
}",
            Expected.WithLocation(0).WithArguments("DoWorkAsync"));
    }
}
