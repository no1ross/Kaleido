using Kaleido;
using Kaleido.Process;
using Kaleido.Process.AspNetCore;
using Kaleido.Process.Providers.SQLite;
using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Microsoft.EntityFrameworkCore;
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

var radiologyConnectionString =
    builder.Configuration.GetConnectionString("Radiology")
    ?? "Data Source=data/radiology.db";

var processConnectionString =
    builder.Configuration.GetConnectionString("RadiologyProcess")
    ?? "Data Source=data/radiology-process.db";

builder.Services.AddDbContext<RadiologyDbContext>(
    options => options.UseSqlite(
        radiologyConnectionString));

builder.Services.AddHttpClient("ReferenceData", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:ReferenceData:BaseUrl"]
        ?? "https://localhost:8441");
});

builder.Services.AddHttpClient("MemberService", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:MemberService:BaseUrl"]
        ?? "https://localhost:8444");
});

builder.Services.AddHttpClient("CodeSet", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:CodeSet:BaseUrl"]
        ?? "https://localhost:8442");
});

builder.Services.AddHttpClient("Configuration", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:Configuration:BaseUrl"]
        ?? "https://localhost:8447");
});

builder.Services.AddHttpClient("ProviderSearch", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:ProviderSearch:BaseUrl"]
        ?? "https://localhost:8443");
});

builder.Services.AddScoped<Kaleido.Samples.PriorAuth.Radiology.Process.Services.QueryableHttpClient>();
builder.Services.AddScoped<Kaleido.Samples.PriorAuth.Radiology.Process.Services.MemberDetailsClient>();
builder.Services.AddScoped<Kaleido.Samples.PriorAuth.Radiology.Process.Services.ProcedureCodeClient>();
builder.Services.AddScoped<Kaleido.Samples.PriorAuth.Radiology.Process.Services.ProcedureModalityClient>();
builder.Services.AddScoped<Kaleido.Samples.PriorAuth.Radiology.Process.Services.MriProcedureCodeResolverClient>();
builder.Services.AddScoped<Kaleido.Samples.PriorAuth.Radiology.Process.Services.QuestionnaireDefinitionClient>();
builder.Services.AddScoped<Kaleido.Samples.PriorAuth.Radiology.Process.Services.RequestingProviderSearchClient>();

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
    .AddDbContextCheck<RadiologyDbContext>();

builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(RadiologyDbContext).Assembly)
    .AddProcessor(o =>
        {
            o.Name = "radiology";
            o.Description = "Prior authorization radiology processor.";
            o.Version = "1.0.0";
            o.DisplayName = "Prior Auth Radiology";
        })
        .AddProcessorAspNetCore(o =>
        {
            o.RoutePrefix = "radiology";
        })
        .UseSqliteProcessContextStore(processConnectionString)
    .AddQueryable()
        .AddQueryableAspNetCore(o =>
        {
            o.RoutePrefix = "radiology";
        });

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");

app.MapProcessor();
app.MapQueryable();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<RadiologyDbContext>();
    var processDbContext =
        scope.ServiceProvider.GetRequiredService<SqliteProcessContextDbContext>();
    var httpClientFactory =
        scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var logger =
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");

    await dbContext.Database.EnsureCreatedAsync();
    await processDbContext.Database.EnsureCreatedAsync();

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
