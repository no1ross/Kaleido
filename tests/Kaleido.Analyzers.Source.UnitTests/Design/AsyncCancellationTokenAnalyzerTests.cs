using Kaleido.Testing;
using Microsoft.CodeAnalysis;
using static Kaleido.Analyzers.Source.UnitTests.AnalyzerTest<
    Kaleido.Analyzers.Source.Design.AsyncCancellationTokenAnalyzer>;

namespace Kaleido.Analyzers.Source.Design.UnitTests;

public sealed class AsyncCancellationTokenAnalyzerTests
    : global::Kaleido.UnitTests.SutFixture<Kaleido.Analyzers.Source.Design.AsyncCancellationTokenAnalyzer>
{
    protected override global::Kaleido.Analyzers.Source.Design.AsyncCancellationTokenAnalyzer CreateSut() =>
        new();

    private static readonly DiagnosticResult Expected =
        new("KAL0016", DiagnosticSeverity.Warning);

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

    // #313: every caller passes its token on, so no method has a default to fall back on -
    // public or internal, an interface or a class.
    [Fact]
    public async Task PublicMethod_WithCancellationTokenDefault_Reports()
    {
        await RunAsync(@"
using System.Threading;
using System.Threading.Tasks;
public class Service
{
    public async Task<int> {|#0:GetValueAsync|}(string key, CancellationToken cancellationToken = default) => await Task.FromResult(0);
}",
            Expected.WithLocation(0).WithArguments("GetValueAsync", AsyncCancellationTokenAnalyzer.DefaultedToken));
    }

    [Fact]
    public async Task InternalMethod_WithCancellationTokenDefault_Reports()
    {
        await RunAsync(@"
using System.Threading;
using System.Threading.Tasks;
internal class Service
{
    internal async Task<int> {|#0:GetValueAsync|}(CancellationToken cancellationToken = default) => await Task.FromResult(0);
}",
            Expected.WithLocation(0).WithArguments("GetValueAsync", AsyncCancellationTokenAnalyzer.DefaultedToken));
    }

    [Fact]
    public async Task Interface_WithCancellationTokenDefault_Reports()
    {
        await RunAsync(@"
using System.Threading;
using System.Threading.Tasks;
public interface IService
{
    Task {|#0:DoWorkAsync|}(CancellationToken cancellationToken = default);
}",
            Expected.WithLocation(0).WithArguments("DoWorkAsync", AsyncCancellationTokenAnalyzer.DefaultedToken));
    }

    // An implementation follows its interface (here one whose token has a default, as
    // Microsoft's IHealthCheck does): the interface is checked, not the implementation.
    [Fact]
    public async Task ImplicitInterfaceImplementation_WithCancellationTokenDefault_NoDiagnostic()
    {
        await RunAsync(@"
using System.Threading;
using System.Threading.Tasks;
public interface IExternal
{
#pragma warning disable KAL0016
    Task CheckAsync(CancellationToken cancellationToken = default);
#pragma warning restore KAL0016
}
public class Check : IExternal
{
    public async Task CheckAsync(CancellationToken cancellationToken = default) => await Task.CompletedTask;
}");
    }

    // The minimal-API binding hook: ASP.NET Core fixes the signature; the token is RequestAborted.
    [Fact]
    public async Task BindAsync_WithHttpContextOnly_NoDiagnostic()
    {
        await RunAsync(@"
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
public class Body
{
    public static async ValueTask<Body?> BindAsync(HttpContext context) => await Task.FromResult<Body?>(null);
}" + HttpContextStub);
    }

    [Fact]
    public async Task ValueTaskMethod_NoCancellationToken_Reports()
    {
        await RunAsync(@"
using System.Threading.Tasks;
public class Service
{
    public async ValueTask<int> {|#0:GetValueAsync|}() => await Task.FromResult(0);
}",
            Expected.WithLocation(0).WithArguments("GetValueAsync", AsyncCancellationTokenAnalyzer.MissingToken));
    }

    // A private helper is not checked (the rule covers public and internal methods).
    [Fact]
    public async Task PrivateMethod_WithCancellationTokenDefault_NoDiagnostic()
    {
        await RunAsync(@"
using System.Threading;
using System.Threading.Tasks;
public class Service
{
    private async Task<int> GetValueAsync(CancellationToken cancellationToken = default) => await Task.FromResult(0);
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
#pragma warning disable KAL0016
    public abstract Task DoWorkAsync();
#pragma warning restore KAL0016
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
#pragma warning disable KAL0016
    Task DoWorkAsync();
#pragma warning restore KAL0016
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
            Expected.WithLocation(0).WithArguments("DoWorkAsync", AsyncCancellationTokenAnalyzer.MissingToken));
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
            Expected.WithLocation(0).WithArguments("GetValueAsync", AsyncCancellationTokenAnalyzer.MissingToken));
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
            Expected.WithLocation(0).WithArguments("DoWorkAsync", AsyncCancellationTokenAnalyzer.MissingToken));
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
            Expected.WithLocation(0).WithArguments("InvokeAsync", AsyncCancellationTokenAnalyzer.MissingToken));
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
            Expected.WithLocation(0).WithArguments("DoWorkAsync", AsyncCancellationTokenAnalyzer.MissingToken));
    }
}
