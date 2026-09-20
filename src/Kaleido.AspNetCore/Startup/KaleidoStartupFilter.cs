using Kaleido.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace Kaleido.AspNetCore.Startup;

internal sealed class KaleidoStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // Register Kaleido middleware first in the pipeline
            app.UseMiddleware<CorrelationMiddleware>();
            app.UseMiddleware<ExceptionMiddleware>();

            // Continue with the rest of the application configuration
            next(app);
        };
    }
}
