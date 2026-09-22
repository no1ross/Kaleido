using Kaleido.Http.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Kaleido.Http.Startup;

internal sealed class KaleidoStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // ExceptionMiddleware must be outermost so it catches exceptions from
            // all inner middleware (including ObservabilityMiddleware) and the app pipeline.
            app.UseMiddleware<ExceptionMiddleware>();
            app.UseMiddleware<ObservabilityMiddleware>();

            next(app);
        };
    }
}
