using Kaleido;
using Kaleido.Http;
using Kaleido.Http.Queryable;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Configuration.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var configurationConnectionString =
    builder.Configuration.GetConnectionString("Configuration")
    ?? throw new Kaleido.Exceptions.KaleidoConfigurationException(
        "ConnectionStrings:Configuration is required.");

builder.Services.AddDbContext<ConfigurationDbContext>(
    options => options.UseSqlite(configurationConnectionString));

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
    .AddDbContextCheck<ConfigurationDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "configuration";
        o.Assemblies = new System.Reflection.Assembly[] { typeof(Program).Assembly, typeof(ConfigurationDbContext).Assembly };
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.Configuration") ?? false;
    })
    .AddEventPublisher<HttpEventPublisher>()
    .AddHttp()
    .AddOpenTelemetry();

var app = builder.Build();

app.UseCors("AllowAll");

app.MapHealthChecks("/health");
app.MapQueryable();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
