using Kaleido;
using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Provider.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

var serviceName =
    builder.Configuration["OTEL_SERVICE_NAME"]
    ?? builder.Environment.ApplicationName;

var resourceBuilder =
    ResourceBuilder.CreateDefault()
        .AddService(serviceName: serviceName);

builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeFormattedMessage = true;
    options.IncludeScopes = true;
    options.SetResourceBuilder(resourceBuilder);
    options.AddOtlpExporter();
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource =>
        resource.AddService(serviceName: serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddKaleidoQueryableInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddKaleidoQueryableInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter();
    });

builder.Services.AddDbContext<ProviderSearchDbContext>(
    options => options.UseSqlite(
        builder.Configuration.GetConnectionString("ProviderSearch")
        ?? "Data Source=data/providersearch.db"));

builder.Services.AddHttpClient("ReferenceData", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:ReferenceData:BaseUrl"]
        ?? "https://localhost:8441");
});

builder.Services.AddScoped<Kaleido.Samples.PriorAuth.Provider.Queryable.Clients.ReferenceDataClient>();

builder.Services.AddControllers();
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
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ProviderSearchDbContext>();

builder.Services.AddPriorAuthEventPublishing(
    builder.Configuration);

builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(ProviderSearchDbContext).Assembly)
    .AddQueryable()
        .AddQueryableAspNetCore(o =>
        {
            o.RoutePrefix = "provider";
        });

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");
app.MapQueryable();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
