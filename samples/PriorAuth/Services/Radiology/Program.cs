using Kaleido.Samples.PriorAuth.Compliance;
using Kaleido;
using Kaleido.Exceptions;
using Kaleido.Http;
using Kaleido.Http.Client;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Provider.SQLite;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Auth;
using Kaleido.Samples.PriorAuth.Clinical;
using Kaleido.Samples.PriorAuth.Questionnaires;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var radiologyConnectionString =
    builder.Configuration.GetConnectionString("Radiology")
    ?? throw new KaleidoConfigurationException(
        PriorAuthErrorCodes.MissingConnectionString,
        "ConnectionStrings:Radiology is required.");

var processConnectionString =
    builder.Configuration.GetConnectionString("RadiologyProcess")
    ?? throw new KaleidoConfigurationException(
        PriorAuthErrorCodes.MissingConnectionString,
        "ConnectionStrings:RadiologyProcess is required.");

builder.Services.AddDbContext<RadiologyDbContext>(
    options => options.UseSqlite(radiologyConnectionString));

builder.Services.AddScoped<IMemberEligibilityService, MemberEligibilityService>();
builder.Services.AddScoped<MemberDetailsClient>();
builder.Services.AddScoped<ProcedureCodeClient>();
builder.Services.AddScoped<ProcedureModalityClient>();
builder.Services.AddScoped<MriProcedureCodeResolverClient>();
builder.Services.AddScoped<ConfiguredInformationRequests>();
builder.Services.AddScoped<ConfiguredQuestionnaireClient>();
builder.Services.AddScoped<RadiologyClinicalReview>();

// Who decides the clinical questions is configuration: the business's pathways in the
// Configuration service (default) or a third-party criteria service (simulated here).
if (string.Equals(builder.Configuration["Clinical:Provider"], "SimulatedCriteriaService", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddScoped<IClinicalQuestionProvider, SimulatedCriteriaServiceProvider>();
}
else
{
    builder.Services.AddScoped<IClinicalQuestionProvider, ConfiguredClinicalPathwayProvider>();
}
builder.Services.AddScoped<RequestingProviderSearchClient>();
builder.Services.AddScoped<HistoryClient>();


// Dev-token auth (sample stand-in for a real IdP); outgoing Kaleido calls are authorized by\n// DevCallCredentialProvider (registered on the Kaleido builder below).
builder.Services.AddDevAuth();

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
builder.Services.AddDevSwagger();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<RadiologyDbContext>()
    .AddDbContextCheck<SqliteKaleidoContextDbContext>("kaleido-sqlite-context-store");

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "radiology";
        o.Assemblies = [typeof(Program).Assembly, typeof(RadiologyDbContext).Assembly];
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.Radiology", StringComparison.Ordinal) ?? false;
        o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust;
        o.DefaultAuthorization = new(null, ["radiology"]);
    })
    .AddEventPublisher<HttpEventPublisher>()
    // Events: PHI erased, PII kept; logs are configured separately (ADR 0025).
    .AddPriorAuthEventRedaction()
    // Sample: return framework diagnostics (e.g. pro_step_not_available) in frameworkMessages.
    .AddHttp(o => o.IncludeFrameworkMessages = true)
    // Process state, Kaleido's outbox and idempotent requests in one SQLite database: a step's state
    // and its event commit together (ADR 0023).
    .AddSqliteContextStore(processConnectionString)
    .AddSqliteOutboxStore(processConnectionString)
    .AddSqliteIdempotencyStore(processConnectionString)
    .AddHttpClients()
    // Outgoing calls: an on-behalf-of dev token for the caller (user + roles + this service as actor),
    // a service token for the service's own calls (ADR 0022).
    .AddCallCredentials<DevCallCredentialProvider>()
    .AddOpenTelemetry();

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");

app.UseDevAuth();

app.MapKaleidoHttp();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<RadiologyDbContext>();
    var processDbContext =
        scope.ServiceProvider.GetRequiredService<SqliteKaleidoContextDbContext>();

    await dbContext.Database.EnsureCreatedAsync();
    await processDbContext.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseDevSwaggerUI();
}


app.Run();
