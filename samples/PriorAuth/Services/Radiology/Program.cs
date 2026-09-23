using Kaleido;
using Kaleido.Exceptions;
using Kaleido.Http;
using Kaleido.Http.Client;
using Kaleido.Http.Process;
using Kaleido.Http.Queryable;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Provider.SQLite;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Radiology.Data;
using Kaleido.Samples.PriorAuth.Radiology.Process.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var radiologyConnectionString =
    builder.Configuration.GetConnectionString("Radiology")
    ?? throw new KaleidoConfigurationException(
        ConfigurationErrorCodes.InvalidServiceName,
        "ConnectionStrings:Radiology is required.");

var processConnectionString =
    builder.Configuration.GetConnectionString("RadiologyProcess")
    ?? throw new KaleidoConfigurationException(
        ConfigurationErrorCodes.InvalidServiceName,
        "ConnectionStrings:RadiologyProcess is required.");

builder.Services.AddDbContext<RadiologyDbContext>(
    options => options.UseSqlite(radiologyConnectionString));

builder.Services.AddScoped<IMemberEligibilityService, MemberEligibilityService>();
builder.Services.AddScoped<MemberDetailsClient>();
builder.Services.AddScoped<ProcedureCodeClient>();
builder.Services.AddScoped<ProcedureModalityClient>();
builder.Services.AddScoped<MriProcedureCodeResolverClient>();
builder.Services.AddScoped<QuestionnaireDefinitionClient>();
builder.Services.AddScoped<RequestingProviderSearchClient>();
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
    .AddDbContextCheck<RadiologyDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "radiology";
        o.Assemblies = [typeof(Program).Assembly, typeof(RadiologyDbContext).Assembly];
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.Radiology", StringComparison.Ordinal) ?? false;
    })
    .AddEventPublisher<HttpEventPublisher>()
    .AddHttp()
    .UseSqliteContextStore(processConnectionString)
    .AddHttpClients()
    .AddOpenTelemetry();

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");

app.MapProcessor();
app.MapQueryable();

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<RadiologyDbContext>();
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
