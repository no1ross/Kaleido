using Kaleido.AspNetCore.Observability;
using Kaleido.Observability;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Kaleido.AspNetCore.Middleware;

internal sealed class CorrelationMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var initializer =
            context.RequestServices
                .GetService<IKaleidoCorrelationContextInitializer>();

        var correlation = KaleidoAspNetCoreCorrelation.Create(context);

        initializer?.Initialize(correlation);

        // Tag the current Activity (created by ASP.NET Core instrumentation)
        // so the Kaleido RequestId appears on every span for this request.
        Activity.Current?.SetTag("kaleido.request.id", correlation.RequestId);

        // Echo RequestId on the response so callers can correlate with backend traces.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[KaleidoCorrelationHeaders.RequestId] =
                correlation.RequestId;
            return Task.CompletedTask;
        });

        await next(context);
    }
}
