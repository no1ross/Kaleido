using Kaleido.Samples.PriorAuth.Common;
using Kaleido.Samples.PriorAuth.EventCollector.Data;
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

builder.Services.AddDbContext<EventCollectorDbContext>(
    options => options.UseSqlite(
        builder.Configuration.GetConnectionString("EventCollector")
        ?? "Data Source=data/eventcollector.db"));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EventCollectorDbContext>();

var app = builder.Build();

app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<EventCollectorDbContext>();

    await dbContext.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/events", async (
    EventEnvelope envelope,
    EventCollectorDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    dbContext.Events.Add(
        new CollectedEvent
        {
            ProcessId = envelope.ProcessId,
            OccurredOn = envelope.OccurredOn,
            EventType = envelope.EventType,
            PayloadJson = envelope.Payload.GetRawText(),
            ReceivedOn = DateTimeOffset.UtcNow
        });

    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Accepted();
});

app.MapGet("/process-events/{processId:guid}", async (
    Guid processId,
    EventCollectorDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var events =
        await dbContext.Events
            .AsNoTracking()
            .Where(x => x.ProcessId == processId)
            .Select(x => new
            {
                x.Id,
                x.ProcessId,
                x.OccurredOn,
                x.ReceivedOn,
                x.EventType,
                x.PayloadJson
            })
            .ToListAsync(cancellationToken);

    return Results.Ok(
        events
            .OrderBy(x => x.OccurredOn)
            .ThenBy(x => x.Id));
});

app.MapGet("/events", async (
    EventCollectorDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var events =
        await dbContext.Events
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(100)
            .Select(x => new
            {
                x.Id,
                x.ProcessId,
                x.OccurredOn,
                x.ReceivedOn,
                x.EventType,
                x.PayloadJson
            })
            .ToListAsync(cancellationToken);

    return Results.Ok(events);
});

app.Run();
