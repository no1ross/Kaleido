using Kaleido.Samples.PriorAuth.EventCollector.Data;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Text.Json;
using System.Text.Json.Nodes;

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

var eventCollectorConnectionString =
    builder.Configuration.GetConnectionString("EventCollector")
    ?? throw new Kaleido.Exceptions.KaleidoConfigurationException(
        "ConnectionStrings:EventCollector is required.");

builder.Services.AddDbContext<EventCollectorDbContext>(
    options => options.UseSqlite(eventCollectorConnectionString));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EventCollectorDbContext>();

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

// POST /events — accepts the envelope produced by HttpEventPublisher:
// { EventType, Context: { RequestId, ServiceName, ProcessId?, StepName?, ... }, Event: { ... } }
app.MapPost("/events", async (
    JsonObject body,
    EventCollectorDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var eventType = body["eventType"]?.GetValue<string>() ?? string.Empty;
    var context = body["context"]?.AsObject();
    var eventNode = body["event"];

    var requestId = context?["requestId"]?.GetValue<string>() ?? string.Empty;
    var serviceName2 = context?["serviceName"]?.GetValue<string>() ?? string.Empty;

    Guid? processId = null;
    if (context?["processId"] is JsonNode pidNode &&
        Guid.TryParse(pidNode.GetValue<string>(), out var pid))
        processId = pid;

    var stepName = context?["stepName"]?.GetValue<string>();

    // OccurredOn lives on the event payload
    var occurredOnStr = eventNode?["occurredOn"]?.GetValue<string>();
    var occurredOn = occurredOnStr is not null
        ? DateTimeOffset.Parse(occurredOnStr).UtcDateTime
        : DateTime.UtcNow;

    dbContext.Events.Add(new CollectedEvent
    {
        EventType = eventType,
        RequestId = requestId,
        ServiceName = serviceName2,
        ProcessId = processId,
        StepName = stepName,
        OccurredOn = occurredOn,
        ReceivedOn = DateTime.UtcNow,
        ContextJson = context?.ToJsonString() ?? "{}",
        EventJson = eventNode?.ToJsonString() ?? "{}"
    });

    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Accepted();
});

// GET /events — recent events, newest first
app.MapGet("/events", async (
    EventCollectorDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var events =
        await dbContext.Events
            .AsNoTracking()
            .OrderByDescending(x => x.Id)
            .Take(200)
            .Select(x => new
            {
                x.Id,
                x.EventType,
                x.RequestId,
                x.ServiceName,
                x.ProcessId,
                x.StepName,
                x.OccurredOn,
                x.ReceivedOn,
                x.ContextJson,
                x.EventJson
            })
            .ToListAsync(cancellationToken);

    return Results.Ok(events);
});

// GET /events/by-request/{requestId} — all events for a correlation request
app.MapGet("/events/by-request/{requestId}", async (
    string requestId,
    EventCollectorDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var events =
        await dbContext.Events
            .AsNoTracking()
            .Where(x => x.RequestId == requestId)
            .OrderBy(x => x.OccurredOn)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.EventType,
                x.RequestId,
                x.ServiceName,
                x.ProcessId,
                x.StepName,
                x.OccurredOn,
                x.ReceivedOn,
                x.ContextJson,
                x.EventJson
            })
            .ToListAsync(cancellationToken);

    return Results.Ok(events);
});

// GET /events/by-process/{processId} — all events for a process
app.MapGet("/events/by-process/{processId:guid}", async (
    Guid processId,
    EventCollectorDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var events =
        await dbContext.Events
            .AsNoTracking()
            .Where(x => x.ProcessId == processId)
            .OrderBy(x => x.OccurredOn)
            .ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.EventType,
                x.RequestId,
                x.ServiceName,
                x.ProcessId,
                x.StepName,
                x.OccurredOn,
                x.ReceivedOn,
                x.ContextJson,
                x.EventJson
            })
            .ToListAsync(cancellationToken);

    return Results.Ok(events);
});

// GET /processes — distinct processes with summary info
app.MapGet("/processes", async (
    EventCollectorDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var processes =
        await dbContext.Events
            .AsNoTracking()
            .Where(x => x.ProcessId.HasValue)
            .GroupBy(x => x.ProcessId!.Value)
            .Select(g => new
            {
                ProcessId = g.Key,
                EventCount = g.Count(),
                MostRecentOccurredOn = g.Max(x => x.OccurredOn)
            })
            .OrderByDescending(x => x.MostRecentOccurredOn)
            .ToListAsync(cancellationToken);

    return Results.Ok(processes);
});

app.Run();
