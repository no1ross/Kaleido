using Kaleido.Samples.PriorAuth.Compliance;
using System.Globalization;
using System.Text.Json.Nodes;
using Kaleido.Samples.PriorAuth.EventCollector.Data;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// The collector is not a Kaleido service, so it wires OpenTelemetry itself from its own
// "OpenTelemetry" section. As in Kaleido's AddOpenTelemetry(), the standard OTEL_* keys (set by
// compose) win over that section, and the SDK defaults apply when neither is set.
var telemetrySettings = builder.Configuration.GetSection("OpenTelemetry");

string? Configured(string standardKey, string key) =>
    string.IsNullOrWhiteSpace(builder.Configuration[standardKey]) ? telemetrySettings[key] : null;

var serviceName = telemetrySettings["ServiceName"] ?? builder.Environment.ApplicationName;
var endpoint = Configured("OTEL_EXPORTER_OTLP_ENDPOINT", "Endpoint");
var protocol = Configured("OTEL_EXPORTER_OTLP_PROTOCOL", "Protocol");

void ConfigureExporter(OtlpExporterOptions exporter, string signal)
{
    if (string.Equals(protocol, "http/protobuf", StringComparison.OrdinalIgnoreCase))
    {
        exporter.Protocol = OtlpExportProtocol.HttpProtobuf;
    }

    if (endpoint is not null)
    {
        // The SDK appends the signal path only to an endpoint it read from OTEL_*.
        exporter.Endpoint = exporter.Protocol == OtlpExportProtocol.HttpProtobuf
            ? new Uri($"{endpoint.TrimEnd('/')}/v1/{signal}")
            : new Uri(endpoint);
    }
}

void ConfigureResource(ResourceBuilder resource)
{
    resource.AddService(serviceName: serviceName);

    var attributes = (telemetrySettings["ResourceAttributes"] ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(x => x.Split('=', 2, StringSplitOptions.TrimEntries))
        .Where(x => x.Length == 2 && x[0].Length > 0)
        .Select(x => new KeyValuePair<string, object>(x[0], x[1]));

    // OTEL_SERVICE_NAME and OTEL_RESOURCE_ATTRIBUTES are applied last, so they win.
    resource.AddAttributes(attributes).AddEnvironmentVariableDetector();
}

builder.Services.AddOpenTelemetry()
    .ConfigureResource(ConfigureResource)
    .WithLogging(
        logging => logging.AddOtlpExporter(o => ConfigureExporter(o, "logs")),
        options =>
        {
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        })
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter(o => ConfigureExporter(o, "traces"));
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter(o => ConfigureExporter(o, "metrics"));
    });

var eventCollectorConnectionString =
    builder.Configuration.GetConnectionString("EventCollector")
    ?? throw new Kaleido.Exceptions.KaleidoConfigurationException(
        PriorAuthErrorCodes.MissingConnectionString,
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
    {
        processId = pid;
    }

    var stepName = context?["stepName"]?.GetValue<string>();

    // OccurredOn lives on the event payload
    var occurredOnStr = eventNode?["occurredOn"]?.GetValue<string>();
    var occurredOn = occurredOnStr is not null
        ? DateTimeOffset.Parse(occurredOnStr, CultureInfo.InvariantCulture).UtcDateTime
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
