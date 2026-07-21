using Kaleido;
using Kaleido.Samples.SQLite;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Kaleido.Samples.Shared;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });
});

builder.Services.AddKaleido(options =>
{
    options.RegisterAssembly(typeof(SampleKaleidoRecord).Assembly);
    options.RegisterAssembly(typeof(SampleKaleidoRecordSource).Assembly);
});

builder.Services.AddDbContext<KaleidoTestDbContext>(options =>
{
    options.UseSqlite("Data Source=kaleido-test-data.sqlite");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1"));
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

using var scope = app.Services.CreateScope();
var sp = scope.ServiceProvider;
using var dbContext = sp.GetRequiredService<KaleidoTestDbContext>();
await DbInitializer.InitializeAsync(dbContext);

app.Run();


// Make the implicit Program class available for integration tests
public partial class Program { }