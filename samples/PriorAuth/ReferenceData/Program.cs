using Kaleido;
using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.ReferenceData.Data;
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

var referenceDataConnectionString =
    builder.Configuration.GetConnectionString("ReferenceData")
    ?? throw new Kaleido.Exceptions.KaleidoConfigurationException(
        "ConnectionStrings:ReferenceData is required.");

builder.Services.AddDbContext<ReferenceDataDbContext>(
    options => options.UseSqlite(referenceDataConnectionString));

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
    .AddDbContextCheck<ReferenceDataDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration)
    .AddEventPublisher<HttpEventPublisher>()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(ReferenceDataDbContext).Assembly)
    .AddQueryable()
        .AddQueryableAspNetCore();

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
