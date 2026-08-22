using Kaleido.Samples.PriorAuth.Intake.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<IntakeDbContext>(
    options => options.UseSqlite(
        builder.Configuration.GetConnectionString("Intake")
        ?? "Data Source=data/intake.db"));

builder.Services.AddHttpClient("ReferenceData", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["Services:ReferenceData:BaseUrl"]
        ?? "https://localhost:8441");
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IntakeDbContext>();

var app = builder.Build();

app.MapHealthChecks("/health");

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<IntakeDbContext>();
    var httpClientFactory =
        scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
    var logger =
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("Startup");

    await dbContext.Database.EnsureCreatedAsync();

    try
    {
        using var referenceDataResponse =
            await httpClientFactory
                .CreateClient("ReferenceData")
                .GetAsync("/health");

        referenceDataResponse.EnsureSuccessStatusCode();

        logger.LogInformation(
            "Verified ReferenceData connectivity at startup with status code {StatusCode}.",
            (int)referenceDataResponse.StatusCode);
    }
    catch (Exception ex)
    {
        logger.LogError(
            ex,
            "Failed to verify ReferenceData connectivity at startup.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

app.Run();
