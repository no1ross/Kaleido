using Kaleido;
using Kaleido.Http;
using Kaleido.Http.Queryable;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.CodeSet.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var codeSetConnectionString =
    builder.Configuration.GetConnectionString("CodeSet")
    ?? throw new Kaleido.Exceptions.KaleidoConfigurationException(
        "ConnectionStrings:CodeSet is required.");

builder.Services.AddDbContext<CodeSetDbContext>(
    options => options.UseSqlite(codeSetConnectionString));

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
    .AddDbContextCheck<CodeSetDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "codeset";
        o.Assemblies = new System.Reflection.Assembly[] { typeof(Program).Assembly, typeof(CodeSetDbContext).Assembly };
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.CodeSet") ?? false;
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
