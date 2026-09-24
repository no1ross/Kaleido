using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.Http.UnitTests.Middleware;

public sealed class ObservabilityMiddlewareTests
{
    private static ObservabilityMiddleware CreateSut(RequestDelegate? next = null) =>
        new(next ?? (_ => Task.CompletedTask));

    [Fact]
    public async Task InvokeAsync_WhenContextInitializerNotRegistered_DoesNotThrow()
    {
        var sut = CreateSut();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };

        await sut.InvokeAsync(httpContext);
    }
}
