using Kaleido;
using Kaleido.Http;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Auth;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var configurationConnectionString =
    builder.Configuration.GetConnectionString("Configuration")
    ?? throw new Kaleido.Exceptions.KaleidoConfigurationException(Kaleido.Exceptions.ConfigurationErrorCodes.InvalidServiceName, "ConnectionStrings:Configuration is required.");

builder.Services.AddDbContext<ConfigurationDbContext>(
    options => options.UseSqlite(configurationConnectionString));

builder.Services.AddControllers();

// Dev-token auth (sample stand-in for a real IdP).
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
    .AddDbContextCheck<ConfigurationDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "configuration";
        o.Assemblies = new System.Reflection.Assembly[] { typeof(Program).Assembly, typeof(ConfigurationDbContext).Assembly };
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.Configuration", StringComparison.Ordinal) ?? false;
        o.AuthorizationMode = KaleidoAuthorizationMode.Authenticated;
    })
    .AddEventPublisher<HttpEventPublisher>()
    .AddHttp()
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

app.MapControllers();

app.Run();
