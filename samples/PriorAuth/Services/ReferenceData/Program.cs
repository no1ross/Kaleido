using Kaleido.Samples.PriorAuth.Queryable;
using Kaleido.Samples.PriorAuth.Compliance;
using Kaleido;
using Kaleido.Http;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Auth;
using Kaleido.Samples.PriorAuth.ReferenceData.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var referenceDataConnectionString =
    builder.Configuration.GetConnectionString("ReferenceData")
    ?? throw new Kaleido.Exceptions.KaleidoConfigurationException(PriorAuthErrorCodes.MissingConnectionString, "ConnectionStrings:ReferenceData is required.");

builder.Services.AddDbContext<ReferenceDataDbContext>(
    options => options.UseSqlite(referenceDataConnectionString));


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
    .AddDbContextCheck<ReferenceDataDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "referencedata";
        o.Assemblies = new System.Reflection.Assembly[] { typeof(Program).Assembly, typeof(ReferenceDataDbContext).Assembly };
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.ReferenceData", StringComparison.Ordinal) ?? false;
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseDevSwaggerUI();
}


app.Run();
