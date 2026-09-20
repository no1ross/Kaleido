using Kaleido.AspNetCore.Observability;
using Kaleido.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Kaleido.AspNetCore.Middleware;

internal sealed class CorrelationMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var initializer =
            context.RequestServices
                .GetService<IKaleidoCorrelationContextInitializer>();

        initializer?.Initialize(
            KaleidoAspNetCoreCorrelation.Create(context));

        await _next(context);
    }
}
