using Kaleido;
using Kaleido.Registry;
using Kaleido.Observability;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms(context =>
    {
        context.AddRequestTransform(transformContext =>
        {
            if (!transformContext.ProxyRequest.Headers.Contains(KaleidoCorrelationHeaders.RequestId))
            {
                transformContext.ProxyRequest.Headers.TryAddWithoutValidation(
                    KaleidoCorrelationHeaders.RequestId,
                    Guid.NewGuid().ToString());
            }

            return ValueTask.CompletedTask;
        });
    });

builder.Services.AddKaleido(builder.Configuration)
    .AddHttpClients();

builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");
app.MapRegistry();
app.MapReverseProxy();

app.Run();
