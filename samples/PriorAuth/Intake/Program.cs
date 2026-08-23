using Kaleido.Samples.PriorAuth.Intake.Data;
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
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter();
    });

builder.Services.AddDbContext<IntakeDbContext>(
    options => options.UseSqlite(
        builder.Configuration.GetConnectionString("Intake")
        ?? "Data Source=data/intake.db"));

builder.Services.AddHttpClient("ReferenceData", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:ReferenceData:BaseUrl"]
        ?? "https://localhost:8441");
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IntakeDbContext>();

var app = builder.Build();

app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
    var httpClientFactory =
        scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var logger =
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");

    await dbContext.Database.EnsureCreatedAsync();

    try
    {
        using var referenceDataResponse =
            await httpClientFactory
                .CreateClient("ReferenceData")
                .GetAsync("/health");

        referenceDataResponse.EnsureSuccessStatusCode();

        logger.LogInformation(
            "Verified ReferenceData connectivity at startup with status code {StatusCode}.",
            (int)referenceDataResponse.StatusCode);
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Failed to verify ReferenceData connectivity at startup.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
