using Kaleido.Samples.PriorAuth.Queryable;
using Kaleido.Samples.PriorAuth.Compliance;
using Kaleido;
using Kaleido.Exceptions;
using Kaleido.Http;
using Kaleido.Http.Client;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Auth;
using Kaleido.Samples.PriorAuth.Provider.Data;
using Kaleido.Samples.PriorAuth.Provider.Queryable.Clients;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var providerConnectionString =
    builder.Configuration.GetConnectionString("Provider")
    ?? throw new KaleidoConfigurationException(
        PriorAuthErrorCodes.MissingConnectionString,
        "ConnectionStrings:Provider is required.");

builder.Services.AddDbContext<ProviderSearchDbContext>(
    options => options.UseSqlite(providerConnectionString));

builder.Services.AddScoped<PlanNetworkClient>();


// Dev-token auth (sample stand-in for a real IdP); outgoing Kaleido calls are authorized by\n// DevCallCredentialProvider (registered on the Kaleido builder below).
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
    .AddDbContextCheck<ProviderSearchDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "provider";
        o.Assemblies = new System.Reflection.Assembly[] { typeof(Program).Assembly, typeof(ProviderSearchDbContext).Assembly };
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.Provider", StringComparison.Ordinal) ?? false;
        o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust;
        o.DefaultAuthorization = new(DevAuthPolicies.InternalCaller, []);
    })
    .AddEventPublisher<HttpEventPublisher>()
    // Events: PHI erased, PII kept; logs are configured separately (ADR 0025).
    .AddPriorAuthEventRedaction()
    // Full pages are counted with one SELECT COUNT(*) (EF Core's operators, #352).
    .AddPriorAuthEfCoreQueries()
    // Sample: return framework diagnostics (e.g. pro_step_not_available) in frameworkMessages.
    .AddHttp(o => o.IncludeFrameworkMessages = true)
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseDevSwaggerUI();
}


app.Run();
