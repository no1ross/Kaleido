using Kaleido;
using Kaleido.Http;
using Kaleido.Observability.OpenTelemetry;
using Kaleido.Samples.PriorAuth;
using Kaleido.Samples.PriorAuth.Auth;
using Kaleido.Samples.PriorAuth.Member.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var memberConnectionString =
    builder.Configuration.GetConnectionString("Member")
    ?? throw new Kaleido.Exceptions.KaleidoConfigurationException(Kaleido.Exceptions.ConfigurationErrorCodes.InvalidServiceName, "ConnectionStrings:Member is required.");

builder.Services.AddDbContext<MemberDbContext>(
    options => options.UseSqlite(memberConnectionString));


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
    .AddDbContextCheck<MemberDbContext>();

builder.Services.AddHttpClient("PriorAuthEventCollector", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["Services:EventCollector:BaseUrl"]
        ?? "http://localhost:8086"));

builder.Services.AddKaleido(builder.Configuration, o =>
    {
        o.ServiceName = "member";
        o.Assemblies = new System.Reflection.Assembly[] { typeof(Program).Assembly, typeof(MemberDbContext).Assembly };
        o.TypeFilter = type => type.Namespace?.StartsWith("Kaleido.Samples.PriorAuth.Member", StringComparison.Ordinal) ?? false;
        o.AuthorizationMode = KaleidoAuthorizationMode.ZeroTrust;
        o.DefaultAuthorization = new(DevAuthPolicies.AuthenticatedUser, []);
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


app.Run();
