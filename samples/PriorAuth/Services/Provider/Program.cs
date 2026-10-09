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
        ConfigurationErrorCodes.InvalidServiceName,
        "ConnectionStrings:Provider is required.");

builder.Services.AddDbContext<ProviderSearchDbContext>(
    options => options.UseSqlite(providerConnectionString));

builder.Services.AddScoped<PlanNetworkClient>();


// Dev-token auth (sample stand-in for a real IdP) + outbound token forwarding.
builder.Services.AddDevAuth();
builder.Services.AddDevTokenForwarding("ReferenceData");
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
    .AddHttp()
    .AddHttpClients()
    .AddOpenTelemetry();

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");
app.UseDevAuth();

app.MapKaleidoHttp();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.Run();
