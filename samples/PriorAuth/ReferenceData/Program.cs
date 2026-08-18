using Kaleido.Samples.PriorAuth.ReferenceData.Artifacts.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReferenceDataDbContext>(
    options => options.UseSqlite(
        builder.Configuration.GetConnectionString("ReferenceData")
        ?? "Data Source=data/referencedata.db"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await ReferenceDataDbInitializer.InitializeAsync(app.Services);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
