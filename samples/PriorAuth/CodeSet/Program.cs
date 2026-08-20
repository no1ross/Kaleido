using Kaleido;
using Kaleido.Queryable;
using Kaleido.Queryable.AspNetCore;
using Kaleido.Samples.PriorAuth.CodeSet.Artifacts.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<CodeSetDbContext>(
    options => options.UseSqlite(
        builder.Configuration.GetConnectionString("CodeSet")
        ?? "Data Source=data/codeset.db"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddKaleido()
    .AddAssembly(typeof(Program).Assembly)
    .AddAssembly(typeof(CodeSetDbContext).Assembly)
    .AddQueryable()
        .AddQueryableAspNetCore();

var app = builder.Build();

app.MapQueryable();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
