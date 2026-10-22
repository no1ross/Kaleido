using Kaleido.Samples.PriorAuth.Audit;
using Kaleido.Samples.PriorAuth.Queryable;
using Kaleido.Samples.PriorAuth.Compliance;
using Kaleido;
using Kaleido.Exceptions;
using Kaleido.Http;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Auth;
using Kaleido.Samples.PriorAuth.History.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var historyConnectionString =
    builder.Configuration.GetConnectionString("History")
    ?? throw new KaleidoConfigurationException(
        PriorAuthErrorCodes.MissingConnectionString,
        "ConnectionStrings:History is required.");

// Who created and last changed each row, through which application, and when (Common/Audit).
builder.Services.AddPriorAuthAuditing();
builder.Services.AddDbContext<HistoryDbContext>(
    (services, options) => options.UseSqlite(historyConnectionString).UseAuditing(services));


// Dev-token auth (sample stand-in for a real IdP).
builder.Services.AddDevAuth();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            // The UI reads the idempotency headers of execute answers across origins (#396).
            .WithExposedHeaders("Retry-After", "Idempotent-Replayed");
    });
});
builder.Services.AddDevSwagger();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<HistoryDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "history";
        o.Assemblies = new System.Reflection.Assembly[] { typeof(Program).Assembly, typeof(HistoryDbContext).Assembly };
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.History", StringComparison.Ordinal) ?? false;
        o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust;
        o.DefaultAuthorization = new(DevAuthPolicies.AuthenticatedUser, []);
    })
    .AddEventPublisher<HttpEventPublisher>()
    // Events: PHI erased, PII kept; logs are configured separately (ADR 0025).
    .AddPriorAuthEventRedaction()
    // Full pages are counted with one SELECT COUNT(*) (EF Core's operators, #352).
    .AddPriorAuthEfCoreQueries()
    // Sample: return framework diagnostics (e.g. pro_step_not_available) in frameworkMessages.
    .AddHttp(o => o.IncludeFrameworkMessages = true)
    .AddOpenTelemetry();

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");

app.UseDevAuth();

app.MapKaleidoHttp();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<HistoryDbContext>();

    await dbContext.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseDevSwaggerUI();
}


app.Run();
