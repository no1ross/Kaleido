//using Kaleido;
//using Kaleido.Queryable;
//using Kaleido.Queryable.AspNetCore;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddDbContext<ProviderSearchDbContext>(
//    options => options.UseSqlite(
//        builder.Configuration.GetConnectionString("ProviderSearch")
//        ?? "Data Source=data/providersearch.db"));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

//builder.Services.AddKaleido()
//    .AddAssembly(typeof(Program).Assembly)
//    .AddAssembly(typeof(ProviderSearchDbContext).Assembly)
//    .AddQueryable()
//        .AddQueryableAspNetCore();

var app = builder.Build();

//app.MapQueryable();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
