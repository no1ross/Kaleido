using Kaleido;
using Kaleido.Http;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Auth;
using Kaleido.Samples.PriorAuth.CodeSet.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var codeSetConnectionString =
    builder.Configuration.GetConnectionString("CodeSet")
    ?? throw new Kaleido.Exceptions.KaleidoConfigurationException(Kaleido.Exceptions.ConfigurationErrorCodes.InvalidServiceName, "ConnectionStrings:CodeSet is required.");

builder.Services.AddDbContext<CodeSetDbContext>(
    options => options.UseSqlite(codeSetConnectionString));


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
    .AddDbContextCheck<CodeSetDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "codeset";
        o.Assemblies = new System.Reflection.Assembly[] { typeof(Program).Assembly, typeof(CodeSetDbContext).Assembly };
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.CodeSet", StringComparison.Ordinal) ?? false;
        o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust;
        o.DefaultAuthorization = new(DevAuthPolicies.AuthenticatedUser, []);
    })
    .AddEventPublisher<HttpEventPublisher>()
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
