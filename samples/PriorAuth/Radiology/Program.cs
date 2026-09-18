using Kaleido;
using Kaleido.Exceptions;
using Kaleido.Process;
using Kaleido.Process.AspNetCore;
using Kaleido.Process.Providers.SQLite;
using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
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
    ?? throw new KaleidoConfigurationException(
        "ConnectionStrings:Radiology is required.");

var processConnectionString =
    builder.Configuration.GetConnectionString("RadiologyProcess")
    ?? throw new KaleidoConfigurationException(
        "ConnectionStrings:RadiologyProcess is required.");

builder.Services.AddDbContext<RadiologyDbContext>(
    options => options.UseSqlite(radiologyConnectionString));

builder.Services.AddScoped<IMemberEligibilityService, MemberEligibilityService>();
builder.Services.AddScoped<MemberDetailsClient>();
builder.Services.AddScoped<ProcedureCodeClient>();
builder.Services.AddScoped<ProcedureModalityClient>();
builder.Services.AddScoped<MriProcedureCodeResolverClient>();
builder.Services.AddScoped<QuestionnaireDefinitionClient>();
builder.Services.AddScoped<RequestingProviderSearchClient>();
builder.Services.AddScoped<HistoryClient>();

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

builder.Services.AddPriorAuthEventPublishing(
    builder.Configuration);

builder.Services.AddKaleido(builder.Configuration)
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(RadiologyDbContext).Assembly)
    .AddProcessor()
        .AddProcessorAspNetCore()
        .UseSqliteProcessContextStore(processConnectionString)
    .AddQueryable()
        .AddQueryableAspNetCore()
    .AddProcessClients("Member", "History")
    .AddQueryableClients("Member", "CodeSet", "Configuration", "Provider", "History");

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

    await dbContext.Database.EnsureCreatedAsync();
    await processDbContext.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
