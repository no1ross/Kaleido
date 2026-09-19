using Kaleido.AspNetCore.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;

namespace Kaleido;

public static class AspNetCoreServiceCollectionExtensions
{
    public static IApplicationBuilder UseKaleidoExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionMiddleware>();
    }
}
