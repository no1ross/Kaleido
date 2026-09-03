using Kaleido;
using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore;
using Kaleido.Process;
using Kaleido.Process.AspNetCore;
using Kaleido.Samples.PriorAuth.Common;
using Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data;
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
            .AddKaleidoProcessInstrumentation()
            .AddKaleidoQueryableInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics
            .AddKaleidoProcessInstrumentation()
            .AddKaleidoQueryableInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation()
            .AddOtlpExporter();
    });

builder.Services.AddDbContext<MemberDbContext>(
    options => options.UseSqlite(
        builder.Configuration.GetConnectionString("MemberService")
        ?? "Data Source=data/memberservice.db"));

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
    .AddDbContextCheck<MemberDbContext>();

//builder.Services.AddPriorAuthEventPublishing(
//    builder.Configuration);

builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(MemberDbContext).Assembly)
    .AddParticipant(o =>
        {
            o.Name = "member-service";
            o.Description = "Member service participant.";
            o.Version = "1.0.0";
            o.DisplayName = "Member Service";
        })
        .AddParticipantAspNetCore(o =>
        {
            o.RoutePrefix = "member";
        })
    .AddQueryable()
        .AddQueryableAspNetCore(o =>
        {
            o.RoutePrefix = "member";
        });

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");
app.MapQueryable();
app.MapParticipant();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
