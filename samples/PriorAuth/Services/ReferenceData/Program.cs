using Kaleido;
using Kaleido.Http;
using Kaleido.Http.Queryable;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.ReferenceData.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var referenceDataConnectionString =
    builder.Configuration.GetConnectionString("ReferenceData")
    ?? throw new Kaleido.Exceptions.KaleidoConfigurationException(
        "ConnectionStrings:ReferenceData is required.");

builder.Services.AddDbContext<ReferenceDataDbContext>(
    options => options.UseSqlite(referenceDataConnectionString));

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
    .AddDbContextCheck<ReferenceDataDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "referencedata";
        o.Assemblies = new System.Reflection.Assembly[] { typeof(Program).Assembly, typeof(ReferenceDataDbContext).Assembly };
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.ReferenceData") ?? false;
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
