using Kaleido;
using Kaleido.Exceptions;
using Kaleido.Http;
using Kaleido.Http.Client;
using Kaleido.Http.Process;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Provider.SQLite;
using Kaleido.Samples.PriorAuth;
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
    })
    .AddEventPublisher<HttpEventPublisher>()
    .AddHttp()
    .UseSqliteProcessContextStore(processConnectionString)
    .AddHttpClients()
    .AddOpenTelemetry();

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");

app.MapProcessor();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
    var processDbContext =
        scope.ServiceProvider.GetRequiredService<SqliteProcessContextDbContext>();

    await dbContext.Database.EnsureCreatedAsync();
    await processDbContext.Database.EnsureCreatedAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
