using Kaleido;
using Kaleido.Exceptions;
using Kaleido.Http;
using Kaleido.Http.Client;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Provider.SQLite;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Auth;
using Kaleido.Samples.PriorAuth.Intake.Data;
using Kaleido.Samples.PriorAuth.Intake.Process.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var intakeConnectionString =
    builder.Configuration.GetConnectionString("Intake")
    ?? throw new KaleidoConfigurationException(
        ConfigurationErrorCodes.InvalidServiceName,
        "ConnectionStrings:Intake is required.");

var processConnectionString =
    builder.Configuration.GetConnectionString("IntakeProcess")
    ?? throw new KaleidoConfigurationException(
        ConfigurationErrorCodes.InvalidServiceName,
        "ConnectionStrings:IntakeProcess is required.");

builder.Services.AddDbContext<IntakeDbContext>(
    options => options.UseSqlite(intakeConnectionString));

builder.Services.AddScoped<MemberDetailsClient>();
builder.Services.AddScoped<ProcedureCodeClient>();
builder.Services.AddScoped<ProductCodeMappingClient>();
builder.Services.AddScoped<HistoryClient>();


// Dev-token auth (sample stand-in for a real IdP) + outbound token forwarding
// so downstream Kaleido calls carry an on-behalf-of token (the user + this
// service as actor) or a service token.
builder.Services.AddDevAuth();
builder.Services.AddDevTokenForwarding(
    "CodeSet", "Configuration", "History", "Member", "Provider", "Radiology");
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
    .AddDbContextCheck<IntakeDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "intake";
        o.Assemblies = new[] { typeof(Program).Assembly, typeof(IntakeDbContext).Assembly };
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.Intake", StringComparison.Ordinal) ?? false;
        o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust;
        o.DefaultAuthorization = new(DevAuthPolicies.AuthenticatedUser, []);
    })
    .AddEventPublisher<HttpEventPublisher>()
    // Sample: return framework diagnostics (e.g. pro_step_not_available) in frameworkMessages.
    .AddHttp(o => o.IncludeFrameworkMessages = true)
    .UseSqliteProcessorContextStore(processConnectionString)
    .AddHttpClients()
    .AddOpenTelemetry();

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");

app.UseDevAuth();

app.MapKaleidoHttp();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
    var processDbContext =
        scope.ServiceProvider.GetRequiredService<SqliteProcessorContextDbContext>();

    await dbContext.Database.EnsureCreatedAsync();
    await processDbContext.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseDevSwaggerUI();
}


app.Run();
